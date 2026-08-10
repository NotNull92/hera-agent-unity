# hera-agent-unity

Install CLI **v0.2.0**, the low-token command-line client that lets AI coding agents control and verify a live Unity Editor:

```bash
npm install --global hera-agent-unity
hera-agent-unity version
hera-agent-unity doctor --json
```

The npm wrapper downloads the matching native binary from the GitHub release whose tag equals the npm package version. The supported native targets are Linux amd64/arm64, macOS amd64/arm64, and Windows amd64.

## v0.2.0 highlights

- Launch or restart the exact Unity project from its recorded Editor version.
- Drive optional Input System keyboard/mouse sequences in Play Mode, record bounded input, and replay it.
- Capture bounded uGUI identity/coordinates and Camera.main-constrained 3D physics evidence.
- Use opt-in restricted `exec` when project code only needs a constrained platform API surface.

Connector **0.0.86** passed the five representative Unity compile buckets and a live Unity 6000.5.6f1 regression with **31 tools / 80 actions**, `ReleaseGateTests` **18/18 PASS**, and **0** Console errors.

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

OpenUPM also reports **0.0.86** as the current `latest` Connector release. CLI and Connector versions are intentionally separate. Documentation and source: <https://github.com/NotNull92/hera-agent-unity>

## License

Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).
