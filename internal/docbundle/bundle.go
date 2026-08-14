package docbundle

import (
	"bufio"
	"compress/gzip"
	"fmt"
	"os"
)

// Build validates non-empty JSONL lines and writes them unchanged to a gzip bundle.
func Build(
	inputPath string,
	outputPath string,
	validate func([]byte, int) error,
) (int, int64, error) {
	lines, err := readLines(inputPath, validate)
	if err != nil {
		return 0, 0, err
	}
	size, err := writeLines(outputPath, lines)
	if err != nil {
		return 0, 0, err
	}
	return len(lines), size, nil
}

func readLines(inputPath string, validate func([]byte, int) error) ([][]byte, error) {
	source, err := os.Open(inputPath)
	if err != nil {
		return nil, fmt.Errorf("open %s: %w", inputPath, err)
	}
	defer source.Close()

	var lines [][]byte
	scanner := bufio.NewScanner(source)
	scanner.Buffer(make([]byte, 0, 1024*1024), 1024*1024)
	lineNo := 0
	for scanner.Scan() {
		lineNo++
		line := scanner.Bytes()
		if len(line) == 0 {
			continue
		}
		if err := validate(line, lineNo); err != nil {
			return nil, fmt.Errorf("%s:%d: %w", inputPath, lineNo, err)
		}
		lines = append(lines, append([]byte(nil), line...))
	}
	if err := scanner.Err(); err != nil {
		return nil, fmt.Errorf("read %s: %w", inputPath, err)
	}
	if len(lines) == 0 {
		return nil, fmt.Errorf("%s: no entries", inputPath)
	}
	return lines, nil
}

func writeLines(outputPath string, lines [][]byte) (int64, error) {
	destination, err := os.Create(outputPath)
	if err != nil {
		return 0, fmt.Errorf("create %s: %w", outputPath, err)
	}
	defer destination.Close()

	writer, err := gzip.NewWriterLevel(destination, gzip.BestCompression)
	if err != nil {
		return 0, fmt.Errorf("gzip: %w", err)
	}
	for _, line := range lines {
		if _, err := writer.Write(append(line, '\n')); err != nil {
			return 0, fmt.Errorf("write: %w", err)
		}
	}
	if err := writer.Close(); err != nil {
		return 0, fmt.Errorf("close gzip: %w", err)
	}
	if err := destination.Close(); err != nil {
		return 0, fmt.Errorf("close %s: %w", outputPath, err)
	}

	info, err := os.Stat(outputPath)
	if err != nil {
		return 0, fmt.Errorf("stat %s: %w", outputPath, err)
	}
	return info.Size(), nil
}
