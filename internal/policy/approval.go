package policy

import (
	"encoding/base64"
	"encoding/json"
	"fmt"
	"strings"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

type ApprovalClaims struct {
	Version       int                `json:"version"`
	OperationID   client.OperationID `json:"operation_id"`
	Tool          string             `json:"tool"`
	Action        string             `json:"action,omitempty"`
	ArgumentsHash string             `json:"arguments_hash"`
	RiskClass     string             `json:"risk_class"`
	ProjectID     string             `json:"project_id"`
	ExpiresAtMS   int64              `json:"expires_at_ms"`
	SingleUse     bool               `json:"single_use"`
}

func InspectApprovalToken(token string) (ApprovalClaims, error) {
	encoded, _, ok := strings.Cut(token, ".")
	if !ok || encoded == "" {
		return ApprovalClaims{}, fmt.Errorf("approval token has an invalid envelope")
	}
	payload, err := base64.RawURLEncoding.DecodeString(encoded)
	if err != nil {
		return ApprovalClaims{}, fmt.Errorf("decode approval token payload: %w", err)
	}
	var claims ApprovalClaims
	if err := json.Unmarshal(payload, &claims); err != nil {
		return ApprovalClaims{}, fmt.Errorf("decode approval token claims: %w", err)
	}
	if claims.Version != 1 || claims.OperationID == "" || claims.Tool == "" || !claims.SingleUse {
		return ApprovalClaims{}, fmt.Errorf("approval token claims are incomplete")
	}
	return claims, nil
}
