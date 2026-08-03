# Distribution Channels

## Current release baseline

```text
CLI release:       v0.1.1
UPM Connector:     0.0.80
Release commit:    58c929996342d763ca2f012d8d266b6e7a055062
Connector tag:     connector-0.0.80
CLI tag:           v0.1.1
```

CLI and Connector versions are independent.

## Channel matrix

| Channel | External state checked 2026-08-04 | Repository state | Next action |
|---|---|---|---|
| GitHub Release binaries | `v0.1.1` published with five native assets | Complete | None |
| PowerShell / shell installers | Resolve GitHub `releases/latest` | Complete and already follows `v0.1.1` | None |
| Go install | Resolves the latest module tag | Complete through `v0.1.1` | None |
| Unity UPM Git package | `connector-0.0.80` tag published | Complete | None |
| npm | Registry `latest` is still `0.1.0` | Package `0.1.1`, release verification, and publish workflow prepared | Configure trusted publisher, then dispatch `npm-publish.yml` with `v0.1.1` |
| HOL awesome-codex-plugins | Hera Agent Unity is listed | Plugin `1.0.1` remains compatible and scanner-gated | Upstream mirror refresh happens after source update |
| HOL awesome-ai-plugins | Hera Agent Unity is listed | Same upstream manifest URL | No separate package publication |
| Publisher-owned Codex marketplace | Not present before this preparation | `.agents/plugins/marketplace.json` prepared | Push this commit, then users can add `NotNull92/hera-agent-unity` |
| OpenAI curated plugin directory | Hera is not listed; no general self-serve package publish path was verified | Plugin bundle satisfies the standard repository shape | Treat as a curated-submission candidate, not an automated release channel |
| Skills CLI / skills.sh | No indexed Hera page was found before preparation | Standard `.agents/skills/hera-agent-unity/SKILL.md` is repository-backed and statically valid | Available after source push; directory visibility follows first observed installations |

## npm activation

The npm package is public and owned by `notnull92`. The repository does not store an npm token. The prepared workflow uses npm trusted publishing with GitHub OIDC.

One-time npm package configuration:

```bash
npm trust github hera-agent-unity \
  --repo NotNull92/hera-agent-unity \
  --file npm-publish.yml \
  --yes
```

Equivalent npmjs.com fields:

```text
Publisher:         GitHub Actions
Organization/user: NotNull92
Repository:        hera-agent-unity
Workflow filename: npm-publish.yml
```

After this preparation commit is on `main`, dispatch **Publish npm Package** with:

```text
release_tag = v0.1.1
```

The workflow fails before publishing unless:

- `npm/package.json` equals the requested `v*` tag;
- the GitHub release is stable;
- all five native assets exist;
- npm tests and `npm pack --dry-run` pass;
- the version is not already present on npm.

## Publisher-owned Codex marketplace

After this commit is pushed:

```bash
codex plugin marketplace add NotNull92/hera-agent-unity --ref main
```

Open Codex and use `/plugins` to enable `hera-unity` from **Hera Agent Unity**. The repository marketplace points to `./plugins/hera-unity`; it does not duplicate the plugin bundle.


## Standalone Agent Skill

The open Skills CLI discovers `.agents/skills/<name>/SKILL.md` directly from a Git repository. Hera's standalone skill is therefore source-backed rather than uploaded as a separate package:

```bash
npx skills add NotNull92/hera-agent-unity \
  --skill hera-agent-unity \
  --agent codex
```

The skill and the Codex plugin are alternative delivery forms of the same CLI-first workflow. The skills.sh directory is telemetry-driven, so absence from search before the first installation does not mean the repository source is invalid.

## Release ownership

- GitHub `v*` tags own native CLI releases.
- `connector-*` tags own immutable UPM Connector pins.
- `npm/package.json` must match the CLI release whose assets it downloads.
- `plugins/hera-unity/.codex-plugin/plugin.json` versions the plugin workflow bundle independently.
- `.agents/plugins/marketplace.json` is the publisher-owned Codex catalog.
- HOL listings are third-party mirrors and are not the canonical plugin source.
