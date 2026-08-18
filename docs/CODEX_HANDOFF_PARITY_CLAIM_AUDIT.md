# Codex Handoff — Pipeline Parity Claim Audit

## Goal

Independently verify or refute a set of claims made in a Claude session on
2026-08-18 about which official Unity CLI / `com.unity.pipeline` capabilities
Hera does **not** have, and why. The requester does not trust the claims and
asked for a full re-examination by a second agent.

This is a **claim audit**, not a source audit. `docs/UNITY_PIPELINE_PARITY_AUDIT.md`
already records the file-by-file source coverage pass; do not repeat it. What is
unverified here is whether the *conclusions* drawn from that work hold.

## Why the claims are suspect

The session produced two contradictory answers in a row:

1. **First answer** — a table of "10 unrealized items", produced by grepping
   `AgentConnector/` and reading the live catalog only. It did not consult
   `docs/UNITY_PIPELINE_PARITY_MATRIX.md` or `docs/DISCOVERY_SURFACE_DESIGN.md`.
2. **Second answer** — a retraction of most of that table, on the grounds that
   the matrix classifies all 153 public commands with no `planned` row left.

The second answer was derived from repository documents that the same session
never validated against the running Editor. **Treat both as unverified.** A
document asserting coverage is not evidence of coverage.

## Ground rules

- Verify against **source and a live Editor**, not against the design documents.
  `docs/UNITY_PIPELINE_PARITY_MATRIX.md` is itself under audit (C1).
- Connector-side conclusions need the three-bucket rule from `CLAUDE.md`:
  `6000.0`–`6000.2`, `6000.3`–`6000.4`, `6000.5+`. Representative versions come
  from `docs/UNITY_EDITOR_VERSION_INVENTORY.md`. A bucket you cannot run is
  `BLOCKED`, never `PASS`.
- Use `%HERA_UNITY_PROJECT%` (or an explicit `--project`) for the target
  project. Do not write machine-local absolute paths into any artifact.
- Run Unity-facing checks through the installed `hera-agent-unity` binary.
  `go run .` is for testing repository changes only.
- 🔒 The source material for the original survey is external. Whatever you
  conclude, do not let origin narrative reach tool `Description` strings,
  `agent_hint`, `CHANGELOG.md`, or commit messages. Internal design docs under
  `docs/` may name official commands; shipped surfaces may not.

## Claims to audit

Each row is falsifiable. Record `CONFIRMED`, `REFUTED`, or `BLOCKED` with the
command output that decided it.

| ID | Claim | Where it came from | Independent check | What refutes it |
|---|---|---|---|---|
| C1 | The parity matrix is complete and correct: 153 public commands classified `126 covered / 12 duplicate / 7 rejected / 6 excluded / 2 conditional`, no `planned` row. | `docs/UNITY_PIPELINE_PARITY_MATRIX.md` tail | Re-derive the command list from the installed official CLI and package source rather than from the matrix. Then spot-check every `covered` row by actually invoking the named Hera equivalent. | Any `covered` row whose Hera equivalent errors, is missing an action, or answers a different question. Any official public command absent from the matrix. |
| C2 | `search` was rejected on measured evidence: the Search index lags AssetDatabase intermittently (create-then-query returned the reference on `6000.0.35f1`, zero on `6000.3.5f2` and `6000.5.6f1`), and its asset/scene/menu query spaces are subsets of `manage_assets find` / `find_gameobjects` / `menu list`. | Commit `c56c1c1` body | Re-run the create-then-query probe on all three buckets. Compare `SearchService` results against `manage_assets find` / `deps reverse` on the same fixture. Test `dep:`, `#property`, `ref:` specifically. | A settled-index configuration where Search returns results the AssetDatabase path cannot, or where the lag does not reproduce. |
| C3 | `set_autotick` was dropped for no measurable gain: unfocused heartbeat cadence measured 1001–1109 ms against a 1.0 s target, and `EditorApplication.SignalTick` is non-public. | `docs/DISCOVERY_SURFACE_DESIGN.md` D6 | Measure heartbeat intervals with the Editor unfocused and minimized, during compile and during a bake, not only at idle. Confirm `SignalTick` accessibility per bucket. | Any state where an unfocused Editor stalls long enough to change a command outcome — e.g. bake/compile progress that does not advance until focus returns. |
| C4 | `package resolve` is impossible to contract because `Client.Resolve()` returns `void`. | `docs/DISCOVERY_SURFACE_DESIGN.md` D6 | Check the `UnityEditor.PackageManager.Client.Resolve` signature per bucket. Determine whether a completion signal is observable another way (registry events, `packages-lock.json` mtime, a follow-up `List`). | An observable completion signal that would let the action report success or failure honestly. |
| C5 | `import_asset` is a `duplicate`: an agent copying an external file into `Assets/` already has its own filesystem tools plus `editor refresh`. | matrix + `DISCOVERY_SURFACE_DESIGN.md` D6 | Attempt the workflow as a caller **without** filesystem tools (see C11). Check whether importer settings applied by the official command have no Hera equivalent after a plain file copy. | A caller class that cannot perform the copy, or import settings unreachable through `manage_asset_import` after copying. |
| C6 | Configurable authoring root is a `duplicate`; `AssetPathGuard` is hard-fixed to `Assets/`. | `AgentConnector/Editor/Core/AssetPathGuard.cs` (string literals near the containment check) | Read the guard end to end. Confirm whether any tool can write outside `Assets/`, and whether a fixed root actually blocks a real workflow. | A workflow that needs a narrower or different write root and has no other way to be constrained. |
| C7 | Durable handles closed the ObjectRef gap: `guid:<32hex>[:<fileId>]` and GlobalObjectId strings are accepted across `manage_assets`, `manage_material`, `manage_prefab`, `manage_asset_import`, `manage_animation`, with resolution centralized in `AssetPathGuard`. The remaining gap is that a failed resolution does not report the strategies it attempted. | Commit `8d17373`; `docs/ASSET_HANDLE_DESIGN.md`; `docs/TARGET_RESOLUTION_DESIGN.md` | Move an asset, then resolve the recorded path and the handle. Try a sub-asset handle against a multi-material container. Then feed a deliberately bad handle and read the error. | A tool in that list that rejects a handle, or a failure message that already names the attempted strategies (which would make the "remaining gap" claim false). |
| C8 | Per-record prefab `apply`/`revert` is deferred, not rejected: the record objects support it, but no identifier for a single override survives a domain reload. | Commit `c3c0ada` body | Check whether `PropertyModification` / override records expose anything stable across a reload. | A stable per-override identifier, which would turn a deferral into an open gap. |
| C9 | `audit` / `audit_status` are `conditional`: `6000.0` and `6000.3` expose no Project Auditor assembly; `6000.5+` exposes `UnityEditor.ProjectAuditorModule` but no fixture has `com.unity.project-auditor-rules`. | matrix | Install the rules package in a disposable fixture and check whether a positive, non-empty result is obtainable. | A reachable configuration producing non-empty modules/results, which makes it implementable now. |
| C10 | `switch_build_target` / `list_build_profiles` are rejected by a recorded decision (2026-08-13), on the grounds of no reported blocked workflow, minutes-long unresponsiveness, an `exec` fallback, and batch-mode `-buildTarget` being the standard path. | `docs/DECISION_LEDGER.md` | Confirm the ledger row and test whether the `exec` fallback still works now that `exec` is approval-gated. | A blocked workflow with no batch-mode alternative, or an `exec` fallback that no longer functions for a caller class. |
| C11 | The 12 `duplicate` classifications assume the caller has its own filesystem tools **and** permission to run arbitrary code. That assumption fails for an MCP client on the compact default, where arbitrary-code tools cannot be searched, described, or called without `--allow-arbitrary-code`, and it became more expensive for every caller when `exec` was approval-gated in v0.2.14. | Claude hypothesis, from `docs/DECISION_LEDGER.md` M10 row and live approval behavior — **least verified claim in this document** | Start MCP on the compact default and attempt each `duplicate` workflow end to end. Repeat with a plain shell caller that has no filesystem tooling. | Reaching every `duplicate` workflow from the compact default without arbitrary-code permission (refutes it), or finding additional caller classes that are also blocked (strengthens it). |
| C12 | These `covered` rows are judgment calls, not equivalences: `recompile_status` → `status` (5 states vs Hera's), `capture_game_view` → `screenshot --view game` (no camera selection, no max-resolution cap), `set_tags_layers` → `manage_editor` tag/layer actions, and no `feature_unavailable`-style version-gate code exists in Hera. | Claude source greps this session | For each, run the official command and the Hera equivalent against the same fixture and diff the answers. Grep for a version-gate response code. | Either an answer difference that makes `covered` wrong, or an existing version-gate code that makes the last part wrong. |

## Verification commands

Live surface:

```bash
hera-agent-unity --project "$HERA_UNITY_PROJECT" list --catalog \
  --schema_version hera.tool-catalog/1 > catalog.json
hera-agent-unity --project "$HERA_UNITY_PROJECT" list --tool <name>
hera-agent-unity <command> --help
```

Surface-cost comparison, if any conclusion proposes a change:

```bash
go run ./tools/catalog-payload-report \
  --catalog catalog.json \
  --compare docs/metrics/catalog-payload-baseline.json \
  --fail-on-change
```

`review_required` (exit `3`) means the surface change needs an explicit review
and an intentional baseline regeneration, not that it is forbidden.

Repository gates before any proposed change lands:

```bash
go clean -testcache
gofmt -w .
golangci-lint run ./...
golangci-lint fmt --diff
go test ./...
go run ./tools/sync-agent-guides --check
```

## Deliverable

Write findings to `docs/handoffs/` as a dated file. For each claim ID record:

- verdict (`CONFIRMED` / `REFUTED` / `BLOCKED`), and for `BLOCKED`, what was missing;
- the command and the output that decided it;
- the Unity version and bucket, for anything Connector-side;
- for a refuted claim, whether it implies a real capability gap, and if so, the
  `CLAUDE.md` admission-gate evidence it would need: the failure prevented,
  why an existing action or flag cannot absorb it, contract and safety impact,
  regression evidence, surface cost, and baseline review.

Do not implement anything from a refuted claim in the same pass that refutes it.
Report first; the queue decision is the user's.

## Known-good context

- Current release is CLI `v0.2.16`; Connector `0.1.2` was unchanged by it.
- Live catalog at the time of writing: **34 tools**, reported as 132 actions.
  `CLAUDE.md` still says `33 [HeraTool] classes` and does not mention
  `manage_timeline` — confirm which number is right as part of C1.
- The original survey and its raw 140-command dump live in `docs/report/`,
  which is gitignored. They are design input only, and the repository has no
  backup of them.
