package main

import (
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"os"

	"github.com/NotNull92/hera-agent-unity/internal/schema"
	"github.com/NotNull92/hera-agent-unity/internal/toolregistry"
)

type validationSummary struct {
	SchemaVersion string `json:"schema_version"`
	CatalogHash   string `json:"catalog_hash"`
	Tools         int    `json:"tools"`
	Actions       int    `json:"actions"`
	Strict        int    `json:"strict"`
}

func main() {
	if err := run(os.Args[1:], os.Stdin, os.Stdout); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}

func run(args []string, stdin io.Reader, stdout io.Writer) error {
	flags := flag.NewFlagSet("validate-tool-catalog", flag.ContinueOnError)
	flags.SetOutput(io.Discard)
	filePath := flags.String("file", "", "catalog JSON file; stdin when omitted")
	if err := flags.Parse(args); err != nil {
		return fmt.Errorf("parse arguments: %w", err)
	}
	if flags.NArg() != 0 {
		return fmt.Errorf("unexpected positional arguments")
	}

	data, err := readInput(*filePath, stdin)
	if err != nil {
		return err
	}
	catalog, err := toolregistry.ParseCatalog(data)
	if err != nil {
		return err
	}
	if _, err := schema.NewCompilerCache().Compile(
		catalog.CatalogHash,
		catalog.SchemaDefinitions(),
	); err != nil {
		return fmt.Errorf("compile catalog schemas: %w", err)
	}

	summary := validationSummary{
		SchemaVersion: catalog.SchemaVersion,
		CatalogHash:   catalog.CatalogHash,
		Tools:         len(catalog.Tools),
	}
	for _, tool := range catalog.Tools {
		summary.Actions += len(tool.Actions)
		if tool.ContractMode == toolregistry.ContractStrict {
			summary.Strict++
		}
	}
	if err := json.NewEncoder(stdout).Encode(summary); err != nil {
		return fmt.Errorf("write validation summary: %w", err)
	}
	return nil
}

func readInput(filePath string, stdin io.Reader) ([]byte, error) {
	if filePath == "" {
		data, err := io.ReadAll(stdin)
		if err != nil {
			return nil, fmt.Errorf("read catalog stdin: %w", err)
		}
		return data, nil
	}
	data, err := os.ReadFile(filePath)
	if err != nil {
		return nil, fmt.Errorf("read catalog file: %w", err)
	}
	return data, nil
}
