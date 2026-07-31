package schema

import "fmt"

// ValidationError identifies the schema and JSON Pointer that rejected a value.
type ValidationError struct {
	Key     string
	Pointer string
	Cause   error
}

func (err *ValidationError) Error() string {
	return fmt.Sprintf("schema %q rejected %s: %v", err.Key, err.Pointer, err.Cause)
}

func (err *ValidationError) Unwrap() error {
	return err.Cause
}
