# hera-agent-unity

Install CLI **v0.2.0**, the low-token command-line client that lets AI coding agents control and verify a live Unity Editor:

```bash
npm install --global hera-agent-unity
hera-agent-unity version
hera-agent-unity doctor --json
```

The npm wrapper downloads the matching native binary from the GitHub release whose tag equals the npm package version. The supported native targets are Linux amd64/arm64, macOS amd64/arm64, and Windows amd64.

The package also verifies ownership of the official MCP Registry entry
`io.github.NotNull92/hera-agent-unity`. Registry clients launch the experimental,
default-off local adapter with `HERA_MCP_ENABLED=1` and the documented core
stdio profile.

Add the Unity Connector through Package Manager using the latest source:

```text
https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector
```

Or pin the released Connector **0.0.86** independently:

```text
https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector#connector-0.0.86
```

CLI and Connector versions are intentionally separate. Documentation and source: <https://github.com/NotNull92/hera-agent-unity>

## License

Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).
