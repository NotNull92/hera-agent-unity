package cmd

import (
	"fmt"
	"os"

	"github.com/NotNull92/hera-agent-unity/internal/assetconfig"
	"github.com/NotNull92/hera-agent-unity/internal/client"
)

// unityDocsCmd resolves the offline Unity Documentation directory on the
// CLI side and forwards the request. Path resolution lives here, not in
// the connector, because env / asset-config / autodetect all run in the
// user's shell context — the connector just receives the absolute path.
//
// Priority (first hit wins):
//
//  1. --docs-path / --docs_root flag (or 'docs_root' in --params)
//  2. HERA_AGENT_UNITY_DOCS environment variable
//  3. asset-config unity_docs_path (persisted by `asset-config unity-docs`)
//  4. assetconfig.DetectUnityDocsPath() probe of well-known locations
//
// If nothing resolves, we still forward the call — the connector emits a
// DOCS_NOT_CONFIGURED error with the recovery commands as suggestions.
func unityDocsCmd(args []string, send sendFn) (*client.CommandResponse, error) {
	params, err := buildParams(args, nil)
	if err != nil {
		return nil, err
	}

	// --docs-path is the human-facing flag; rename to the wire key.
	if v, ok := params["docs-path"]; ok {
		if _, alreadySet := params["docs_root"]; !alreadySet {
			params["docs_root"] = v
		}
		delete(params, "docs-path")
	}

	if _, ok := params["docs_root"]; !ok {
		if env := os.Getenv("HERA_AGENT_UNITY_DOCS"); env != "" {
			params["docs_root"] = env
		}
	}

	if _, ok := params["docs_root"]; !ok {
		if persisted, _ := assetconfig.GetUnityDocsPath(); persisted != "" {
			params["docs_root"] = persisted
		}
	}

	if _, ok := params["docs_root"]; !ok {
		if detected := assetconfig.DetectUnityDocsPath(); detected != "" {
			params["docs_root"] = detected
			if isHumanCommand() || flagVerbose {
				fmt.Fprintf(os.Stderr,
					"[hera-agent-unity] using autodetected docs_root: %s\n", detected)
			}
		}
	}

	return send("unity_docs", params)
}
