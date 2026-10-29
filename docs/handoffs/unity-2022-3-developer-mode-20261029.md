# Unity 2022.3 Developer Mode implementation evidence

Status: **BLOCKED before implementation — specification fact correction requested.**

## Source and scope

- Starting source: `main` at `867bf2dfaa9ded351cd40daac75bbb76a7a04823`.
- Working branch: `feat/unity-2022-3-developer-mode`.
- Specification: `docs/CODEX_HANDOFF_UNITY_2022_3_DEVELOPER_MODE.md`, FINAL v2.
- The only initial untracked file was that handoff. No production code was changed.
- No Unity process was started, no compiler or test was run, and no shared configuration or installed CLI was changed.

## Stop condition

FINAL v2 D6 says seven test files use Unity 6000.6-only APIs and instructs branching their expected-ID computations. The current files already use `EntityIdCompat.IdOf` / `ToObject` rather than direct EntityId APIs:

| File under `AgentConnector/Editor/Tests/` | Existing bridge usage |
|---|---|
| `EntityIdCompatTests.cs` | `RoundTrip` obtains and resolves IDs through the bridge; the 6000.6 mention is documentation |
| `ReleaseGateTests.cs:78` | Calls `EntityIdCompatTests.RunAll()` |
| `AtomicBatchTests.cs:39,77` | Uses `EntityIdCompat.IdOf(go)` |
| `ExecSerializerTests.cs:61` | Expected ID is `EntityIdCompat.IdOf(value)` |
| `SceneGameObjectToolTests.cs:102,110` | Uses `EntityIdCompat.IdOf(child)` |
| `ScreenshotAnnotationTests.cs:70,71` | Uses bridge IDs for target and blocker |
| `ScreenshotPhysicsTests.cs:64,66,70` | Uses bridge IDs for target, collider and camera |

Read-only search for `GetInstanceID|GetEntityId|EntityIdToObject|EntityIdIsValid|EntityId\.` in these seven files returns no matches (rg exit 1). A separate bridge-name search confirms the calls above. No compile success is inferred from this inspection.

The earlier design review incorrectly treated these bridge references as direct 6000.6-only API usage. This evidence corrects that review finding.

Requested clarification: retain these portable expectations unchanged; add the specified 2022.3 edge-case and gate tests plus the temporary-config fixture. Do not add unnecessary version branches to existing expectations. This is a fact correction under the FINAL v2 execution rule requiring a stop and report before continuing.

## Commands and results

| Command | Exit | Result |
|---|---:|---|
| `git status --short` | 0 | Initial handoff only; no other changes |
| `git switch -c feat/unity-2022-3-developer-mode` | 0 | Branch created from the starting main |
| `rg -n 'EntityId\|GetEntityId'` on the named tests | 0 | Bridge references found; see table |
| `rg -n 'GetInstanceID\|GetEntityId\|EntityIdToObject\|EntityIdIsValid\|EntityId\.'` on the seven named files | 1 | No direct version-specific API matches |
| `git diff --stat` | 0 | No tracked implementation changes at stop point |
| `git rev-parse HEAD` | 0 | Starting source SHA above |

The search patterns above are displayed with escaped separators for Markdown readability; the actual extended-regex searches used plain `|` alternation.

## Verification matrix

| FINAL v2 step | Status | Evidence / reason |
|---|---|---|
| 1. Go formatting, lint, tests | BLOCKED | Not run; stopped before implementation |
| 2. Commit and exact-source export | BLOCKED | No implementation candidate or Unity export produced |
| 3. Shared setting backup | BLOCKED | Not needed yet; no Unity or configuration mutation performed |
| 4. Unity 2022.3 normal/test-enabled passes | BLOCKED | Not run |
| 5. Unity 6000.6.7f1 regression | BLOCKED | Not run |
| 5. Required Unity 6000.6.6f1 representative | BLOCKED | Not installed per provided inventory; not substituted |
| 6. Catalog baseline comparison | BLOCKED | Not run |
| 7. Unity 2022.3 live matrix | BLOCKED | Not run |
| 8. Unity 6000.6.7f1 live smoke | BLOCKED | Not run |
| 9. Editor closure / configuration restoration | PASS (no action needed) | No Editor started; shared configuration and global CLI untouched |

Exported SHA: none. Verification CLI: not built; no binary path or version to report. Proposed next CLI release, once implemented and verified: `v0.3.24` (no tag created). Connector remains `0.2.13`; its planned `0.3.0` change has not been applied.

No compatibility PASS is claimed. Implementation and all runtime risks remain open pending the narrow D6 correction.
