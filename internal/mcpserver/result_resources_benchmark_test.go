package mcpserver

import (
	"encoding/json"
	"fmt"
	"strings"
	"testing"
	"time"

	"github.com/NotNull92/hera-agent-unity/internal/client"
	"github.com/NotNull92/hera-agent-unity/internal/resultstore"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
	"github.com/modelcontextprotocol/go-sdk/mcp"
)

var boundedResultBenchmarkSink *mcp.CallToolResult

func BenchmarkBoundedCommandResult(b *testing.B) {
	benchmarkBoundedCommandResult(b, func(size int) int { return size * 4 })
}

func BenchmarkBoundedCommandResultSpooling(b *testing.B) {
	benchmarkBoundedCommandResult(b, func(int) int { return DefaultMaxInlineBytes })
}

func benchmarkBoundedCommandResult(b *testing.B, inlineLimit func(int) int) {
	for _, size := range []int{32 << 10, 1 << 20, 10 << 20} {
		b.Run(fmt.Sprintf("%dKB", size>>10), func(b *testing.B) {
			store, err := resultstore.New(mcpTestProjectID, resultstore.Options{
				Root: b.TempDir(), MaxBytes: 64 << 20, Retention: time.Hour,
			})
			if err != nil {
				b.Fatal(err)
			}
			runtime := nativeRuntime{results: store, maxInlineBytes: inlineLimit(size)}
			response := &client.CommandResponse{
				Success: true,
				Message: "OK",
				Data:    json.RawMessage(`{"payload":"` + strings.Repeat("x", size) + `"}`),
			}
			invocation := toolInvocation{
				tool: toolregistry.Tool{
					Name: "probe",
					Safety: toolregistry.Safety{
						RiskClass: "read_only", ReadOnly: true, Idempotent: true,
					},
				},
				operationID: client.OperationID("op_benchmark_result"),
			}

			b.ReportAllocs()
			b.SetBytes(int64(size))
			b.ResetTimer()
			for index := 0; index < b.N; index++ {
				boundedResultBenchmarkSink = boundedCommandResult(runtime, invocation, response)
			}
		})
	}
}
