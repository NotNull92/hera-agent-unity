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
	case "editor":
		if !isEditorBootstrapAction(subArgs) {
			return false, nil
		}
		resp, bootstrapErr := runEditorBootstrap(ctx, subArgs, runner.config, defaultEditorBootstrapRuntime())
		if bootstrapErr != nil {
			return true, bootstrapErr
		}
		(&ResponsePrinter{
			Quiet:       runner.config.Quiet,
			CompactJSON: runner.config.CompactJSON,
		}).Print(resp, category)
		if !resp.Success {
			return true, ErrCommandFailed
		}
		return true, nil
	case "task":
		return true, taskCmd(runner.config, subArgs)
	case "asset-config":
		if len(subArgs) > 0 && subArgs[0] == "detect" {
			return false, nil
		}
		return true, assetConfigCmd(subArgs)
	case "doctor":
		return true, doctorCmd(subArgs)
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
	send := runner.send
	if category != "call" {
		var approvalToken string
		subArgs, approvalToken, err = extractLegacyApproval(subArgs)
		if err != nil {
			return nil, err
		}
		var preflight callPreflightFunc
		var resolveAction func(string, map[string]any) (string, error)
		if runner.instance != nil && instanceSupports(runner.instance, client.FeatureApprovalV1) {
			registry := toolregistry.NewRegistry(toolregistry.RegistryOptions{})
			preflight = func(request client.ApprovalPreflightRequest) (*client.ApprovalPreflight, error) {
				request.TimeoutMS = runner.config.TimeoutMillis()
				return client.DefaultClient.PreflightApproval(ctx, runner.instance, request)
			}
			resolveAction = func(command string, params map[string]any) (string, error) {
				snapshot, loadErr := registry.Load(ctx, runner.instance)
				if loadErr != nil {
					return "", fmt.Errorf("load tool catalog for approval: %w", loadErr)
				}
				tool, resolveErr := resolveCallTool(snapshot.Catalog, command, "")
				if resolveErr != nil {
					return "", resolveErr
				}
				return resolveLegacyAction(tool, params), nil
			}
		}
		send = withLegacyApproval(send, legacyApprovalOptions{
			token:         approvalToken,
			preflight:     preflight,
			resolveAction: resolveAction,
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
			interactive: approvalTTY(os.Stdin, os.Stderr),
			confirm: func(summary client.ApprovalSummary) (bool, error) {
				return promptCallApproval(os.Stdin, os.Stderr, summary)
			},
		})
	}

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
			preflight: func(request client.ApprovalPreflightRequest) (*client.ApprovalPreflight, error) {
				request.TimeoutMS = runner.config.TimeoutMillis()
				return client.DefaultClient.PreflightApproval(ctx, runner.instance, request)
			},
			input:       detectCallInput(os.Stdin),
			interactive: approvalTTY(os.Stdin, os.Stderr),
			confirm: func(summary client.ApprovalSummary) (bool, error) {
				return promptCallApproval(os.Stdin, os.Stderr, summary)
			},
		}
		resp, err = command.Run(ctx, runner.instance, subArgs)
	case "editor":
		resp, err = runEditorCmd(ctx, subArgs, editorRuntime{
			Config:  runner.config,
			Send:    send,
			Resolve: runner.resolve,
		})
	case "build":
		resp, err = buildCmd(ctx, subArgs, send, runner.config.Timeout)
	case "test":
		resp, err = testCmd(
			ctx,
			subArgs,
			send,
			runner.resolve,
			runner.config.Timeout,
		)
	case "manage_packages":
		resp, err = managePackagesCmd(ctx, subArgs, packageRuntime{
			Config:  runner.config,
			Send:    send,
			Resolve: runner.resolve,
		})
	case "unity_docs":
		resp, err = unityDocsCmd(subArgs, send)
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
			resp, err = send("detect_assets", params)
		}
	default:
		resp, err = runLegacyToolCommand(category, subArgs, send)
	}

	return resp, err
}
