package client

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"time"
)

type ApprovalSummary struct {
	Tool            string      `json:"tool"`
	Action          string      `json:"action,omitempty"`
	Target          string      `json:"target"`
	SideEffect      string      `json:"side_effect"`
	Reversible      bool        `json:"reversible"`
	MayReloadDomain bool        `json:"may_reload_domain"`
	ExternalImpact  bool        `json:"external_impact"`
	OperationID     OperationID `json:"operation_id"`
}

type ApprovalPreflight struct {
	Token       string          `json:"token"`
	OperationID OperationID     `json:"operation_id"`
	ExpiresAtMS int64           `json:"expires_at_ms"`
	Summary     ApprovalSummary `json:"summary"`
}

type ApprovalPreflightRequest struct {
	Command     string
	Action      string
	Params      any
	OperationID OperationID
	TimeoutMS   int
}

type approvalPreflightWireRequest struct {
	OperationID OperationID `json:"operation_id"`
	Tool        string      `json:"tool"`
	Action      string      `json:"action,omitempty"`
	Arguments   any         `json:"arguments"`
}

func (c *Client) PreflightApproval(
	ctx context.Context,
	instance *Instance,
	request ApprovalPreflightRequest,
) (*ApprovalPreflight, error) {
	operationID := request.OperationID
	if operationID == "" {
		var err error
		operationID, err = NewOperationID()
		if err != nil {
			return nil, err
		}
	}
	body, err := json.Marshal(approvalPreflightWireRequest{
		OperationID: operationID,
		Tool:        request.Command,
		Action:      request.Action,
		Arguments:   request.Params,
	})
	if err != nil {
		return nil, fmt.Errorf("marshal approval preflight: %w", err)
	}
	if request.TimeoutMS > 0 {
		var cancel context.CancelFunc
		ctx, cancel = context.WithTimeout(ctx, time.Duration(request.TimeoutMS)*time.Millisecond)
		defer cancel()
	}
	start := time.Now()
	response, err := c.doWithReloadRetry(ctx, body, instance, "/approval/preflight", retryPolicy{allowRetry: true})
	if err != nil {
		return nil, fmt.Errorf("request approval preflight: %w", err)
	}
	responseBody, statusCode, err := c.processHTTPResponse(response, "approval preflight", start)
	if err != nil {
		return nil, err
	}
	var envelope CommandResponse
	if err := json.Unmarshal(responseBody, &envelope); err != nil {
		return nil, fmt.Errorf("decode approval preflight envelope: %w", err)
	}
	if statusCode != http.StatusOK || !envelope.Success {
		return nil, fmt.Errorf("approval preflight rejected: %s: %s", envelope.Code, envelope.Message)
	}
	var preflight ApprovalPreflight
	if err := json.Unmarshal(envelope.Data, &preflight); err != nil {
		return nil, fmt.Errorf("decode approval preflight data: %w", err)
	}
	if preflight.Token == "" || preflight.OperationID == "" {
		return nil, fmt.Errorf("approval preflight returned incomplete data")
	}
	return &preflight, nil
}
