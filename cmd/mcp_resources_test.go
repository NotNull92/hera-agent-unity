package cmd

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"strings"
	"testing"
	"time"

	"github.com/modelcontextprotocol/go-sdk/mcp"
)

func TestMCPProcessReadsOversizedResultResource(t *testing.T) {
	port, home := startMCPUnityFixture(t)
	ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
	defer cancel()
	command := mcpHelperCommand(ctx, fmt.Sprintf("--port %d mcp --transport stdio --profile core", port))
	command.Env = append(command.Env,
		"HOME="+home, "USERPROFILE="+home, "HERA_MCP_MAX_INLINE_BYTES=32",
	)
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
	transport := &mcp.IOTransport{
		Reader: testReadCloser{Reader: stdout, Closer: stdout}, Writer: stdin,
	}
	client := mcp.NewClient(&mcp.Implementation{Name: "resource-smoke", Version: "test"}, nil)
	session, err := client.Connect(ctx, transport, nil)
	if err != nil {
		t.Fatalf("Connect() error=%v stderr=%s", err, stderr.String())
	}

	templates, err := session.ListResourceTemplates(ctx, nil)
	if err != nil {
		t.Fatal(err)
	}
	if len(templates.ResourceTemplates) != 1 || templates.ResourceTemplates[0].MIMEType != "application/json" {
		t.Fatalf("resource templates = %#v", templates.ResourceTemplates)
	}
	call, err := session.CallTool(ctx, &mcp.CallToolParams{
		Name: "scene", Arguments: map[string]any{"action": "info"},
	})
	if err != nil {
		t.Fatal(err)
	}
	encoded, err := mcpJSON(call)
	if err != nil {
		t.Fatal(err)
	}
	if strings.Contains(encoded, "M9Fixture") {
		t.Fatal("oversized Unity payload remained in inline tool content")
	}
	structured := call.StructuredContent.(map[string]any)
	if structured["code"] != "RESULT_SPOOLED" {
		t.Fatalf("resource result code = %#v", structured["code"])
	}
	resource := structured["resource"].(map[string]any)
	uri := resource["uri"].(string)
	read, err := session.ReadResource(ctx, &mcp.ReadResourceParams{URI: uri})
	if err != nil {
		t.Fatal(err)
	}
	if len(read.Contents) != 1 || !strings.Contains(read.Contents[0].Text, "M9Fixture") {
		t.Fatalf("retrieved resource = %#v", read.Contents)
	}
	if err := session.Close(); err != nil {
		t.Fatal(err)
	}
	if err := command.Wait(); err != nil {
		t.Fatalf("process error=%v stderr=%s", err, stderr.String())
	}
}

func TestMCPMaxInlineBytesRejectsExplicitZero(t *testing.T) {
	t.Setenv("HERA_MCP_MAX_INLINE_BYTES", "0")
	if _, err := mcpMaxInlineBytes(); err == nil {
		t.Fatal("explicit zero inline byte cap was silently accepted")
	}
}

func mcpJSON(value any) (string, error) {
	data, err := json.Marshal(value)
	return string(data), err
}
