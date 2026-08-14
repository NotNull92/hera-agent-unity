package cmd

import (
	"io"
	"os"
	"strings"
)

type stdinReader interface {
	io.Reader
	Stat() (os.FileInfo, error)
}

func hasStdinData(input stdinReader) bool {
	info, err := input.Stat()
	if err != nil {
		return false
	}
	mode := info.Mode()
	return mode&os.ModeCharDevice == 0 &&
		(mode&os.ModeNamedPipe != 0 || mode.IsRegular())
}

func readStdinIfPiped(args []string) []string {
	_, positional, _ := buildParams(args, nil)
	if len(positional) > 0 || !hasStdinData(os.Stdin) {
		return args
	}
	data, err := io.ReadAll(os.Stdin)
	if err != nil || len(data) == 0 {
		return args
	}
	code := strings.TrimRight(string(data), "\n\r")
	return append([]string{code}, args...)
}
