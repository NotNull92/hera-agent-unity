package cmd

import (
	"context"
	"fmt"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func runStandaloneCommand(category string, subArgs []string) (bool, error) {
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
	case "update":
		return true, updateCmd(subArgs)
	case "install":
		return true, installCmd()
	case "uninstall":
		return true, uninstallCmd()
	case "status":
		inst, err := discoverStatusInstance(flagProject, flagPort)
		if err != nil {
			return true, err
		}
		statusErr := statusCmd(inst)
		printUpdateNotice(category)
		return true, statusErr
	case "ping":
		return true, pingCmd(flagProject, flagPort)
	case "asset-config":
		return true, assetConfigCmd(subArgs)
	case "doctor":
		return true, doctorCmd(subArgs)
	case "html-to-uidoc":
		return true, htmlToUIDocCmd(subArgs)
	}
	return false, nil
}

func runUnityCommand(ctx context.Context, category string, subArgs []string, send SendFunc, resolve instanceResolver) (*client.CommandResponse, error) {
	var resp *client.CommandResponse
	var err error

	switch category {
	case "batch":
		err := batchCmd(ctx, subArgs, client.SendBatch, resolve, flagTimeout)
		if err != nil {
			return nil, err
		}
		return &client.CommandResponse{Success: true}, nil
	case "editor":
		resp, err = editorCmd(subArgs, send, resolve, category)
	case "test":
		resp, err = testCmd(subArgs, send, resolve)
	case "manage_packages":
		resp, err = managePackagesCmd(subArgs, send, resolve)
	case "unity_docs":
		resp, err = unityDocsCmd(subArgs, send)
	case "ui_doc":
		resp, err = uiDocCmd(subArgs, send)
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
			resp, err = send("exec", params)
		}
	default:
		var params map[string]interface{}
		params, _, err = buildParams(subArgs, nil)
		if err == nil {
			resp, err = send(category, params)
		}
	}

	return resp, err
}
