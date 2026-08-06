# Distribution Channels

## Current release baseline

External state was last checked on 2026-08-06.

```text
CLI release:          v0.1.4
npm package:          0.1.4 (latest)
UPM Connector:        0.0.80
Codex plugin bundle:  1.0.1
Release commit:       2e73316717709a5c550d708dae53e532dc8cb347
Distribution commit:  2e73316717709a5c550d708dae53e532dc8cb347
Connector tag:        connector-0.0.80
CLI tag:              v0.1.4
```

Release evidence:

- [main CI run 31069934044](https://github.com/NotNull92/hera-agent-unity/actions/runs/31069934044)
- [GitHub Release run 31069980864](https://github.com/NotNull92/hera-agent-unity/actions/runs/31069980864)
- [npm 0.1.4 run 31070049824](https://github.com/NotNull92/hera-agent-unity/actions/runs/31070049824)
- [MCP Registry 0.1.4 run 31070065379](https://github.com/NotNull92/hera-agent-unity/actions/runs/31070065379)

CLI, Connector, npm, and plugin versions are intentionally independent.

## Channel matrix

| Channel | External state | Repository state | Next action |
|---|---|---|---|
| GitHub Release binaries | `v0.1.4` published with five native assets | Complete | None |
| PowerShell / shell installers | Resolve GitHub `releases/latest` | Complete and follows `v0.1.4` | None |
| Go install | Resolves the latest module tag | Complete through `v0.1.4` | None |
| Unity UPM Git package | `connector-0.0.80` tag published | Complete | None |
| OpenUPM | Registry `latest` is `0.0.80` | Complete | Keep Connector package metadata compatible with OpenUPM |
| npm | Registry `latest` is `0.1.4` | Complete; automatic workflow-run publication is active | None |
| HOL awesome-codex-plugins | Hera Agent Unity is listed; source scanner passed for `5cf8b23` | Plugin `1.0.1` is scanner-gated | Upstream mirror refresh remains upstream-owned |
| HOL awesome-ai-plugins | Hera Agent Unity is listed | Uses the same upstream bundle | No separate package publication |
| Publisher-owned Codex marketplace | `main` exposes `.agents/plugins/marketplace.json` | Complete | Keep the catalog entry and plugin bundle in sync |
| Skills CLI / skills.sh | Repository-backed skill is installable | `.agents/skills/hera-agent-unity/SKILL.md` is the canonical source | Directory visibility remains telemetry-driven |
| Official MCP Registry | `io.github.NotNull92/hera-agent-unity` version `0.1.4` is active and latest | Complete; npm stdio launch metadata verified through the public API | Monitor preview Registry compatibility |
| GitHub MCP Registry | Hera is not listed | Candidate after official MCP Registry publication | Request curated inclusion after the official entry is live |
| Glama | No Hera-specific entry verified | Can ingest the official MCP Registry or a GitHub submission | Prefer official Registry ingestion; do not market Hera as a remote hosted server |
| Smithery | No Hera entry verified | Local stdio publication requires an MCPB bundle | Add an MCPB release artifact only if this extra packaging surface is worth maintaining |
| awesome-mcp-servers | No Hera entry found | Manual community PR path is available | Submit after official registry metadata and install instructions are stable |
| OpenAI curated plugin directory | Hera is not listed; no general self-serve publish path was verified | Plugin bundle satisfies the standard repository shape | Treat as a curated-submission candidate, not an automated release channel |

## npm publication

The npm package is public and owned by `notnull92`. The repository does not
store an npm token. GitHub Actions publishes through npm trusted publishing and
OIDC using `.github/workflows/npm-publish.yml`.

Trusted publisher configuration:

```text
Publisher:         GitHub Actions
Organization/user: NotNull92
Repository:        hera-agent-unity
Workflow filename: npm-publish.yml
Allowed action:    npm publish
```

Successful npm publications include
[0.1.1 run 30858410191](https://github.com/NotNull92/hera-agent-unity/actions/runs/30858410191),
[0.1.2 run 30862729736](https://github.com/NotNull92/hera-agent-unity/actions/runs/30862729736),
[0.1.3 run 30863071866](https://github.com/NotNull92/hera-agent-unity/actions/runs/30863071866),
and the automatic
[0.1.4 run 31070049824](https://github.com/NotNull92/hera-agent-unity/actions/runs/31070049824).
The workflow fails before publishing unless:

- `npm/package.json` equals the requested `v*` tag;
- the GitHub release is stable;
- all five native assets exist;
- npm tests and `npm pack --dry-run` pass;
- the version is not already present on npm.

`.github/workflows/mcp-publish.yml` starts only after **Publish npm Package**
succeeds. It verifies that the exact package version is visible on npm, checks
the registry contract test, downloads checksum-pinned `mcp-publisher` `1.7.9`,
authenticates with GitHub OIDC, and publishes `server.json`. A manual stable-tag
dispatch provides an idempotent recovery path when npm is already published.
The first successful Registry publication is
[0.1.3 run 30863089628](https://github.com/NotNull92/hera-agent-unity/actions/runs/30863089628).
The current latest publication is
[0.1.4 run 31070065379](https://github.com/NotNull92/hera-agent-unity/actions/runs/31070065379); the public API reports `0.1.4` as active and latest.

## Publisher-owned Codex marketplace

The repository catalog is live on `main`:

```bash
codex plugin marketplace add NotNull92/hera-agent-unity --ref main
```

Open Codex and use `/plugins` to enable `hera-unity` from **Hera Agent Unity**.
The marketplace points to `./plugins/hera-unity`; it does not duplicate the
plugin bundle.

The distribution commit passed both the repository CI and HOL scanner:

- [CI run 30858316257](https://github.com/NotNull92/hera-agent-unity/actions/runs/30858316257)
- [HOL Plugin Scanner run 30858315912](https://github.com/NotNull92/hera-agent-unity/actions/runs/30858315912)

## Standalone Agent Skill

The open Skills CLI discovers `.agents/skills/<name>/SKILL.md` directly from a
Git repository. Hera's standalone skill is source-backed rather than uploaded
as a separate package:

```bash
npx skills add NotNull92/hera-agent-unity \
  --skill hera-agent-unity \
  --agent codex
```

The skill and Codex plugin are alternative delivery forms of the same
CLI-first workflow.

## MCP community expansion

### What the migration unlocked

CLI `v0.1.4` contains a real local MCP server. It uses stdio and can be launched
from the published npm package with this process contract:

```text
environment: HERA_MCP_ENABLED=1
command:     hera-agent-unity
arguments:   mcp --transport stdio --profile core
```

That makes Hera eligible for package-backed MCP discovery. It does **not** make
Hera a hosted remote service: the process must run on the user's machine, see
the local heartbeat directory, and connect to a running Unity Editor with the
UPM Connector installed. The adapter remains experimental and default-off.

### Publication flow

1. **GitHub Release first.** The stable `v0.1.4` tag produces the five native
   CLI assets used by every installer and by the npm wrapper.
2. **npm second.** Trusted publishing releases `hera-agent-unity@0.1.4` with
   immutable `mcpName` ownership metadata.
3. **Official MCP Registry third.** The successful npm workflow triggers the
   separate MCP workflow, which publishes the matching `server.json` through
   GitHub OIDC. Keeping this separate lets operators retry Registry publication
   without attempting to republish the immutable npm version.
4. **Verify downstream discovery.** Confirm the entry through the Registry API.
   Registry aggregators can consume that metadata; Glama states that it is a
   superset of the official Registry, so an official entry is preferable to
   maintaining duplicate metadata first.
5. **Request curated and community listings.** After the canonical entry is
   stable, request GitHub MCP Registry inclusion and submit a concise entry to
   `punkpeye/awesome-mcp-servers`.
6. **Package MCPB only if justified.** Smithery publishes local stdio servers as
   MCPB bundles. That is an additional versioned artifact and verification
   surface, not a free listing of the existing npm package.

Authoritative references:

- [Official MCP Registry publishing quickstart](https://modelcontextprotocol.io/registry/quickstart)
- [Official MCP Registry package types](https://modelcontextprotocol.io/registry/package-types)
- [Official MCP Registry GitHub Actions guide](https://modelcontextprotocol.io/registry/github-actions)
- [Official MCP Registry aggregators](https://modelcontextprotocol.io/registry/registry-aggregators)
- [GitHub MCP Registry overview](https://docs.github.com/en/copilot/concepts/context/mcp#about-the-github-mcp-registry)
- [Glama registry and submission model](https://glama.ai/)
- [Smithery local MCPB publishing](https://smithery.ai/docs/build/publish)
- [awesome-mcp-servers contribution guide](https://github.com/punkpeye/awesome-mcp-servers/blob/main/CONTRIBUTING.md)

## Release ownership

- GitHub `v*` tags own native CLI releases.
- `connector-*` tags own immutable UPM Connector pins.
- OpenUPM mirrors the Connector package version from the tagged repository source.
- `npm/package.json` must match the CLI release whose assets it downloads.
- `.github/workflows/npm-publish.yml` owns npm trusted publication.
- Root `server.json` and npm `mcpName` own official MCP Registry identity.
- `.github/workflows/mcp-publish.yml` owns ordered, retryable Registry publication.
- `plugins/hera-unity/.codex-plugin/plugin.json` versions the plugin workflow bundle independently.
- `.agents/plugins/marketplace.json` is the publisher-owned Codex catalog.
- HOL listings are third-party mirrors and are not the canonical plugin source.
