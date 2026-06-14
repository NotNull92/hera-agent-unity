package cmd

import (
	"encoding/json"
	"fmt"
	"os"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

// uiDocCmd handles the ui_doc tool. export and gen_sprite are simple
// passthroughs; apply reads the IR document from --file so the (potentially
// large) doc never rides inline in the agent's context — it is parsed here and
// injected as the `doc` param. Synchronous: apply creates GameObjects and
// imports generated sprites, neither of which triggers a domain reload.
func uiDocCmd(args []string, send SendFunc) (*client.CommandResponse, error) {
	args, doc, err := extractDocFile(args)
	if err != nil {
		return nil, err
	}

	params, _, err := buildParams(args, nil)
	if err != nil {
		return nil, err
	}
	if doc != nil {
		params["doc"] = doc
	}

	return send("ui_doc", params)
}

// extractDocFile strips `--file <path>` and returns the parsed JSON document.
// Returns a nil doc when --file is absent (export / gen_sprite / inline --doc).
func extractDocFile(args []string) ([]string, interface{}, error) {
	var out []string
	var filePath string
	for i := 0; i < len(args); i++ {
		if args[i] == "--file" {
			if i+1 >= len(args) {
				return nil, nil, fmt.Errorf("--file requires a path argument")
			}
			filePath = args[i+1]
			i++
			continue
		}
		out = append(out, args[i])
	}

	if filePath == "" {
		return out, nil, nil
	}

	data, err := os.ReadFile(filePath)
	if err != nil {
		return nil, nil, fmt.Errorf("read --file %s: %w", filePath, err)
	}
	var doc interface{}
	if err := json.Unmarshal(data, &doc); err != nil {
		return nil, nil, fmt.Errorf("parse --file %s as JSON: %w", filePath, err)
	}
	return out, doc, nil
}
