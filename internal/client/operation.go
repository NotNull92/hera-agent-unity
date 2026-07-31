package client

import (
	"bytes"
	"crypto/rand"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"fmt"
)

const FeatureOperationLedgerV1 = "operation_ledger_v1"

type OperationID string

func NewOperationID() (OperationID, error) {
	var value [16]byte
	if _, err := rand.Read(value[:]); err != nil {
		return "", fmt.Errorf("generate operation id: %w", err)
	}
	return OperationID("op_" + hex.EncodeToString(value[:])), nil
}

type RequestMeta struct {
	OperationID   OperationID `json:"operation_id"`
	ArgumentsHash string      `json:"arguments_hash"`
	ApprovalToken *string     `json:"approval_token"`
	ClientKind    string      `json:"client_kind"`
	CatalogHash   string      `json:"catalog_hash,omitempty"`
}

type SendOptions struct {
	OperationID OperationID
	Idempotent  bool
	ClientKind  string
	CatalogHash string
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

func hasFeature(instance *Instance, feature string) bool {
	for _, candidate := range instance.Features {
		if candidate == feature {
			return true
		}
	}
	return false
}
