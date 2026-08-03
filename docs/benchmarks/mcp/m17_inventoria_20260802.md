# M17 Inventoria cross-verification — 2026-08-02

Inventoria verification source: `2ea5c38`

Unity: `6000.3.5f2`

Connector under test: repository source `0.0.74`, temporarily resolved as a
local package in the already-open Inventoria Editor

Final restored Connector: Git package `0.0.64`

## Result

The Inventoria cross-verification is **PASS** for every safe live category
executed below. At the time of this 2026-08-02 run, M17 was **BLOCKED** because
section 28.3 requires
the complete 14-case integration matrix to run in a marked disposable fixture.
The retained earlier fixture run and this production-project-safe Inventoria
run are complementary, but combining observations from two environments is
not the same evidence as one complete disposable-fixture suite.

The measured default-decision gates also do not justify promotion. Typed CLI
and the existing CLI remain primary. MCP remains unreleased, experimental,
stdio-only, and default-off.

## Durable artifacts

- [Repository gates and explicit exit codes](m17_inventoria_20260802_gates.txt)
- [Sanitized Inventoria and live MCP transcript](m17_inventoria_20260802_unity.jsonl)
- [Package, scene, console, hash, and byte-compare restoration proof](m17_inventoria_20260802_restore.txt)
- [A-to-E raw telemetry](m17_20260802_ae_1.jsonl)
- [Profile/Full definition measurement](m17_20260802_tool_definitions.txt)

The first three artifacts were derived from the retained local runner output
and contain no machine-specific paths or approval tokens. Historical fixture
observations without a retained artifact are not counted as PASS evidence.

## Required evidence

| M17 category | Result | Evidence |
|---|---|---|
| Full Go verification | PASS | `gofmt -l .` empty; `go vet ./...`, `go build ./...`, `go test -count=1 ./...`, `golangci-lint run ./...`, and guide sync all exited zero. Lint reported `0 issues`; focused catalog validator, registry, and schema gates also passed. |
| Connector compilation | PASS | Repository Connector `0.0.74` compiled in Inventoria. The same Editor PID returned `ready`; error console delta was zero. |
| Unity EditMode tests | PASS | Eight Editor-only suites produced 137 `[PASS]` lines, eight `ALL PASSED` summaries, and zero failures. The Unity Test Runner separately and truthfully discovered zero NUnit cases for filter `HeraAgent`. |
| Disposable-fixture integration | BLOCKED | No current durable artifact proves the complete section 28.3 matrix in one marked disposable fixture. Historical observations and this Inventoria run are not counted as a substitute. |
| MCP conformance | PASS | 52 focused official-SDK/server/process tests passed. A source-built MCP process was then reached by the official Go SDK against Inventoria: server `hera-agent-unity`, core Profile 8 tools, successful clean `GameScene` result. |
| A-to-E benchmark | PASS as transport smoke | Retained run `m17_20260802_ae_1` remained 5/5 first and final success. See the versioned benchmark report and raw JSONL. It is not statistical model-quality or billing evidence. |
| Safety approval | PASS | ApprovalPolicy and AssetMutationPreflight suites passed. A destructive batch was denied with `APPROVAL_REQUIRED` and completed zero items; reread proved the target remained. A separately approved cleanup executed once. |
| Response-loss exactly once | PASS | A delayed non-idempotent write client was terminated before receiving a response. Reusing the same operation ID returned `Count=1`; an independent read also returned `Count=1`. |
| Catalog reload | PASS | Adding a temporary strict tool changed domain epoch, catalog hash, and tool count 31→32; the tool returned `Value=17`. Removing it restored the original hash and count 32→31 under a new epoch. |
| Agent-guide sync | PASS | `go run ./tools/sync-agent-guides --check` exited zero. |

## Inventoria scenario transcript

The production project was used only for reversible or read-only validation.
No scene or asset was saved.

| Scenario | Observed result |
|---|---|
| query | `scene info` returned clean `GameScene`, root count 6. |
| invalid argument repair | Typed GameObject position array was rejected before Unity; the documented string form succeeded. A boolean component value was likewise rejected by the compatibility schema and repaired with its string form. |
| GameObject creation | Temporary `__HeraM17Transient` was created at `(17,0,0)`. |
| component mutation | `BoxCollider` was added; `m_IsTrigger` was set and reread as `true`. |
| missing target | A guaranteed-missing hierarchy path returned stable code `TARGET_NOT_FOUND`. |
| Play Mode | Entered Play Mode, observed `playing`, then exited normally. |
| tests | OperationLedger, ApprovalPolicy, AssetMutationPreflight, ToolCatalog, ToolContract, ToolDiscovery, ToolProfiles, and ToolSafety all passed. |
| package job | Local-source add completed through Unity Package Manager. Git restore completed as an asynchronous package job before exact baseline files were restored. |
| domain reload | Package switches and temporary Editor scripts changed the domain epoch while the Editor returned to `ready`. |
| destructive deny | An unapproved destructive batch returned `APPROVAL_REQUIRED`, `completed=0`, and left the temporary object present. |
| destructive approve | A bound single-use token approved deletion of only the temporary object. Subsequent lookup returned `TARGET_NOT_FOUND`. |
| batch | Read-only scene and object queries both completed; the destructive batch failed closed. |
| custom tool reload | `m17_reload_probe` appeared, returned `Value=17`, and disappeared after cleanup. |
| response loss | The replay and independent counter read both returned exactly one mutation. |

Direct UI and persistent asset authoring were not repeated in Inventoria. The
production-project safety rule forbids treating Inventoria as a destructive
benchmark fixture. Historical fixture observations are not retained as current
PASS evidence. Asset mutation preflight behavior was nevertheless exercised by
its Editor suite without leaving project state behind.

## Catalog reload measurements

| State | Catalog hash | Domain epoch | Tools | Actions |
|---|---|---|---:|---:|
| baseline | `sha256:2b358fd7ecb5dc0a936fd0c61ee6922d0032a5aac99b5c7db191bd5398d2d2d5` | `06d6468321c546b3877aa3f1131b1d22` | 31 | 75 |
| probe added | `sha256:7463561c3fed8d981d22ce9b482dc9c80a1fa6043cea5cbeb14bb5e05b8862f3` | `18a5d27d21e34ce59713b935fa0a3e4e` | 32 | 75 |
| probe removed | `sha256:2b358fd7ecb5dc0a936fd0c61ee6922d0032a5aac99b5c7db191bd5398d2d2d5` | `fd85b4b6e74647379394e0b82ae0b894` | 31 | 75 |

The restored hash proves the temporary tool left no catalog-contract residue;
the new epoch proves the removal reload was not served from stale state.

## Default-decision measurements

The retained A-to-E run has ceiling effects:

- A and B both recorded zero invalid arguments, 100% first success, 100% final
  success, and zero unsafe mutation. Typed benefit thresholds are not met.
- B through E recorded zero dependent model calls and zero billed model cost.
  MCP-primary model-call, first-success, and cost thresholds are not met.
- Profile versus Full retained equal final success and wrong-tool counts while
  reducing estimated serialized definition tokens by 72.5%. That subgate
  passes, but it cannot independently promote MCP.

## Cleanup and restoration

- All temporary GameObjects and custom scripts were removed.
- `GameScene` was reloaded from disk after the temporary scene-only mutation;
  final state was `dirty=false`, root count 6.
- Inventoria's package manifest SHA-256 was restored to
  `fb834dba745c143fa1d00959d921774440ce81830cb778ebb2e6c5ee124478f6`.
- Its package lock SHA-256 was restored to
  `44b6a9e7bf395e5b32e8e932101ca1a0a389640cbd22567b96f6fcec2affd32d`.
- Unity resolved Git Connector `0.0.64`, returned `ready`, and reported zero
  console errors.
- The Inventoria worktree was clean.
- No approval token is retained in this report or repository evidence.

One initial direct ToolCatalog menu invocation held the outer request lock
while the test synchronously dispatched an inner request. The user restarted
the Editor; no Editor process was launched, stopped, or restarted by the agent.
All subsequent menu suites were scheduled after the outer request completed,
and the full eight-suite run passed.

## Remaining blocker

Create or open a marked disposable Unity fixture and run all fourteen section
28.3 cases there: query, GameObject creation, component mutation, UI creation,
asset mutation, Play Mode, tests, package job, domain reload, invalid argument
repair, missing target, destructive approve/deny, batch, and custom-tool
reload. Retain its bounded transcript and cleanup proof. Response-loss
exactly-once remains a separate M17 evidence category and must also stay
retained. The required next step at that time was an independent PASS B review;
no default change was authorized. The follow-up below records its completion.

## Follow-up — 2026-08-03

The disposable-fixture prerequisite above is now fulfilled by
[`m17_fixture_6000.3.5f2_20260803.md`](m17_fixture_6000.3.5f2_20260803.md),
which retains all fourteen section 28.3 scenarios and cleanup proof from one
marked Unity `6000.3.5f2` fixture. The required independent PASS B review then
returned `APPROVE` with no findings, closing M17 as PASS without promoting MCP.
