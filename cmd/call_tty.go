package cmd

import (
	"os"

	"github.com/mattn/go-isatty"
)

func approvalTTY(input, output *os.File) bool {
	return (isatty.IsTerminal(input.Fd()) || isatty.IsCygwinTerminal(input.Fd())) &&
		(isatty.IsTerminal(output.Fd()) || isatty.IsCygwinTerminal(output.Fd()))
}
