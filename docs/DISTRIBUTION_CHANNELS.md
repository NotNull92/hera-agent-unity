# Distribution Channels

## Current release baseline

External state was last checked on 2026-08-10.

```text
CLI release:          v0.2.0
npm package:          0.2.0 (latest)
UPM Connector:        0.0.86
Codex plugin bundle:  1.0.1
Release commit:       7b1888bd676816f8fc67a2bbfaf3f81a8795f2c2
Distribution commit:  7b1888bd676816f8fc67a2bbfaf3f81a8795f2c2
Connector tag:        connector-0.0.86
CLI tag:              v0.2.0
```

Release evidence:

- [main CI run 31388970057](https://github.com/NotNull92/hera-agent-unity/actions/runs/31388970057)
- [GitHub Release run 31389063113](https://github.com/NotNull92/hera-agent-unity/actions/runs/31389063113)
- [npm 0.2.0 run 31389189346](https://github.com/NotNull92/hera-agent-unity/actions/runs/31389189346)

CLI, Connector, npm, and plugin versions are intentionally independent.

## Channel matrix

| Channel | External state | Repository state | Next action |
|---|---|---|---|
| GitHub Release binaries | `v0.2.0` published with five native assets | Complete | None |
| PowerShell / shell installers | Resolve GitHub `releases/latest` | Complete and follows `v0.2.0` | None |
| Go install | Resolves the latest module tag | Complete through `v0.2.0` | None |
| Unity UPM Git package | `connector-0.0.86` tag published | Complete | None |
| OpenUPM | Registry `latest` is `0.0.86` | Complete | Keep Connector package metadata compatible with OpenUPM |
| npm | Registry `latest` is `0.2.0` | Complete; automatic workflow-run publication is active | None |
| HOL awesome-codex-plugins | Hera Agent Unity is listed; source scanner passed for `5cf8b23` | Plugin `1.0.1` is scanner-gated | Upstream mirror refresh remains upstream-owned |
| HOL awesome-ai-plugins | Hera Agent Unity is listed | Uses the same upstream bundle | No separate package publication |
| Publisher-owned Codex marketplace | `main` exposes `.agents/plugins/marketplace.json` | Complete | Keep the catalog entry and plugin bundle in sync |
| Skills CLI / skills.sh | Repository-backed skill is installable | `.agents/skills/hera-agent-unity/SKILL.md` is the canonical source | Directory visibility remains telemetry-driven |
| OpenAI curated plugin directory | Hera is not listed; no general self-serve publish path was verified | Plugin bundle satisfies the standard repository shape | Treat as a curated-submission candidate, not an automated release channel |

> The MCP Registry listing and the MCP community channels that depended on it
> (GitHub MCP Registry, Glama, Smithery, `awesome-mcp-servers`) are not part of
> this matrix any more. CLI `v0.3.0` removed the stdio adapter those channels
> published, so there is no stdio launch contract to hand them.

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
[0.1.4 run 31070049824](https://github.com/NotNull92/hera-agent-unity/actions/runs/31070049824),
plus [0.2.0 run 31389189346](https://github.com/NotNull92/hera-agent-unity/actions/runs/31389189346).
The workflow fails before publishing unless:

- `npm/package.json` equals the requested `v*` tag;
- the GitHub release is stable;
- all five native assets exist;
- npm tests and `npm pack --dry-run` pass;
- the version is not already present on npm.

A manual stable-tag dispatch provides an idempotent recovery path when npm is
already published.

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

## Release ownership

- GitHub `v*` tags own native CLI releases.
- `connector-*` tags own immutable UPM Connector pins.
- OpenUPM mirrors the Connector package version from the tagged repository source.
- `npm/package.json` must match the CLI release whose assets it downloads.
- `.github/workflows/npm-publish.yml` owns npm trusted publication.
- `plugins/hera-unity/.codex-plugin/plugin.json` versions the plugin workflow bundle independently.
- `.agents/plugins/marketplace.json` is the publisher-owned Codex catalog.
- HOL listings are third-party mirrors and are not the canonical plugin source.
