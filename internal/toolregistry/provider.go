package toolregistry

import (
	"context"
	"fmt"

	"github.com/NotNull92/hera-agent-unity/internal/client"
)

const defaultCatalogTimeoutMs = 30_000

type Sender interface {
	Send(
		context.Context,
		*client.Instance,
		string,
		any,
		int,
	) (*client.CommandResponse, error)
}

type Provider interface {
	Load(context.Context, *client.Instance) (*Snapshot, error)
}

type ProviderError struct {
	Code    string
	Message string
}

func (err *ProviderError) Error() string {
	if err.Code == "" {
		return err.Message
	}
	return fmt.Sprintf("%s: %s", err.Code, err.Message)
}

func responseData(response *client.CommandResponse, operation string) ([]byte, error) {
	if response == nil {
		return nil, fmt.Errorf("%s returned no response", operation)
	}
	if !response.Success {
		return nil, &ProviderError{Code: response.Code, Message: response.Message}
	}
	if len(response.Data) == 0 {
		return nil, fmt.Errorf("%s returned no data", operation)
	}
	return response.Data, nil
}
