package cmd

import (
	"os"

	"github.com/mattn/go-isatty"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

func approvalTTY(input, output *os.File) bool {
	return (isatty.IsTerminal(input.Fd()) || isatty.IsCygwinTerminal(input.Fd())) &&
		(isatty.IsTerminal(output.Fd()) || isatty.IsCygwinTerminal(output.Fd()))
}

// approvalConfirmer answers an approval preflight. With --yes
// (HERA_AGENT_APPROVE) the operator pre-approved this invocation, so only the
// terminal question is skipped: the preflight still runs and the Connector
// still binds and records the operation.
func approvalConfirmer(autoApprove bool) func(client.ApprovalSummary) (bool, error) {
	if autoApprove {
		return func(client.ApprovalSummary) (bool, error) { return true, nil }
	}
	return func(summary client.ApprovalSummary) (bool, error) {
		return promptCallApproval(os.Stdin, os.Stderr, summary)
	}
}
