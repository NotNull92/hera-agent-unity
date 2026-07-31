package cmd

import (
	"bytes"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"os"
)

type callInput struct {
	Reader io.Reader
	Piped  bool
}

type callStdin interface {
	io.Reader
	Stat() (os.FileInfo, error)
}

type callOptions struct {
	Tool         string
	JSON         optionalString
	File         optionalString
	Profile      string
	OperationID  string
	ValidateOnly bool
	Explain      bool
}

type optionalString struct {
	value string
	set   bool
}

func (value *optionalString) String() string {
	return value.value
}

func (value *optionalString) Set(raw string) error {
	value.value = raw
	value.set = true
	return nil
}

func detectCallInput(stdin callStdin) callInput {
	info, err := stdin.Stat()
	if err != nil {
		return callInput{}
	}
	mode := info.Mode()
	piped := mode&os.ModeCharDevice == 0 &&
		(mode&os.ModeNamedPipe != 0 || mode.IsRegular())
	return callInput{Reader: stdin, Piped: piped}
}

func parseCallOptions(args []string) (callOptions, error) {
	if len(args) == 0 {
		return callOptions{}, fmt.Errorf("usage: hera-agent-unity call <tool> [--json <object>|--file <path>]")
	}
	options := callOptions{Tool: args[0]}
	flags := flag.NewFlagSet("call", flag.ContinueOnError)
	flags.SetOutput(io.Discard)
	flags.Var(&options.JSON, "json", "JSON request object")
	flags.Var(&options.File, "file", "path to a JSON request object")
	flags.StringVar(&options.Profile, "profile", "", "require tool membership in profile")
	flags.StringVar(&options.OperationID, "operation-id", "", "reuse an existing operation ID")
	flags.BoolVar(&options.ValidateOnly, "validate-only", false, "validate without executing")
	flags.BoolVar(&options.Explain, "explain", false, "explain validation and safety without executing")
	if err := flags.Parse(args[1:]); err != nil {
		return callOptions{}, fmt.Errorf("parse call flags: %w", err)
	}
	if flags.NArg() != 0 {
		return callOptions{}, fmt.Errorf("unexpected call argument %q", flags.Arg(0))
	}
	return options, nil
}

func (options callOptions) readParams(input callInput) (map[string]any, error) {
	sources := 0
	if options.JSON.set {
		sources++
	}
	if options.File.set {
		sources++
	}
	if input.Piped {
		sources++
	}
	if sources > 1 {
		return nil, fmt.Errorf("multiple input sources: use exactly one of --json, stdin, or --file")
	}

	data := []byte(`{}`)
	switch {
	case options.JSON.set:
		data = []byte(options.JSON.value)
	case input.Piped:
		if input.Reader == nil {
			return nil, fmt.Errorf("read call stdin: no reader")
		}
		read, err := io.ReadAll(input.Reader)
		if err != nil {
			return nil, fmt.Errorf("read call stdin: %w", err)
		}
		data = read
	case options.File.set:
		read, err := os.ReadFile(options.File.value)
		if err != nil {
			return nil, fmt.Errorf("read call file %s: %w", options.File.value, err)
		}
		data = read
	}
	return decodeCallObject(data)
}

func decodeCallObject(data []byte) (map[string]any, error) {
	decoder := json.NewDecoder(bytes.NewReader(data))
	decoder.UseNumber()
	var params map[string]any
	if err := decoder.Decode(&params); err != nil {
		return nil, fmt.Errorf("decode call input: %w", err)
	}
	if params == nil {
		return nil, fmt.Errorf("call input must be a JSON object")
	}
	var trailing any
	if err := decoder.Decode(&trailing); err != io.EOF {
		if err == nil {
			return nil, fmt.Errorf("decode call input: multiple JSON values")
		}
		return nil, fmt.Errorf("decode call input: %w", err)
	}
	return params, nil
}
