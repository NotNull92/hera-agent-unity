package cmd

import (
	"context"
	"fmt"
	"os"

	"github.com/NotNull92/hera-agent-unity/internal/assetconfig"
	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

type standaloneRunner struct {
	config GlobalConfig
}

func (runner standaloneRunner) Run(
	ctx context.Context,
	category string,
	subArgs []string,
) (bool, error) {
	switch category {
	case "help", "--help", "-h":
		if len(subArgs) > 0 {
			printTopicHelp(subArgs[0])
		} else {
			printHelp()
		}
		return true, nil
	case "version", "--version", "-v":
		fmt.Println("hera-agent-unity " + Version)
		return true, nil
	case "mcp":
		return true, mcpCmd(ctx, runner.config, subArgs)
	case "update":
		return true, updateCmd(subArgs)
	case "install":
		return true, installCmd()
	case "uninstall":
		return true, uninstallCmd()
	case "status":
		resolve := func() (*client.Instance, error) {
			return discoverStatusInstance(runner.config.Project, runner.config.Port)
		}
		inst, err := waitForInstance(
			ctx,
			resolve,
			initialDiscoveryTimeoutMs(runner.config.TimeoutMillis()),
		)
		if err != nil {
			return true, err
		}
		statusErr := statusCmd(ctx, inst)
		printUpdateNoticeWithConfig(category, runner.config.Quiet)
		return true, statusErr
	case "ping":
		return true, pingCmd(runner.config.Project, runner.config.Port)
	case "asset-config":
		if len(subArgs) > 0 && subArgs[0] == "detect" {
			return false, nil
		}
		return true, assetConfigCmd(subArgs)
	case "doctor":
		return true, doctorCmd(subArgs)
	case "html-to-uidoc":
		return true, htmlToUIDocCmd(subArgs)
	}
	return false, nil
}

type unityCommandRunner struct {
	config   GlobalConfig
	send     SendFunc
	instance *client.Instance
	resolve  instanceResolver
}

func (runner unityCommandRunner) Run(
	ctx context.Context,
	category string,
	subArgs []string,
) (*client.CommandResponse, error) {
	var resp *client.CommandResponse
	var err error

	switch category {
	case "batch":
		err := batchCmd(ctx, subArgs, batchRuntime{
			Config:    runner.config,
			Instance:  runner.instance,
			SendBatch: client.SendBatch,
		})
		if err != nil {
			return nil, err
		}
		return &client.CommandResponse{Success: true}, nil
	case "call":
		registry := toolregistry.NewRegistry(toolregistry.RegistryOptions{})
		command := &callCommand{
			load: registry.Load,
			send: runner.send,
			sendOperation: func(
				command string,
				params map[string]any,
				options client.SendOptions,
			) (*client.CommandResponse, error) {
				return client.DefaultClient.SendWithOptions(
					ctx,
					runner.instance,
					command,
					params,
					runner.config.TimeoutMillis(),
					options,
				)
			},
			input: detectCallInput(os.Stdin),
		}
		resp, err = command.Run(ctx, runner.instance, subArgs)
	case "editor":
		resp, err = runEditorCmd(ctx, subArgs, editorRuntime{
			Config:  runner.config,
			Send:    runner.send,
			Resolve: runner.resolve,
		})
	case "test":
		resp, err = testCmd(
			ctx,
			subArgs,
			runner.send,
			runner.resolve,
			runner.config.Timeout,
		)
	case "manage_packages":
		resp, err = managePackagesCmd(ctx, subArgs, packageRuntime{
			Config:  runner.config,
			Send:    runner.send,
			Resolve: runner.resolve,
		})
	case "unity_docs":
		resp, err = unityDocsCmd(subArgs, runner.send)
	case "ui_doc":
		resp, err = uiDocCmd(subArgs, runner.send)
	case "asset-config":
		if len(subArgs) == 0 || subArgs[0] != "detect" {
			return nil, fmt.Errorf("unsupported Unity-backed asset-config command")
		}
		if _, err = assetconfig.Load(); err != nil {
			return nil, fmt.Errorf("initialize asset config before detection: %w", err)
		}
		var params map[string]interface{}
		params, _, err = buildParams(subArgs[1:], nil)
		if err == nil {
			resp, err = runner.send("detect_assets", params)
		}
	case "exec":
		subArgs, err = readExecFileIfPresent(subArgs)
		if err != nil {
			return nil, err
		}
		subArgs = readStdinIfPiped(subArgs)
		var params map[string]interface{}
		params, _, err = buildParams(subArgs, nil)
		if err == nil {
			if v, ok := params["check"].(bool); ok && v {
				params["compile_only"] = true
				delete(params, "check")
			}
			request := newToolRequest("exec", params)
			resp, err = runner.send(request.Command, request.Params)
		}
	default:
		var params map[string]interface{}
		params, _, err = buildParams(subArgs, nil)
		if err == nil {
			request := newToolRequest(category, params)
			resp, err = runner.send(request.Command, request.Params)
		}
	}

	return resp, err
}
