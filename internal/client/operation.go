package client

import (
	"bytes"
	"crypto/rand"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"unicode"
	"unicode/utf16"
)

const (
	FeatureApprovalV1        = "approval_v1"
	FeatureOperationLedgerV1 = "operation_ledger_v1"
)

var ErrInvalidOperationID = errors.New("invalid operation id")

type OperationID string

func NewOperationID() (OperationID, error) {
	var value [16]byte
	if _, err := rand.Read(value[:]); err != nil {
		return "", fmt.Errorf("generate operation id: %w", err)
	}
	return OperationID("op_" + hex.EncodeToString(value[:])), nil
}

func ParseOperationID(value string) (OperationID, error) {
	runes := []rune(value)
	length := len(utf16.Encode(runes))
	if length < 8 || length > 128 {
		return "", fmt.Errorf("%w: must be 8-128 characters", ErrInvalidOperationID)
	}
	for _, character := range runes {
		if !unicode.IsLetter(character) && !unicode.IsDigit(character) && character != '_' && character != '-' {
			return "", fmt.Errorf("%w: contains unsupported character", ErrInvalidOperationID)
		}
	}
	return OperationID(value), nil
}

type RequestMeta struct {
	OperationID   OperationID `json:"operation_id"`
	ArgumentsHash string      `json:"arguments_hash"`
	ApprovalToken *string     `json:"approval_token"`
	ClientKind    string      `json:"client_kind"`
	CatalogHash   string      `json:"catalog_hash,omitempty"`
}

type SendOptions struct {
	OperationID   OperationID
	ApprovalToken string
	Idempotent    bool
	ClientKind    string
	CatalogHash   string
}

type OperationOutcomeUnknownError struct {
	Code        string
	OperationID OperationID
	Command     string
	Cause       error
}

func (err *OperationOutcomeUnknownError) Error() string {
	return fmt.Sprintf(
		"%s: outcome of %s operation %s is unknown; it was not retried",
		err.Code,
		err.Command,
		err.OperationID,
	)
}

func (err *OperationOutcomeUnknownError) Unwrap() error {
	return err.Cause
}

func argumentsHash(params any) (string, error) {
	var encoded bytes.Buffer
	encoder := json.NewEncoder(&encoded)
	encoder.SetEscapeHTML(false)
	if err := encoder.Encode(params); err != nil {
		return "", fmt.Errorf("marshal arguments for hash: %w", err)
	}
	digest := sha256.Sum256(bytes.TrimSpace(encoded.Bytes()))
	return "sha256:" + hex.EncodeToString(digest[:]), nil
}

func ArgumentsHash(params any) (string, error) { return argumentsHash(params) }

func hasFeature(instance *Instance, feature string) bool {
	for _, candidate := range instance.Features {
		if candidate == feature {
			return true
		}
	}
	return false
}
