# hera-agent-unity

Install CLI **v0.3.0**, the low-token command-line client that lets AI coding agents control and verify a live Unity Editor:

```bash
npm install --global hera-agent-unity
hera-agent-unity version
hera-agent-unity doctor --json
```

The npm wrapper downloads the matching native binary from the GitHub release whose tag equals the npm package version. The supported native targets are Linux amd64/arm64, macOS amd64/arm64, and Windows amd64.

## v0.3.0 highlights

- A failed request declares how it can be retried (`retryable`, `state`, `retry_after_ms`) instead of leaving the client to infer recovery from a dropped connection.
- The heartbeat advertises the catalog schema hash, so a matching catalog is reused without another round-trip.
- Breaking: the experimental MCP adapter was removed. There is no `mcp` command and no `HERA_MCP_*` environment variable; the CLI is the only agent surface.

Connector **0.2.0** was verified on a disposable Unity `6000.5.6f1` project: the package compiles with **0** Console errors, the `ReleaseGateTests` suite is green, and the live catalog exposes **34 tools / 133 actions**. The `6000.0`-`6000.2` and `6000.3`-`6000.4` buckets were not run for this release.

Add the Unity Connector through Package Manager using the latest source:

```text
https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector
```

Or pin the released Connector **0.2.0** independently:

```text
https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector#connector-0.2.0
```

CLI and Connector versions are intentionally separate. Documentation and source: <https://github.com/NotNull92/hera-agent-unity>

## License

Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).
