package client

import (
	"encoding/json"
	"net/http"
	"os"
	"time"
)

type Client struct {
	Debug              bool
	httpClient         *http.Client
	cache              *InstanceCache
	processDeadChecker func(int) bool
	readFile           func(string) ([]byte, error)
	sleep              func(time.Duration)
}

func NewClient() *Client {
	return &Client{
		httpClient:         sharedHTTPClient,
		cache:              NewInstanceCache(),
		processDeadChecker: checkProcessDead,
		readFile:           os.ReadFile,
		sleep:              time.Sleep,
	}
}

var DefaultClient = NewClient()

type Instance struct {
	State         string        `json:"state"`
	ProjectPath   string        `json:"projectPath"`
	Port          int           `json:"port"`
	PID           int           `json:"pid"`
	UnityVersion  string        `json:"unityVersion,omitempty"`
	DocsVersion   string        `json:"docsVersion,omitempty"`
	Compiler      *CompilerInfo `json:"compiler,omitempty"`
	DomainEpoch   string        `json:"domainEpoch,omitempty"`
	Features      []string      `json:"features,omitempty"`
	Timestamp     int64         `json:"timestamp,omitempty"`
	CompileErrors bool          `json:"compileErrors,omitempty"`
}

type CompilerInfo struct {
	CscPath     string `json:"cscPath,omitempty"`
	CscKind     string `json:"cscKind,omitempty"`
	CscFound    bool   `json:"cscFound,omitempty"`
	DotnetPath  string `json:"dotnetPath,omitempty"`
	DotnetKind  string `json:"dotnetKind,omitempty"`
	DotnetFound bool   `json:"dotnetFound,omitempty"`
	Error       string `json:"error,omitempty"`
}

type CommandRequest struct {
	Command string      `json:"command"`
	Params  any         `json:"params"`
	Meta    RequestMeta `json:"meta"`
}

type BatchCommandItem struct {
	Command string `json:"command"`
	Params  any    `json:"params,omitempty"`
}

type BatchOptions struct {
	FailFast bool `json:"fail_fast"`
	Atomic   bool `json:"atomic"`
}

type BatchCommandRequest struct {
	Commands []BatchCommandItem `json:"commands"`
	Options  BatchOptions       `json:"options"`
}

type BatchCommandResponse struct {
	Success   bool              `json:"success"`
	Message   string            `json:"message"`
	Code      string            `json:"code,omitempty"`
	Data      json.RawMessage   `json:"data,omitempty"`
	Results   []CommandResponse `json:"results"`
	Completed int               `json:"completed"`
	Failed    int               `json:"failed"`
}

type CommandResponse struct {
	Success     bool             `json:"success"`
	Message     string           `json:"message"`
	Code        string           `json:"code,omitempty"`
	Suggestions []string         `json:"suggestions,omitempty"`
	AgentHint   string           `json:"agent_hint,omitempty"`
	Data        json.RawMessage  `json:"data,omitempty"`
	Timings     map[string]int64 `json:"timings,omitempty"`
}
