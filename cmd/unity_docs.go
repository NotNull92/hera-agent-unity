package cmd

import "github.com/NotNull92/hera-agent-unity/internal/client"

// unityDocsCmd is a thin passthrough — the connector now ships its own
// docs data set under AgentConnector/Editor/Data/, so the CLI no longer
// has to resolve / forward a docs_root path. Kept as a named case so the
// help text under printTopicHelp("unity_docs") still has a dedicated home.
func unityDocsCmd(args []string, send SendFunc) (*client.CommandResponse, error) {
	params, err := buildParams(args, nil)
	if err != nil {
		return nil, err
	}
	return send("unity_docs", params)
}
