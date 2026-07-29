package client

import (
	"context"
	"fmt"
	"net"
	"time"
)

func IsPortReachable(ctx context.Context, port int) bool {
	dialer := net.Dialer{Timeout: 500 * time.Millisecond}
	connection, err := dialer.DialContext(ctx, "tcp", fmt.Sprintf("127.0.0.1:%d", port))
	if err != nil {
		return false
	}
	return connection.Close() == nil
}
