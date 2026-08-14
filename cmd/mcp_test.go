package cmd

import (
	"bytes"
	"context"
	"fmt"
	"io"
	"os"
	"slices"
	"strings"
	"testing"
	"time"

	"github.com/modelcontextprotocol/go-sdk/mcp"
)

type testReadCloser struct {
	io.Reader
	io.Closer
}

func TestMCPProcessHelper(t *testing.T) {
	if os.Getenv("HERA_MCP_TEST_HELPER") != "1" {
		return
	}
	os.Args = append([]string{"hera-agent-unity"}, strings.Fields(os.Getenv("HERA_MCP_TEST_ARGS"))...)
	if err := Execute(context.Background()); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	os.Exit(0)
}

func TestMCPStdoutContainsOnlyProtocolFrames(t *testing.T) {
	// Given
	port, home := startMCPUnityFixture(t)
	ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
	defer cancel()
	command := mcpHelperCommand(ctx, fmt.Sprintf("--port %d mcp --transport stdio --exposure profile --profile core", port))
	command.Env = append(command.Env, "HOME="+home, "USERPROFILE="+home)
	stdout, err := command.StdoutPipe()
	if err != nil {
		t.Fatal(err)
	}
	stdin, err := command.StdinPipe()
	if err != nil {
		t.Fatal(err)
	}
	var stderr bytes.Buffer
	command.Stderr = &stderr
	var protocol bytes.Buffer
	transport := &mcp.IOTransport{
		Reader: testReadCloser{Reader: io.TeeReader(stdout, &protocol), Closer: stdout},
		Writer: stdin,
	}
	if err := command.Start(); err != nil {
		t.Fatal(err)
	}
	client := mcp.NewClient(&mcp.Implementation{Name: "hera-mcp-test", Version: "test"}, nil)

	// When
	session, err := client.Connect(ctx, transport, nil)
	if err != nil {
		t.Fatalf("Connect() error = %v; stderr=%s", err, stderr.String())
	}
	result := session.InitializeResult()
	tools, err := session.ListTools(ctx, nil)
	if err != nil {
		t.Fatalf("ListTools() error = %v", err)
	}
	call, err := session.CallTool(ctx, &mcp.CallToolParams{
		Name:      "scene",
		Arguments: map[string]any{"action": "info"},
	})
	if err != nil {
		t.Fatalf("CallTool() error = %v", err)
	}
	if err := session.Close(); err != nil {
		t.Fatalf("Close() error = %v", err)
	}
	if err := command.Wait(); err != nil {
		t.Fatalf("process error = %v; stderr=%s", err, stderr.String())
	}

	// Then
	if result.ServerInfo == nil || result.ServerInfo.Name != "hera-agent-unity" {
		t.Fatalf("server info = %#v", result.ServerInfo)
	}
	if result.Capabilities == nil || result.Capabilities.Tools == nil {
		t.Fatalf("capabilities = %#v, want native tools", result.Capabilities)
	}
	if len(tools.Tools) != 1 || tools.Tools[0].Name != "scene" {
		t.Fatalf("tools = %#v, want scene", tools.Tools)
	}
	if call.IsError {
		t.Fatalf("native scene call = %#v, want success", call)
	}
	structured, ok := call.StructuredContent.(map[string]any)
	if !ok || structured["success"] != true {
		t.Fatalf("structured result = %#v", call.StructuredContent)
	}
	assertProtocolOnly(t, protocol.Bytes())
}

func TestMCPStderrMayContainDiagnostics(t *testing.T) {
	// Given
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()
	command := mcpHelperCommand(ctx, "mcp --transport http")
	var stdout bytes.Buffer
	var stderr bytes.Buffer
	command.Stdout = &stdout
	command.Stderr = &stderr

	// When
	err := command.Run()

	// Then
	if err == nil {
		t.Fatal("process succeeded, want unsupported transport failure")
	}
	if stdout.Len() != 0 {
		t.Fatalf("stdout = %q, want empty", stdout.String())
	}
	if !strings.Contains(stderr.String(), "unsupported MCP transport") {
		t.Fatalf("stderr = %q, want transport diagnostic", stderr.String())
	}
}

func TestMCPOptionsParseCompactAndArbitraryCodePermission(t *testing.T) {
	// Given
	args := []string{"--exposure", "compact", "--profile", "advanced", "--allow-arbitrary-code"}

	// When
	options, err := parseMCPOptions(args)

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if options.Exposure != "compact" || options.Profile != "advanced" || !options.AllowArbitraryCode {
		t.Fatalf("options=%#v", options)
	}
}

func TestMCPOptionsDefaultToCompactExposure(t *testing.T) {
	// Given
	t.Setenv("HERA_MCP_EXPOSURE", "")

	// When
	options, err := parseMCPOptions(nil)

	// Then
	if err != nil {
		t.Fatal(err)
	}
	if options.Exposure != "compact" {
		t.Fatalf("exposure=%q, want compact", options.Exposure)
	}
}

func TestMCPProcessDefaultsToCompactAndCallsDynamicCustomTool(t *testing.T) {
	// Given
	port, home := startMCPUnityFixture(t)
	ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
	defer cancel()
	command := mcpHelperCommand(ctx, fmt.Sprintf("--port %d mcp --transport stdio", port))
	command.Env = append(command.Env, "HOME="+home, "USERPROFILE="+home)
	stdout, err := command.StdoutPipe()
	if err != nil {
		t.Fatal(err)
	}
	stdin, err := command.StdinPipe()
	if err != nil {
		t.Fatal(err)
	}
	var stderr bytes.Buffer
	command.Stderr = &stderr
	if err := command.Start(); err != nil {
		t.Fatal(err)
	}
	transport := &mcp.IOTransport{Reader: testReadCloser{Reader: stdout, Closer: stdout}, Writer: stdin}
	client := mcp.NewClient(&mcp.Implementation{Name: "compact-e2e", Version: "test"}, nil)
	session, err := client.Connect(ctx, transport, nil)
	if err != nil {
		t.Fatalf("Connect() error=%v stderr=%s", err, stderr.String())
	}

	// When
	tools, err := session.ListTools(ctx, nil)
	if err != nil {
		t.Fatal(err)
	}
	search, err := session.CallTool(ctx, &mcp.CallToolParams{Name: "tool_search", Arguments: map[string]any{"query": "dynamic probe"}})
	if err != nil {
		t.Fatal(err)
	}
	call, err := session.CallTool(ctx, &mcp.CallToolParams{Name: "tool_call", Arguments: map[string]any{
		"name": "dynamic_probe", "arguments": map[string]any{"action": "inspect"},
	}})
	if err != nil {
		t.Fatal(err)
	}

	// Then
	want := []string{"tool_call", "tool_describe", "tool_search"}
	got := make([]string, len(tools.Tools))
	for index, tool := range tools.Tools {
		got[index] = tool.Name
	}
	if !slices.Equal(got, want) {
		t.Fatalf("tools=%v, want %v", got, want)
	}
	if search.IsError || call.IsError {
		t.Fatalf("search=%#v call=%#v", search, call)
	}
	envelope, ok := search.StructuredContent.(map[string]any)
	if !ok {
		t.Fatalf("search content=%#v", search.StructuredContent)
	}
	results, ok := envelope["data"].([]any)
	if !ok || len(results) == 0 || results[0].(map[string]any)["name"] != "dynamic_probe" {
		t.Fatalf("search data=%#v", envelope["data"])
	}
	if err := session.Close(); err != nil {
		t.Fatal(err)
	}
	if err := command.Wait(); err != nil {
		t.Fatalf("process error=%v stderr=%s", err, stderr.String())
	}
}

func TestMCPProcessGracefulEOFAfterDiscovery(t *testing.T) {
	// Given
	port, home := startMCPUnityFixture(t)
	ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
	defer cancel()
	command := mcpHelperCommand(ctx, fmt.Sprintf("--port %d mcp --transport stdio --profile core", port))
	command.Env = append(command.Env, "HOME="+home, "USERPROFILE="+home)
	command.Stdin = strings.NewReader(`{"jsonrpc":"2.0","id":1,"method":"server/discover","params":{"_meta":{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientInfo":{"name":"eof-test","version":"test"},"io.modelcontextprotocol/clientCapabilities":{}}}}` + "\n")
	var stdout bytes.Buffer
	var stderr bytes.Buffer
	command.Stdout = &stdout
	command.Stderr = &stderr

	// When
	err := command.Run()

	// Then
	if err != nil {
		t.Fatalf("process error = %v; stderr=%s", err, stderr.String())
	}
	if stdout.Len() != 0 {
		assertProtocolOnly(t, stdout.Bytes())
	}
}
