# Codex Implementation Plan: CLI + MCP Adapter Migration

> **Document status:** Approved implementation plan  
> **Execution target:** OpenAI Codex, `gpt-5.6-sol medium`  
> **Repository baseline:** `main` at `f25ccd54f9d521f4cbff71440109842830744e44`  
> **Target architecture:** Existing CLI and Unity Connector retained, stdio MCP adapter added on the shared execution core  
> **Implementation state:** Not started by this document  
> **Normative scope:** This document defines the migration order, contracts, tests, stop conditions, rule-document changes, and rollback boundaries.

---

## 0. How Codex must use this document

This is an implementation specification, not an architecture discussion prompt.

Codex must:

1. Read `AGENTS.md`, `CLAUDE.md`, this document, and `docs/MCP_MIGRATION_PROGRESS.md` before changing code.
2. Implement **one work unit at a time** unless the user explicitly requests a larger batch.
3. Treat all decisions marked `LOCKED` as fixed.
4. Inspect the current repository state before applying instructions because line numbers may move after earlier milestones.
5. Use symbols and file responsibilities from this document, not line-number-only patches.
6. Run the narrow tests for the work unit first, then the repository-wide verification required by that work unit.
7. Update rule, lock, help, and generated agent documents whenever the code changes their truth.
8. Stop at a completion gate. Do not silently continue into the next milestone.
9. Record evidence in `docs/MCP_MIGRATION_PROGRESS.md`.
10. Never claim a milestone is complete only because the code compiles.

Codex must not:

- Replace the Unity localhost HTTP Connector with MCP.
- Delete the CLI.
- Create an independent MCP execution engine.
- Hand-maintain separate CLI and MCP tool definitions.
- Advertise commands or capabilities before their implementation passes tests.
- Retry a non-idempotent operation whose outcome is unknown.
- Push, tag, publish, release, or change installed packages without explicit user instruction.
- Modify unrelated files to make a test pass.
- Weaken existing locked Unity-version, UI-system, main-thread, or package-version decisions.

---

# 1. Migration authority and rule precedence

## 1.1 User-authorized exception to the old MCP prohibition

At the baseline commit, `CLAUDE.md` states that migration to MCP, relay servers, persistent servers, or Python runtimes must not be proposed. The user has now explicitly authorized the following narrower direction:

```text
Existing Go CLI + localhost HTTP Unity Connector
                       +
Optional Go stdio MCP adapter
                       +
One shared normalized tool contract registry
```

This authorization supersedes **only** the old blanket prohibition against an MCP adapter.

It does not supersede these existing decisions:

- The Unity Connector remains localhost HTTP + JSON.
- The Unity Connector remains Editor-only.
- Unity work enters through the existing main-thread queue.
- CLI and Connector versions remain independent.
- The single-editor model remains unchanged.
- Heartbeat and file-bus recovery remain valid architectural assets.
- Shared docs must not contain machine-specific absolute paths.
- Connector C# files and folders require Unity `.meta` files.
- Bundled agent-facing strings remain English.
- CLI changes and Connector changes use separate versioning.
- No package publication, tag, push, or release occurs without user instruction.

## 1.2 Required M0 action

Before MCP implementation code is added, M0 must revise the conflicting lock text in `CLAUDE.md`.

The replacement must describe an **authorized migration in progress**, not a completed feature.

Required meaning:

```text
LOCKED: The existing Go CLI and localhost HTTP Unity Connector remain the
execution core. An optional Go stdio MCP adapter may be added in front of the
same core. The adapter must not replace the Connector, fork tool definitions,
or remove CLI compatibility. Profile MCP is the planned default exposure,
Compact MCP is the dynamic fallback, and Full exposure is explicit opt-in.
Until the migration completion gate passes, CLI remains the production default.
```

Do not remove unrelated locked decisions from `CLAUDE.md`.

---

# 2. Verified baseline

The implementation begins from this verified state:

| Item | Baseline |
|---|---|
| Branch | `main` |
| Commit | `f25ccd54f9d521f4cbff71440109842830744e44` |
| Upstream | `origin/main`, ahead 0, behind 0 |
| Working tree | Clean at analysis time |
| Go module | `github.com/NotNull92/hera-agent-unity` |
| Go directive | `1.25.0` |
| Runtime tool count | 31 |
| Runtime action descriptors | 27 |
| Strict-invalid tool schemas | 21 of 31 |
| Invalid property-level `required: true` occurrences | 24 |
| Tools with an `action` parameter | 14 |
| Action parameters with enum schema | 0 |
| Tool-specific output schemas | 0 |
| Generic output schemas | 31 |
| Tools with meaningful safety declarations | 2 |
| Existing Go tests | Passing at baseline |

Measured catalog sizes from the baseline analysis:

| Surface | UTF-8 bytes |
|---|---:|
| Compact names | 435 |
| Summary list | 9,390 |
| All deep schemas combined | 69,841 |
| Synthesized Full Native MCP list | 47,943 |

The migration must remeasure these values after each catalog-affecting milestone. Do not copy them forward as current facts after the catalog changes.

## 2.1 Existing execution path to preserve

```text
CLI
  -> internal/client
  -> HTTP POST /command or /commands
  -> AgentConnector/Editor/HttpServer.cs
  -> ConcurrentQueue
  -> EditorApplication.update
  -> CommandRouter
  -> ToolDiscovery / [HeraTool]
  -> Unity main thread
```

Long-running path:

```text
run_id / job_id
  -> pending/result JSON
  -> domain reload
  -> result polling
```

## 2.2 Baseline files that define the current contract

```text
main.go
cmd/root.go
cmd/dispatch.go
cmd/send.go
cmd/test.go
cmd/manage_packages.go
cmd/doctor.go

internal/client/client.go
internal/client/types.go
internal/client/transport.go
internal/client/reload_retry.go
internal/client/cache.go
internal/poll/poll.go

AgentConnector/Editor/HttpServer.cs
AgentConnector/Editor/CommandRouter.cs
AgentConnector/Editor/ToolDiscovery.cs
AgentConnector/Editor/Heartbeat.cs

AgentConnector/Editor/Attributes/HeraToolAttribute.cs
AgentConnector/Editor/Attributes/HeraActionAttribute.cs
AgentConnector/Editor/Core/ToolMetadata.cs
AgentConnector/Editor/Core/SchemaUtility.cs
AgentConnector/Editor/Core/ToolParams.cs
AgentConnector/Editor/Core/ParamCoercion.cs
AgentConnector/Editor/Core/Response.cs
AgentConnector/Editor/Core/PackageJobState.cs

AgentConnector/Editor/Tests/ToolDiscoveryTests.cs
```

---

# 3. Locked target decisions

The following decisions are fixed for this migration.

## 3.1 Execution core

1. **LOCKED:** Keep `AgentConnector/Editor/HttpServer.cs`.
2. **LOCKED:** Keep `CommandRouter` and the Unity main-thread queue.
3. **LOCKED:** Keep localhost HTTP as the Go-to-Unity transport.
4. **LOCKED:** Keep heartbeat-based instance discovery.
5. **LOCKED:** Keep package and test file-bus recovery.
6. **LOCKED:** Keep the single-editor operating model.
7. **LOCKED:** Do not add a Python runtime, WebSocket relay, or separate Unity MCP package.

## 3.2 Interface model

1. **LOCKED:** CLI remains supported for CI, scripting, debugging, and recovery.
2. **LOCKED:** The MCP server is a Go adapter in the existing binary.
3. **LOCKED:** Initial MCP transport is stdio.
4. **LOCKED:** Streamable HTTP MCP is outside the first migration.
5. **LOCKED:** CLI and MCP share one normalized registry, validation layer, policy engine, internal client, and result mapping.
6. **LOCKED:** Do not implement MCP by repeatedly invoking `cmd.Execute`.
7. **LOCKED:** Do not fork C# and Go tool definitions.

## 3.3 Exposure model

1. **LOCKED:** Profile exposure is the planned normal MCP mode.
2. **LOCKED:** Compact MCP remains available for dynamic and legacy tools.
3. **LOCKED:** Full exposure is explicit opt-in.
4. **LOCKED:** `exec` and raw `menu` are excluded from normal profiles.
5. **LOCKED:** Arbitrary-code tools require an explicit server startup permission and per-operation policy approval.
6. **LOCKED:** A profile is fixed at MCP process startup. A prior request must not mutate the visible tool list as a hidden session side effect.

## 3.4 Reliability and safety

1. **LOCKED:** Safety metadata is enforced by server policy. It is not decoration.
2. **LOCKED:** Non-idempotent operations never auto-execute again after an unknown outcome.
3. **LOCKED:** Every Go-originating mutation receives an operation ID.
4. **LOCKED:** Connector-side execution ledger is the exactly-once authority.
5. **LOCKED:** Approval binds to operation ID, tool, normalized arguments hash, and expiry.
6. **LOCKED:** Client cancellation after commit does not imply rollback.
7. **LOCKED:** MCP annotations are conservative hints. They do not replace policy enforcement.
8. **LOCKED:** No built-in tool may remain safety-unclassified at the native MCP completion gate.

---

# 4. Explicit non-goals

The migration does not:

- Convert Unity transport from HTTP to MCP.
- Add multi-editor disambiguation.
- Add a public remote MCP endpoint.
- Add OAuth in the first release.
- Add a persistent Windows service.
- Redesign every existing Hera tool.
- Replace batch Undo semantics with a universal transaction.
- Make arbitrary C# execution safe by schema alone.
- Promise cancellation for Unity APIs that cannot be cancelled.
- Make model accuracy claims without the benchmark.
- Promote MCP to the production default before the benchmark gate.
- Remove legacy CLI command syntax.
- Require CLI and Connector version equality.

---

# 5. Target architecture

```text
AI client or human
   |
   +-- Legacy CLI syntax
   |
   +-- Typed CLI: call <tool> with JSON
   |
   +-- stdio MCP
           |
           v
Go Tool Registry Provider
   - fetch normalized catalog from Unity
   - disk/memory cache by project + domain epoch + catalog hash
   - legacy connector fallback
           |
           v
Shared Go Validation and Policy
   - JSON Schema 2020-12 prevalidation
   - aliases and deprecation normalization
   - profile visibility
   - approval
   - operation ID
   - result limits
           |
           v
Shared internal/client
           |
           v
Existing localhost HTTP Connector
           |
           v
Connector Contract Validator
   - authoritative validation
   - authoritative safety policy check
   - operation ledger
           |
           v
Existing CommandRouter and main-thread queue
           |
           v
Existing tool handler
```

Long-running work:

```text
MCP Tasks or blocking CLI wrapper
           |
           v
Go Task Bridge
           |
           v
Existing run_id / job_id / result-file mechanisms
```

---

# 6. Rule and lock document migration

This section is normative. Rule-document changes are implementation work.

## 6.1 Canonical rule hierarchy after M0

| File | Role | Source rule |
|---|---|---|
| `CLAUDE.md` | Repository development constitution, locked architecture, completed-item ledger | Hand-authored canonical |
| `AGENTS.md` | Canonical cross-tool agent guide | Hand-authored canonical |
| `AGENT.md` | Generated mirror used as the root embedded-guide source | Generated from `AGENTS.md` |
| `cmd/AGENT.md` | Generated byte-identical embed copy for `//go:embed` | Generated from `AGENTS.md` |
| `.cursor/rules/hera-agent-unity.mdc` | Tool-specific generated derivative | Generated from `AGENTS.md` |
| `.github/copilot-instructions.md` | Tool-specific generated derivative or stub | Generated from `AGENTS.md` |
| `GEMINI.md` | Tool-specific entry or stub | Generated from canonical rules |
| `.agents/agents.md` | Workspace handoff | Generated from canonical rules |
| `.agents/skills/hera-agent-unity/SKILL.md` | On-demand generated skill | Generated from canonical rules |

At the baseline, `AGENT.md` and `cmd/AGENT.md` already differ. M0 must treat this as pre-existing drift and eliminate it before adding new MCP instructions.

## 6.2 Required synchronization utility

Create:

```text
tools/sync-agent-guides/main.go
tools/sync-agent-guides/main_test.go
```

Required modes:

```text
go run ./tools/sync-agent-guides
go run ./tools/sync-agent-guides --check
```

Required behavior:

1. Read `AGENTS.md`.
2. Generate `AGENT.md`.
3. Generate `cmd/AGENT.md`.
4. Generate tool-specific derivatives using deterministic transforms.
5. In `--check`, write nothing and exit non-zero on drift.
6. Preserve required frontmatter for Cursor and AntiGravity skill files.
7. Never copy `CLAUDE.md` into downstream usage guides.
8. Produce deterministic UTF-8 with LF endings.
9. Do not include local absolute paths.

Add a CI test that fails when generated files drift.

## 6.3 Required `cmd/doctor.go` correction

Update comments and tests so the source relationship is truthful:

```text
AGENTS.md is canonical.
AGENT.md and cmd/AGENT.md are generated mirrors.
cmd/AGENT.md is embedded because go:embed cannot escape the package directory.
```

`doctor --agent-rules` must continue to extract only the intended sections.

## 6.4 Milestone documentation rule

Every milestone must declare:

```text
Rule-document impact:
- CLAUDE.md lock change:
- AGENTS.md user rule change:
- AGENT.md regeneration:
- cmd/AGENT.md regeneration:
- derived guide regeneration:
- README / README.ko change:
- docs change:
```

A milestone is not complete while code and rule documents disagree.

## 6.5 Transitional documentation rule

Do not describe an unimplemented MCP command as available.

Use these states:

- `planned`
- `experimental behind feature flag`
- `available but not default`
- `production default`

The state must match the code and tests.

---

# 7. Canonical tool contract registry

## 7.1 Source of truth

The authoring source remains in C# because:

- `[HeraTool]` and `[HeraAction]` already define runtime tools.
- Custom tools are discovered from loaded Unity assemblies.
- Unity-specific types and version facts are available in the Connector.
- A Go-only static registry would fork the contract.

The normalized runtime catalog emitted by the Connector is the execution contract consumed by both Typed CLI and MCP.

```text
C# attributes and contract DTO types
            |
            v
ToolContractRegistry
            |
            +-- CommandRouter validation
            +-- list catalog response
            +-- Go cache
            +-- Typed CLI
            +-- MCP
```

## 7.2 No new Unity tool for catalog retrieval

Extend the existing special `list` command.

New internal parameter:

```json
{
  "catalog": true,
  "schema_version": "hera.tool-catalog/1"
}
```

This returns the complete normalized catalog in one HTTP response.

Existing behavior must remain:

```text
list --compact
list --names
list
list --tool <name>
```

## 7.3 Catalog envelope

Required JSON shape:

```json
{
  "schema_version": "hera.tool-catalog/1",
  "catalog_hash": "sha256:<lowercase-hex>",
  "domain_epoch": "<opaque-domain-id>",
  "project_id": "sha256:<project-fingerprint>",
  "tools": []
}
```

Rules:

- `domain_epoch` changes on every Unity domain load.
- `catalog_hash` is deterministic for equivalent tool contracts.
- `catalog_hash` excludes timestamps, project paths, ports, PIDs, and domain epoch.
- `project_id` is a non-reversible fingerprint, not an absolute path.
- tools are ordered by ordinal tool name.
- actions are ordered by ordinal action name.
- schema object keys are canonicalized before hashing.

## 7.4 Tool definition

Required normalized shape:

```json
{
  "name": "scene",
  "title": "Scene",
  "description": "Scene operations.",
  "source": {
    "kind": "builtin",
    "assembly": "HeraAgent.Editor",
    "type": "HeraAgent.Tools.ManageScene"
  },
  "contract_mode": "strict",
  "profiles": ["core", "scene", "full"],
  "aliases": [],
  "examples": [],
  "input_schema": {},
  "output_schema": {},
  "actions": [],
  "safety": {}
}
```

`source.kind` values:

- `builtin`
- `custom`

`contract_mode` values:

- `strict`
- `legacy`

Rules:

- Native Profile and Full exposure only include `strict` tools.
- Legacy tools remain callable through Compact `tool_call`.
- A custom tool with no profile metadata belongs only to `custom` and `full`.
- A custom legacy tool is excluded from native exposure.
- A built-in legacy tool blocks the final native MCP completion gate.

## 7.5 Action definition

```json
{
  "name": "load",
  "description": "Load a scene.",
  "aliases": [],
  "input_schema": {},
  "output_schema": {},
  "safety": {}
}
```

The tool-level `input_schema` may use `oneOf` with `action.const` branches. Action-level schemas remain in `actions` for UI, documentation, and validation diagnostics.

## 7.6 C# types to add

Create under `AgentConnector/Editor/Core/`:

```text
ToolContractModels.cs
ToolContractRegistry.cs
ToolContractSchemaBuilder.cs
ToolContractValidator.cs
ToolContractCanonicalJson.cs
ToolContractProfiles.cs
ToolContractSafety.cs
```

Every new `.cs` requires a sibling `.meta`.

Suggested public/internal types:

```csharp
internal sealed class ToolCatalogEnvelope
internal sealed class ToolContract
internal sealed class ToolActionContract
internal sealed class ToolSafetyContract
internal sealed class ToolSafetyRule
internal sealed class ToolSourceContract
internal enum ToolContractMode
internal enum HeraRiskClass
internal static class ToolContractRegistry
internal static class ToolContractSchemaBuilder
internal static class ToolContractValidator
internal static class ToolContractCanonicalJson
```

Do not expose mutable static dictionaries directly.

---

# 8. Attribute and contract authoring changes

## 8.1 Extend `HeraToolAttribute`

Preserve all existing properties during compatibility migration.

Add:

```csharp
public string Title { get; set; }
public string[] Profiles { get; set; } = Array.Empty<string>();
public HeraRiskClass RiskClass { get; set; } = HeraRiskClass.Unspecified;
public bool RequiresConfirmation { get; set; }
public bool Reversible { get; set; }
public bool SupportsCancellation { get; set; }
public ToolContractMode ContractMode { get; set; } = ToolContractMode.Legacy;
```

Do not remove legacy booleans until after all callers and tests have migrated.

## 8.2 Extend `HeraActionAttribute`

Add:

```csharp
public Type ParametersType { get; set; }
public Type ResultType { get; set; }
public string[] Aliases { get; set; } = Array.Empty<string>();
public HeraRiskClass RiskClass { get; set; } = HeraRiskClass.Unspecified;
public bool RequiresConfirmation { get; set; }
public bool Reversible { get; set; }
public bool SupportsCancellation { get; set; }
```

## 8.3 Add class-level action contracts

Some current tools switch on `action` inside `HandleCommand` instead of using `[HeraAction]`.

Add:

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class HeraActionContractAttribute : Attribute
{
    public HeraActionContractAttribute(string action, Type parametersType);
    public Type ResultType { get; set; }
    public string Description { get; set; }
    public string[] Aliases { get; set; }
    public HeraRiskClass RiskClass { get; set; }
    public bool ReadOnly { get; set; }
    public bool Destructive { get; set; }
    public bool Idempotent { get; set; }
    public bool MayReloadDomain { get; set; }
    public bool RequiresPlayMode { get; set; }
    public bool RequiresConfirmation { get; set; }
    public bool Reversible { get; set; }
    public bool SupportsCancellation { get; set; }
}
```

This adds typed contracts without forcing every existing switch handler to be refactored into separate methods.

## 8.4 Extend `ToolParameterAttribute`

Add only fields the schema builder can enforce:

```csharp
public string[] Aliases { get; set; } = Array.Empty<string>();
public bool Deprecated { get; set; }
public string Format { get; set; }
public string SchemaJson { get; set; }
public bool AllowNull { get; set; }
```

`SchemaJson` is an escape hatch for complex JSON values. It must contain a valid JSON Schema fragment. Invalid fragments fail catalog construction.

Do not add a vague `object` type without a schema.

---

# 9. Schema generation and validation

## 9.1 Correct structural defects first

`ToolParameterMetadata.GenerateSchema` must stop writing:

```json
"required": true
```

inside a property schema.

Required fields exist only in the containing object:

```json
{
  "type": "object",
  "required": ["action"]
}
```

## 9.2 `SchemaUtility` replacement

Replace the current primitive-only mapping with a recursive builder.

Required support:

- `string`
- integral numeric types
- floating numeric types
- `bool`
- nullable value types
- enums
- arrays
- `IList<T>` / `List<T>`
- dictionaries with string keys
- nested DTO object types
- `JObject` / `JToken` only with explicit `SchemaJson`
- optional `format`
- defaults converted using invariant culture
- descriptions
- deprecation metadata
- `additionalProperties`

Do not recursively reflect UnityEngine.Object graphs.

For unsupported types, catalog construction must return a structured error. It must not silently emit `"type": "string"`.

## 9.3 Strict and legacy modes

### Strict

- Unknown parameters are rejected.
- aliases are normalized before validation.
- deprecated aliases generate diagnostics.
- required, enum, format, and conditional action schemas are enforced.
- schema uses `additionalProperties: false`, except explicitly declared map fields.

### Legacy

- Existing permissive handler behavior remains.
- unknown parameters generate warnings when possible.
- tool is not natively exposed through normal profiles.
- Compact MCP may call it under conservative policy.

## 9.4 Validation order

```text
1. Identify tool.
2. Normalize positional legacy args.
3. Normalize aliases.
4. Determine action.
5. Reject unknown action.
6. Normalize scalar compatibility forms.
7. Validate normalized input.
8. Evaluate policy.
9. Resolve approval.
10. Dispatch.
11. Validate output where an output schema exists.
```

## 9.5 Connector validation is authoritative

Go validation improves first-attempt accuracy, but direct or older clients may bypass it.

`CommandRouter` must validate again using the Connector contract model before invoking a handler.

Do not make the Connector parse its own emitted JSON Schema for normal validation. Validate against the typed contract model that generated the schema.

## 9.6 Go validation

Add an explicit JSON Schema 2020-12 validator dependency after dependency review.

Candidate:

```text
github.com/santhosh-tekuri/jsonschema/v6
```

Pin an exact reviewed version. Do not use `@latest`.

The Go validation layer must:

- compile schemas once per catalog hash,
- cache compiled schemas,
- return JSON Pointer paths,
- preserve Connector error codes when Connector rejects a request,
- never weaken Connector validation.

## 9.7 Required schema tests

```text
TestNoPropertyLevelBooleanRequired
TestRequiredIsTopLevelStringArray
TestArraysDeclareItems
TestNestedObjectsDeclareProperties
TestUnsupportedTypesFailCatalogBuild
TestStrictSchemaRejectsUnknownProperty
TestAliasesNormalizeBeforeValidation
TestDeprecatedAliasReturnsDiagnostic
TestActionSchemaUsesConstOrEnum
TestAllSchemasPassDraft202012MetaSchema
TestCatalogSchemasAreDeterministic
```

Completion gate:

```text
strict-invalid built-in schemas = 0
```

---

# 10. Action routing and error taxonomy

## 10.1 Fix unknown action behavior

Current behavior can fall back to a default handler when an explicit unknown action is provided.

New rule:

```text
If an explicit action is present and the tool declares actions:
- known action -> dispatch action handler or declared switch handler
- unknown action -> UNKNOWN_ACTION
- never fall back to default HandleCommand
```

A tool with no action contract may still use its default handler.

## 10.2 Stable error codes

Add or standardize:

```text
UNKNOWN_TOOL
UNKNOWN_ACTION
MISSING_ARGUMENT
UNKNOWN_ARGUMENT
ARGUMENT_TYPE_MISMATCH
ARGUMENT_FORMAT_INVALID
ARGUMENT_CONFLICT
INVALID_ARGUMENT
SCHEMA_INVALID
CATALOG_UNAVAILABLE
CATALOG_STALE

POLICY_BLOCKED
APPROVAL_REQUIRED
APPROVAL_DENIED
APPROVAL_INVALID

OPERATION_CONFLICT
OPERATION_IN_PROGRESS
OPERATION_OUTCOME_UNKNOWN
OPERATION_LEDGER_UNAVAILABLE

TASK_NOT_FOUND
TASK_CANCEL_UNSUPPORTED
TASK_EXPIRED

RESULT_SPOOLED
RESULT_RESOURCE_UNAVAILABLE
```

Existing stable tool codes remain valid.

Every validation error should include:

```json
{
  "path": "/action",
  "expected": ["info", "load", "save", "list", "close"],
  "actual": "lod"
}
```

Do not branch on error message text.

---

# 11. Safety contract and policy

## 11.1 Risk classes

```csharp
internal enum HeraRiskClass
{
    Unspecified = 0,
    ReadOnly = 1,
    Write = 2,
    Destructive = 3,
    ArbitraryCode = 4,
    PackageChange = 5,
    ExternalProcess = 6
}
```

## 11.2 Normalized safety shape

```json
{
  "risk_class": "read_only",
  "read_only": true,
  "destructive": false,
  "idempotent": true,
  "may_reload_domain": false,
  "requires_play_mode": false,
  "requires_confirmation": false,
  "reversible": false,
  "supports_cancellation": false,
  "side_effect_scope": "none",
  "rules": []
}
```

## 11.3 Parameter-dependent safety

Tool-level booleans are not enough. Examples:

- `console` read versus `clear=true`
- `exec` compile-only versus execute
- `menu list` versus executing a menu path
- `manage_assets find` versus delete
- `scene info` versus load/save/close

Add operation-level safety rules:

```json
{
  "operation": "clear",
  "when": {
    "clear": { "const": true }
  },
  "risk_class": "destructive",
  "requires_confirmation": true,
  "idempotent": true
}
```

Policy chooses the most specific matching rule. If no rule matches, use the default. Ambiguous matching rules fail catalog validation.

## 11.4 Conservative MCP annotations

Map normalized safety to MCP annotations conservatively:

- `readOnlyHint = true` only when every allowed operation is read-only.
- `destructiveHint = true` when any allowed operation may destroy or irreversibly replace state.
- `idempotentHint = true` only when every allowed operation is idempotent.
- `openWorldHint = true` for package, external process, network, or arbitrary code capability.

Annotations are hints. The policy engine remains authoritative.

## 11.5 Built-in safety completion gate

Every built-in tool and action must be classified.

An `Unspecified` built-in contract is a test failure.

A custom `Unspecified` contract:

- is hidden from normal profiles,
- is hidden from native Full,
- is callable only through Compact,
- requires confirmation,
- is treated as non-idempotent,
- is treated as potentially destructive.

## 11.6 Initial safety audit guidance

The exact audit must inspect handlers, but use these conservative starting points:

| Tool or operation | Initial class |
|---|---|
| queries such as describe, find, docs, list, status | ReadOnly |
| screenshot capture | Write, reversible |
| log to console | Write, idempotent false |
| console clear | Destructive, confirmation |
| scene info/list | ReadOnly |
| scene load | Write, may reload scene state |
| scene save | Write |
| scene close | Destructive, confirmation |
| GameObject/component create/set | Write |
| GameObject/component destroy/remove | Destructive, confirmation |
| asset create/copy/move/delete | Write or Destructive by action |
| package add/remove/embed | PackageChange, confirmation, async |
| refresh/compile/reserialize | Write, may reload domain |
| tests | Write to test state, long-running |
| Play Mode transitions | Write, stateful |
| menu list | ReadOnly |
| menu execute | ArbitraryCode-equivalent, confirmation |
| exec compile-only | Write to compile cache, advanced |
| exec execute | ArbitraryCode, confirmation, advanced |

Do not copy this table blindly. Verify each handler.

---

# 12. Profile definitions

Profiles are defined in Connector metadata and normalized by the registry. Go must not maintain a second hard-coded truth.

## 12.1 `core`

```text
console
find_gameobjects
manage_gameobject
manage_components
scene
manage_editor
screenshot
refresh_unity
```

## 12.2 `scene`

```text
scene
find_gameobjects
manage_gameobject
manage_components
manage_prefab
manage_material
manage_animation
screenshot
refresh_unity
```

## 12.3 `assets`

```text
manage_assets
manage_asset_import
manage_material
manage_prefab
describe_shader
detect_assets
reserialize
refresh_unity
manage_packages
```

## 12.4 `ui`

```text
manage_ui
ui_doc
ui_slop
game_feel
input
screenshot
manage_components
manage_gameobject
```

## 12.5 `diagnostics`

```text
console
describe_type
describe_shader
find_method
list_assemblies
profiler
screenshot
run_tests
unity_docs
log
```

## 12.6 `testing`

```text
run_tests
console
manage_editor
screenshot
input
profiler
```

## 12.7 `custom`

Strict custom tools with an explicit `custom` profile.

## 12.8 `full`

Every strict tool allowed by normal policy, excluding arbitrary-code tools.

## 12.9 `advanced`

Adds:

```text
exec
menu execute
other raw or arbitrary execution surfaces
```

Starting `advanced` requires:

```text
--allow-arbitrary-code
```

Each arbitrary operation still requires approval.

## 12.10 Profile validation

Tests must assert:

- deterministic ordering,
- no duplicate names,
- every profile tool exists,
- normal profiles contain no legacy tool,
- normal profiles contain no arbitrary-code operation,
- profile visibility does not change because of a previous request,
- custom tools without explicit profile do not leak into `core`, `scene`, `assets`, `ui`, `diagnostics`, or `testing`.

---

# 13. Go package layout

Create these packages incrementally:

```text
internal/toolregistry/
  types.go
  provider.go
  unity_provider.go
  legacy_provider.go
  cache.go
  canonical.go
  profiles.go
  errors.go
  *_test.go

internal/schema/
  compiler.go
  validator.go
  errors.go
  *_test.go

internal/policy/
  types.go
  engine.go
  approval.go
  token.go
  *_test.go

internal/idempotency/
  types.go
  store.go
  atomic.go
  reconcile.go
  *_test.go

internal/taskbridge/
  types.go
  manager.go
  package_job.go
  test_run.go
  *_test.go

internal/mcpserver/
  config.go
  server.go
  discovery.go
  native_tools.go
  compact_tools.go
  results.go
  middleware.go
  notifications.go
  *_test.go

internal/telemetry/
  event.go
  recorder.go
  jsonl.go
  *_test.go

cmd/
  call.go
  mcp.go

tools/
  sync-agent-guides/
  validate-tool-catalog/
  benchmark-mcp/
```

Rules:

- `internal/mcpserver` may import `internal/toolregistry`, `internal/schema`, `internal/policy`, `internal/idempotency`, `internal/taskbridge`, and `internal/client`.
- `internal/toolregistry` must not import `cmd`.
- `internal/policy` must not write to stdout.
- `internal/mcpserver` owns protocol output.
- `cmd/mcp.go` is a thin configuration and startup wrapper.
- Shared execution logic must not depend on terminal formatting.

---

# 14. Typed CLI

## 14.1 New generic command

Add:

```text
hera-agent-unity call <tool> --json '<object>'
hera-agent-unity call <tool> --file request.json
echo '<object>' | hera-agent-unity call <tool>
```

Optional flags:

```text
--profile <name>
--operation-id <id>
--approve <token>
--validate-only
--explain
```

Precedence:

```text
explicit --json
stdin
--file
empty object
```

Do not combine multiple sources silently. Conflicts return an error.

## 14.2 Legacy syntax remains

Examples such as these remain valid:

```text
hera-agent-unity scene info
hera-agent-unity console --type error
hera-agent-unity manage_components set ...
```

Legacy syntax is normalized to the same request object and sent through the same validation and policy path.

## 14.3 Do not generate 31 independent Go command implementations

Dynamic custom tools make a compile-time per-tool Go command tree the wrong source of truth.

“Typed CLI” means:

- runtime schema-aware invocation,
- generated help,
- generated completion,
- validation before HTTP,
- exact action and parameter diagnostics.

Built-in shell completions and docs may be generated from a captured catalog, but execution remains registry-driven.

## 14.4 Local parser isolation

The long-lived MCP process must not reuse package-global CLI flags.

Refactor global configuration into an immutable struct:

```go
type GlobalConfig struct {
    Project     string
    Port        int
    Timeout     time.Duration
    Verbose     bool
    Quiet       bool
    Debug       bool
    CompactJSON bool
    Narrate     bool
}
```

New subcommands use local `flag.FlagSet` or an equivalent isolated parser.

Existing CLI behavior must remain covered by tests.

---

# 15. stdio MCP adapter

## 15.1 Command surface

Initial command:

```text
hera-agent-unity mcp --transport stdio --profile core
```

Additional flags:

```text
--profile <core|scene|assets|ui|diagnostics|testing|custom|full|advanced>
--exposure <profile|compact|full>
--strict
--allow-arbitrary-code
--max-inline-bytes <n>
```

`stdio` is the only accepted transport in the initial release.

## 15.2 stdout purity

When `mcp` runs:

- stdout contains protocol frames only.
- banners, hints, progress, debug, timings, and diagnostics go to stderr.
- update notices are disabled.
- terminal styling is disabled.
- no child CLI invocation writes to stdout.
- panic recovery writes a protocol error if possible and diagnostics to stderr.

Add a subprocess test that fails on any non-protocol stdout byte.

## 15.3 SDK use

Use the official Go MCP SDK.

Dependency rule:

1. At implementation time inspect available official versions.
2. Select the first reviewed stable `v1.7.x` or later release that supports protocol revision `2026-07-28`.
3. Pin the exact version in `go.mod`.
4. Do not use `@latest`.
5. If only a prerelease is available, keep MCP experimental, pin the exact prerelease, and record that limitation in `docs/MCP_MIGRATION_PROGRESS.md`.

Use official server and stdio APIs rather than hand-rolling JSON-RPC.

For dynamic registry tools, prefer the SDK’s low-level tool registration API with raw schemas, while retaining Hera’s own validation and policy.

## 15.4 Protocol behavior

Required:

- current protocol revision support,
- SDK-supported legacy fallback,
- `server/discover` when provided by the selected SDK/spec implementation,
- deterministic tool list,
- cache metadata,
- capability negotiation,
- cancellation,
- structured tool results,
- list-changed notifications when supported.

Do not invent protocol method names that differ from the pinned SDK. Adapt this requirement to the actual official API and record the mapping.

## 15.5 Tool result mapping

Success:

```json
{
  "structuredContent": {
    "success": true,
    "message": "OK",
    "data": {}
  },
  "content": [
    {
      "type": "text",
      "text": "OK"
    }
  ]
}
```

Tool execution failure:

- return an MCP tool result with `isError: true`,
- preserve Hera `code`, `message`, `data`, and `suggestions`,
- do not turn expected Unity validation failures into JSON-RPC protocol failures.

Protocol failure:

- malformed MCP request,
- invalid protocol state,
- internal adapter failure before a tool invocation exists.

## 15.6 Native registration

At startup:

1. Discover Unity instance.
2. Fetch catalog.
3. Select fixed profile.
4. Compile schemas.
5. Register visible strict tools in ordinal order.
6. Start stdio server.
7. Observe domain epoch in the background.
8. On catalog change, update registry and notify clients when supported.

If no Unity instance is available, startup should fail clearly in the first release. Offline catalog-only mode is a later option.

---

# 16. Compact MCP

Compact exposure registers only:

```text
tool_search
tool_describe
tool_call
```

## 16.1 `tool_search`

Input:

```json
{
  "query": "scene load",
  "profile": "scene",
  "limit": 5,
  "include_schema": true
}
```

Output:

- ranked tool names,
- descriptions,
- contract mode,
- safety summary,
- optional compact input schema.

Search is deterministic. It may use lexical matching first. Do not introduce embeddings in this migration.

## 16.2 `tool_describe`

Input:

```json
{
  "name": "scene"
}
```

Output:

- full normalized definition,
- current catalog hash,
- current domain epoch.

## 16.3 `tool_call`

Input:

```json
{
  "name": "scene",
  "arguments": {
    "action": "info"
  },
  "operation_id": "<optional-client-id>"
}
```

The server still validates, applies policy, and creates an operation ID when absent.

Compact mode may call legacy custom tools. Native mode may not.

---

# 17. Catalog cache and invalidation

## 17.1 Heartbeat addition

Add a cheap domain-scoped field to `Heartbeat`:

```json
{
  "domainEpoch": "<opaque-id>",
  "features": [
    "tool_catalog_v1",
    "operation_ledger_v1"
  ]
}
```

Rules:

- `domainEpoch` is generated once per domain load.
- Do not compute the entire catalog every heartbeat tick.
- `features` are capability names, not version equality checks.
- Existing clients ignore unknown fields.

## 17.2 Go cache key

```text
project_id
connector feature set
domain_epoch
catalog_hash
```

Memory cache:

- shared by Typed CLI execution inside one process,
- concurrency-safe.

Disk cache:

```text
~/.hera-agent-unity/cache/catalog/<project-id>/<catalog-hash>.json
```

Rules:

- atomic write,
- bounded old entries,
- no credentials,
- no absolute project path in shared output,
- schema validation before accepting cache.

## 17.3 Invalidation

On `domainEpoch` change:

1. pause new native registrations or mark catalog stale,
2. fetch new catalog,
3. validate it,
4. compare catalog hash,
5. replace compiled schemas atomically,
6. issue list-changed notification if the tool list or schemas changed,
7. resume calls.

An in-flight call continues under the catalog snapshot with which it started.

## 17.4 Legacy Connector fallback

If `tool_catalog_v1` is absent:

1. call compact list,
2. call per-tool describe,
3. normalize a conservative legacy catalog,
4. expose Compact MCP only,
5. disable native Profile and Full modes,
6. disable operation-ledger-dependent automatic retry,
7. emit one stderr warning.

This preserves independent CLI and Connector versions without pretending the old Connector has strict contracts.

---

# 18. Operation ID and execution ledger

## 18.1 HTTP request extension

Extend the request without breaking old clients:

```json
{
  "command": "manage_gameobject",
  "params": {},
  "meta": {
    "operation_id": "01J...",
    "arguments_hash": "sha256:...",
    "approval_token": null,
    "client_kind": "cli|mcp",
    "catalog_hash": "sha256:..."
  }
}
```

Unknown `meta` fields are ignored for forward compatibility.

## 18.2 Connector request context

Add:

```csharp
internal sealed class CommandRequestContext
{
    public string OperationId { get; }
    public string ArgumentsHash { get; }
    public string ApprovalToken { get; }
    public string ClientKind { get; }
    public string CatalogHash { get; }
    public CancellationToken CancellationToken { get; }
}
```

Update dispatch signatures deliberately. Do not thread unstructured `JObject` metadata through handlers.

## 18.3 Ledger location

```text
~/.hera-agent-unity/status/operations/<project-id>/<operation-id>.json
```

Use `AtomicFile`.

States:

```text
received
running
committed
responded
outcome_unknown
cancelled
failed
```

Record:

```json
{
  "schema_version": 1,
  "operation_id": "...",
  "tool": "...",
  "action": "...",
  "arguments_hash": "...",
  "risk_class": "...",
  "idempotent": false,
  "state": "committed",
  "started_unix_ms": 0,
  "committed_unix_ms": 0,
  "response": {},
  "response_hash": "sha256:...",
  "domain_epoch": "..."
}
```

Do not store secrets.

## 18.4 Dispatch rules

### New operation

1. validate request,
2. write `received`,
3. acquire command lock,
4. write `running`,
5. invoke handler,
6. serialize final response,
7. atomically write `committed` with response,
8. return response,
9. best-effort mark `responded`.

### Same ID and same arguments

| Ledger state | Behavior |
|---|---|
| committed/responded | Replay stored response |
| received | Resume only if no execution began |
| running in current domain | Return `OPERATION_IN_PROGRESS` |
| running from prior domain | Mark/return `OPERATION_OUTCOME_UNKNOWN` |
| outcome_unknown | Never invoke non-idempotent handler automatically |
| failed | Replay stable failure unless policy allows a new operation ID |
| cancelled before dispatch | Return cancelled |

### Same ID and different arguments

Return `OPERATION_CONFLICT`.

## 18.5 Reload retry change

`internal/client/reload_retry.go` must become operation-aware.

Rules:

- read-only and idempotent calls may resend the same operation ID,
- non-idempotent calls may resend only to query/replay ledger state,
- Connector must not invoke the handler again for an unknown prior operation,
- if Connector lacks ledger capability, non-idempotent transient failure returns `OPERATION_OUTCOME_UNKNOWN`,
- do not generate a new operation ID during retry.

## 18.6 Ledger retention

Initial policy:

- keep committed/failed records for 24 hours,
- keep unknown records for 7 days,
- cap by total bytes per project,
- cleanup only records outside retention,
- never delete a running record,
- cleanup runs best-effort outside the command lock.

Values are configurable and require tests.

---

# 19. Approval and MRTR

## 19.1 Policy sequence

```text
validated input
  -> safety resolution
  -> approval requirement
  -> approval verification
  -> ledger
  -> dispatch
```

Approval occurs before any mutation or `running` ledger state.

## 19.2 Approval binding

An approval token binds:

```text
operation_id
tool
action
arguments_hash
risk_class
project_id
expires_at
single_use
```

Changing arguments invalidates approval.

## 19.3 CLI fallback

Interactive CLI may print a deterministic summary and ask for confirmation only when attached to a human TTY.

Non-interactive CLI returns:

```text
APPROVAL_REQUIRED
```

with a preflight token.

Second call:

```text
--approve <token>
```

## 19.4 MCP flow

When the client supports the required user-input flow:

- request approval through the negotiated MCP mechanism,
- show exact target and side effect,
- continue only after approval.

When unsupported:

- return a tool error containing approval-required metadata,
- accept a second call carrying the approval token in Hera request metadata,
- never silently downgrade approval.

## 19.5 Approval UI summary

Must include:

```text
tool and action
target
side effect
reversibility
domain reload possibility
external/package impact
operation ID
```

Never request approval with only “Are you sure?”

---

# 20. Cancellation and concurrency

## 20.1 Go cancellation

Every MCP request gets a request-scoped context.

Cancellation propagates through:

```text
MCP handler
 -> policy/registry wait
 -> instance discovery
 -> HTTP request
 -> task bridge
```

## 20.2 Connector cancellation phases

Support these phases in order:

1. queued but not dispatched,
2. waiting for command lock,
3. cooperative long-running handler,
4. async task polling.

Do not claim support for phase 3 until the individual Unity API accepts or observes cancellation.

## 20.3 Post-commit cancellation

If cancellation arrives after commit:

- do not rollback automatically,
- preserve committed result,
- return cancellation status with operation ID when possible,
- allow later result lookup.

## 20.4 Adapter concurrency

The MCP process may receive concurrent requests, but Unity execution is serialized.

Add a bounded adapter queue.

Initial default:

```text
max queued Unity operations: 32
max concurrent registry/read-only local operations: implementation-defined and tested
max active Unity handler operations: 1
```

Queue overflow returns a structured busy error. Do not allow unbounded goroutines to accumulate.

## 20.5 Remove process-global logger mutation

`internal/poll.WaitForAsyncJob` must stop changing the global standard logger with `log.SetOutput`.

Replace it with request-scoped logging or eliminate the obsolete suppression after verifying that the current closed-connection fix makes it unnecessary.

This is a prerequisite for concurrent long-lived MCP operation.

---

# 21. Tasks bridge

## 21.1 Preserve existing durable mechanisms

Do not replace:

- test `run_id`,
- package `job_id`,
- result files,
- pending package records,
- post-reload package verification.

Wrap them in a generic Go task abstraction.

## 21.2 Task model

```go
type State string

const (
    StateWorking       State = "working"
    StateInputRequired State = "input_required"
    StateCompleted     State = "completed"
    StateFailed        State = "failed"
    StateCancelled     State = "cancelled"
)

type Task struct {
    ID          string
    Kind        string
    State       State
    Progress    *Progress
    OperationID string
    Result      json.RawMessage
    Error       *TaskError
    CreatedAt   time.Time
    UpdatedAt   time.Time
}
```

## 21.3 First task adapters

1. Unity test runs
2. Package Manager add/remove/embed

Do not generalize every command into a task.

## 21.4 Capability fallback

- If MCP Tasks is negotiated, expose durable task handles.
- If not, retain current blocking/polling behavior.
- CLI behavior remains backward compatible.
- A task ID must survive adapter restart when the underlying file-bus job survives.

## 21.5 Cancellation

- tests: support only if the Unity Test Framework path can actually cancel,
- package operations: report unsupported when Package Manager cannot safely cancel,
- polling cancellation does not mean the Unity operation stopped.

---

# 22. Result size and resources

## 22.1 Model-facing limit

The existing 50 MiB HTTP transport limit is not a model-context policy.

Add:

```text
HERA_MCP_MAX_INLINE_BYTES
```

Initial default:

```text
131072 bytes
```

This is a proposed default and must be benchmarked.

## 22.2 Inline behavior

Under limit:

- return structured content,
- include compact text fallback.

Over limit:

1. write the full result atomically under a result cache,
2. return summary, byte size, hash, truncation status, and resource handle,
3. expose a supported resource read path,
4. avoid putting the full result in tool content.

## 22.3 Result storage

```text
~/.hera-agent-unity/results/<project-id>/<operation-id>/<result-hash>.json
```

Add retention and byte caps.

Do not spool credentials or arbitrary sensitive files.

## 22.4 Encourage projection first

Before spooling, preserve and expand existing low-token controls:

- limit,
- offset/cursor,
- fields projection,
- IDs-only,
- names-only,
- stacktrace mode,
- depth.

---

# 23. Long-lived process requirements

The existing CLI is short-lived. MCP is long-lived.

Required refactors:

- immutable startup config,
- local FlagSets,
- no package-global output mode mutation after startup,
- no global logger redirection,
- concurrency-safe catalog cache,
- concurrency-safe compiled schema cache,
- bounded queues,
- graceful context cancellation,
- deterministic shutdown,
- no update notices,
- no TUI,
- no stdout contamination,
- instance loss and rediscovery,
- domain epoch observation,
- stale catalog rejection,
- memory caps for operation and result caches.

Do not run `cmd.Execute` repeatedly inside the MCP server.

---

# 24. Version negotiation and compatibility

## 24.1 MCP protocol

Target the official 2026-07-28 protocol behavior through the official SDK.

Support a legacy revision only when the pinned SDK supports it without forking the server implementation.

Record accepted protocol versions in tests and `docs/MCP_MIGRATION_PROGRESS.md`.

## 24.2 CLI and Connector independence

Do not add equality checks between CLI release tag and Connector package version.

Use optional Connector feature capabilities:

```text
tool_catalog_v1
operation_ledger_v1
approval_v1
task_bridge_v1
domain_epoch_v1
```

An older Connector triggers a documented degraded mode.

## 24.3 Dependency review

For each new dependency record:

```text
module
exact version
license
reason
transitive impact
security scan result
rollback version
```

No new dependency is accepted solely because it is convenient.

---

# 25. File impact matrix

## 25.1 Preserve with targeted edits

| File | Planned change |
|---|---|
| `main.go` | Keep entry point; no protocol logic |
| `cmd/root.go` | config isolation and new standalone routing |
| `cmd/dispatch.go` | route `call` and `mcp`; retain legacy |
| `cmd/doctor.go` | correct canonical guide relationship and feature reporting |
| `internal/client/types.go` | request metadata, capability fields |
| `internal/client/transport.go` | metadata serialization and context behavior |
| `internal/client/reload_retry.go` | operation-aware retry |
| `internal/poll/poll.go` | request-scoped logging |
| `HttpServer.cs` | parse request metadata and cancellation |
| `CommandRouter.cs` | validation, policy, ledger, error taxonomy |
| `ToolDiscovery.cs` | normalized catalog facade |
| `Heartbeat.cs` | domain epoch and feature capabilities |
| `Response.cs` | structured operation metadata if required |
| `PackageJobState.cs` | task bridge metadata, retain file bus |

## 25.2 Extend

```text
AgentConnector/Editor/Attributes/HeraToolAttribute.cs
AgentConnector/Editor/Attributes/HeraActionAttribute.cs
AgentConnector/Editor/Core/ToolMetadata.cs
AgentConnector/Editor/Core/SchemaUtility.cs
AgentConnector/Editor/Tests/ToolDiscoveryTests.cs
cmd/root_test.go
cmd/help/*
README.md
README.ko.md
docs/ARCHITECTURE.md
docs/GO_CLI.md
docs/CSHARP_CONNECTOR.md
docs/COMMANDS.md
docs/INDEX.md
CHANGELOG.md
```

## 25.3 Create

Use the package layout in section 13 plus:

```text
docs/MCP_MIGRATION_PROGRESS.md
docs/MCP.md
AgentConnector/Editor/Core/OperationLedger.cs
AgentConnector/Editor/Core/OperationLedger.cs.meta
AgentConnector/Editor/Tests/OperationLedgerTests.cs
AgentConnector/Editor/Tests/OperationLedgerTests.cs.meta
```

## 25.4 Do not delete

```text
CLI legacy commands
HttpServer
CommandRouter
Heartbeat
PackageJobState
TestRunner file-bus path
existing error codes
README installation paths
```

---

# 26. Milestone dependency graph

```text
M0 Rules and baseline
 |
 v
M1 Structural schema validity
 |
 v
M2 Action contracts and error taxonomy
 |
 v
M3 Safety and profiles
 |
 v
M4 Canonical catalog endpoint and domain epoch
 |
 v
M5 Go registry, cache, and validation
 |
 v
M6 Typed CLI
 |
 v
M7 Operation ledger and retry safety
 |
 v
M8 stdio MCP skeleton
 |
 v
M9 Native Profile tools
 |
 +------> M10 Compact and Full exposure
 |                 |
 v                 v
M11 Approval/MRTR  M13 Catalog invalidation
 |
 v
M12 Tasks bridge
 |
 v
M14 Result resources
 |
 v
M15 Telemetry and benchmark
 |
 v
M16 Documentation and release hardening
 |
 v
M17 Cross-verification and MCP-primary decision
```

M9 may begin only after M7. Do not expose mutating native tools before ledger and policy are active.

---

# 27. Detailed milestone work orders

## M0. Migration authority, rules, and progress ledger

### Goal

Make repository rules truthful before implementation begins.

### Target files

```text
CLAUDE.md
AGENTS.md
AGENT.md
cmd/AGENT.md
cmd/doctor.go
.cursor/rules/hera-agent-unity.mdc
.github/copilot-instructions.md
GEMINI.md
.agents/agents.md
.agents/skills/hera-agent-unity/SKILL.md
tools/sync-agent-guides/*
docs/MCP_MIGRATION_PROGRESS.md
```

### Required changes

1. Replace the blanket MCP prohibition in `CLAUDE.md` with the authorized transitional lock.
2. Add a locked migration section linking this implementation plan and progress file.
3. Record that CLI remains the production default until M17.
4. Establish the canonical rule hierarchy from section 6.
5. Implement deterministic agent-guide generation and `--check`.
6. Eliminate current `AGENT.md` versus `cmd/AGENT.md` drift.
7. Correct `cmd/doctor.go` comments.
8. Add progress-file structure with all milestones initially pending.
9. Do not add user-facing MCP usage instructions yet.

### Tests

```text
go run ./tools/sync-agent-guides --check
go test ./cmd/...
go test ./...
rg old prohibited wording across rule files
hera-agent-unity doctor --agent-rules
```

The installed binary may still show old output. For source verification, build a test binary or test `extractAgentRules` directly. Do not update the installed binary without release instruction.

### Completion gate

- no conflicting MCP prohibition remains,
- generated guides are synchronized,
- old functionality remains documented,
- MCP is described only as planned,
- working tree contains only M0 changes.

### Stop conditions

- generation would erase tool-specific required frontmatter,
- canonical hierarchy cannot be made deterministic,
- unrelated rule content changes.

### Suggested commit

```text
docs(architecture): authorize CLI plus MCP adapter migration
```

---

## M1. Structural JSON Schema validity

### Goal

Make every current tool schema structurally valid without changing handler behavior.

### Target files

```text
AgentConnector/Editor/Core/ToolMetadata.cs
AgentConnector/Editor/Core/SchemaUtility.cs
AgentConnector/Editor/ToolDiscovery.cs
AgentConnector/Editor/Tests/ToolDiscoveryTests.cs
```

### Required changes

1. remove property-level boolean `required`,
2. preserve object-level required arrays,
3. add array `items`,
4. add correct nullable handling,
5. fail unsupported type mapping instead of silently using string,
6. canonicalize object property ordering,
7. retain current external response fields for compatibility.

Do not introduce strict unknown-property rejection yet.

### Tests

Add structural recursive assertions over every runtime tool schema.

Required evidence:

```text
property-level required boolean count = 0
invalid runtime schema count = 0
tool names unchanged
action names unchanged
```

Run live Unity compile and `HeraAgent/Tests/ToolDiscovery` when a suitable Editor has the local Connector source.

### Rule-document impact

Update `CLAUDE.md` completed ledger only after the tests pass. Regenerate agent guides only if usage rules changed.

### Suggested commit

```text
fix(schema): emit structurally valid tool contracts
```

---

## M2. Action contracts and validation taxonomy

This milestone is divided into work units. Complete one unit per Codex session.

### M2.1 Read/query tools

Migrate:

```text
describe_shader
describe_type
find_gameobjects
find_method
game_feel
list_assemblies
ui_slop
unity_docs
console read path
scene info/list
menu list
```

### M2.2 Scene and object mutation tools

Migrate:

```text
scene load/save/close
manage_gameobject
manage_components
manage_editor
screenshot
input
```

### M2.3 Asset and UI tools

Migrate:

```text
manage_assets
manage_asset_import
manage_material
manage_prefab
manage_animation
manage_ui
ui_doc
reserialize
refresh_unity
detect_assets
```

### M2.4 Package, test, profiler, and raw tools

Migrate:

```text
manage_packages
run_tests
profiler
log
exec
menu execute
```

### Required changes per tool

1. add action contracts or a strict default contract,
2. add aliases that handlers actually accept,
3. model complex values with nested schema or `SchemaJson`,
4. specify required fields per action,
5. specify conflicts and alternatives,
6. add output schema where practical,
7. switch `ContractMode` to strict only when its tests pass,
8. remove hidden handler-only aliases or declare them,
9. return `UNKNOWN_ACTION` for explicit invalid action.

### Tests per tool

- minimum valid input,
- every action,
- missing required,
- wrong type,
- unknown property,
- unknown action,
- alias,
- mutually exclusive targets,
- output shape.

### Completion gate

All built-in tools have explicit contracts. No built-in remains legacy after M2.4.

### Suggested commits

```text
feat(contracts): type read and query tool inputs
feat(contracts): type scene and object operations
feat(contracts): type asset and UI operations
feat(contracts): type package test and raw operations
```

---

## M3. Safety classification and profiles

### Goal

Give every built-in operation an enforced safety class and profile membership.

### Target files

```text
HeraToolAttribute.cs
HeraActionAttribute.cs
new safety/contract Core files
all built-in tool declarations
ToolDiscoveryTests.cs
CLAUDE.md
```

### Required changes

1. add risk enum and fields,
2. normalize legacy booleans,
3. define parameter-dependent safety,
4. classify every built-in operation,
5. create profile metadata,
6. exclude arbitrary-code operations from normal profiles,
7. make unspecified built-in safety a catalog failure,
8. add conservative MCP annotation mapping tests.

### Completion gate

```text
unclassified built-in tools/actions = 0
normal-profile arbitrary-code operations = 0
profile validation failures = 0
```

### Suggested commit

```text
feat(safety): classify operations and define MCP profiles
```

---

## M4. Canonical catalog, hash, and domain epoch

### Goal

Return the full normalized contract in one Connector request.

### Target files

```text
ToolContractModels.cs
ToolContractRegistry.cs
ToolContractCanonicalJson.cs
ToolDiscovery.cs
CommandRouter.cs
Heartbeat.cs
internal/client/types.go
tests
```

### Required changes

1. add `list` catalog mode,
2. emit catalog envelope,
3. compute deterministic hash,
4. add project fingerprint,
5. add domain epoch and feature capabilities to heartbeat,
6. add catalog snapshot tests,
7. ensure old list modes remain byte-shape compatible.

### Tests

```text
TestCatalogOrderIsDeterministic
TestCatalogHashStableForEquivalentContracts
TestCatalogHashChangesForContractChange
TestCatalogExcludesVolatileFieldsFromHash
TestHeartbeatDomainEpochChangesAfterReload
TestLegacyListShapesRemainCompatible
```

### Completion gate

One HTTP request returns a validated 31-tool catalog with stable hash.

### Suggested commit

```text
feat(connector): expose normalized tool catalog
```

---

## M5. Go registry, cache, and validation

### Goal

Consume the Connector catalog without depending on `cmd`.

### Target files

```text
internal/toolregistry/*
internal/schema/*
internal/client/*
tools/validate-tool-catalog/*
go.mod
go.sum
```

### Required changes

1. add provider interface,
2. add catalog-v1 Unity provider,
3. add legacy fallback provider,
4. add memory/disk cache,
5. compile input and output schemas,
6. add deterministic profile selection,
7. add dependency review record,
8. add fixture and live integration tests.

### Completion gate

- Go validates every strict schema,
- cache survives a second process,
- stale/corrupt cache is rejected,
- older Connector enters Compact-only degraded mode,
- no `cmd` import.

### Suggested commit

```text
feat(registry): add cached Go tool contract provider
```

---

## M6. Typed CLI

### Goal

Add schema-validated JSON invocation while preserving legacy commands.

### Target files

```text
cmd/call.go
cmd/root.go
cmd/dispatch.go
internal/schema
internal/toolregistry
internal/policy skeleton
help docs and tests
```

### Required changes

1. add `call`,
2. isolate config from global flags,
3. normalize legacy invocation through the shared path where safe,
4. add validate-only and explain modes,
5. preserve compact output behavior,
6. add completion-generation entry point if scoped to this milestone,
7. document quoting-free stdin usage.

### Tests

```text
TestCallJSON
TestCallStdin
TestCallFile
TestCallRejectsMultipleSources
TestCallRejectsUnknownArgumentBeforeHTTP
TestLegacySceneCommandStillWorks
TestLegacyParamsPrecedence
TestTypedAndLegacyProduceEquivalentRequest
```

### Completion gate

Typed CLI works for all strict built-ins. Existing CLI tests remain green.

### Suggested commit

```text
feat(cli): add schema-validated JSON tool invocation
```

---

## M7. Connector operation ledger and safe retry

### Goal

Eliminate duplicate non-idempotent execution ambiguity.

### Target files

```text
HttpServer.cs
CommandRouter.cs
OperationLedger.cs
AtomicFile.cs
Heartbeat.cs
internal/client/types.go
internal/client/transport.go
internal/client/reload_retry.go
tests
```

### Required changes

1. add request metadata,
2. generate operation IDs in Go,
3. add Connector request context,
4. persist ledger before execution,
5. persist response before HTTP write,
6. replay committed responses,
7. conflict on changed arguments,
8. return unknown instead of re-executing,
9. capability-gate retry for old Connectors,
10. add retention cleanup.

### Tests

```text
TestOperationReplayReturnsStoredResponse
TestOperationConflictRejectsDifferentArguments
TestCommittedResponseSurvivesResponseLoss
TestPriorDomainRunningBecomesUnknown
TestNonIdempotentUnknownDoesNotInvokeHandler
TestIdempotentRetryUsesSameOperationID
TestLegacyConnectorDisablesMutationRetry
TestLedgerAtomicWriteFallback
```

### Ultra verification

Use a disposable Unity fixture. Simulate a response-loss or reload path and verify exactly one mutation.

### Completion gate

No tested non-idempotent operation executes twice under response loss or reload.

### Suggested commit

```text
feat(reliability): add operation ledger and replay-safe retries
```

---

## M8. stdio MCP skeleton

### Goal

Start an official-SDK stdio server without exposing Unity tools yet.

### Target files

```text
cmd/mcp.go
internal/mcpserver/config.go
internal/mcpserver/server.go
internal/mcpserver/discovery.go
go.mod
go.sum
tests
```

### Required changes

1. pin official SDK,
2. add `mcp` standalone command,
3. disable terminal output on stdout,
4. expose server identity and discovery,
5. support graceful shutdown,
6. add feature flag default off,
7. run protocol conformance smoke tests.

### Tests

```text
TestMCPStdoutContainsOnlyProtocolFrames
TestMCPStderrMayContainDiagnostics
TestMCPGracefulEOF
TestMCPContextCancellation
TestMCPUnsupportedTransportRejected
TestMCPFeatureFlag
```

### Completion gate

An MCP inspector/client can discover the server over stdio with zero non-protocol stdout.

### Suggested commit

```text
feat(mcp): add experimental stdio server
```

---

## M9. Native Profile tool bridge

### Goal

Expose fixed profile tools as native MCP tools.

### Target files

```text
internal/mcpserver/native_tools.go
internal/mcpserver/results.go
internal/mcpserver/middleware.go
internal/policy/*
internal/toolregistry/*
tests
```

### Required changes

1. fetch catalog at startup,
2. select profile,
3. register strict tools,
4. validate input,
5. enforce policy,
6. assign operation ID,
7. call shared internal client,
8. map results,
9. map annotations conservatively,
10. keep profile fixed.

Begin with `core` read-only operations, then add writes after policy and ledger tests are proven.

### Tests

```text
TestProfileRegistersExpectedTools
TestProfileOrderingStable
TestNativeToolValidatesBeforeUnity
TestNativeToolPreservesHeraErrorCode
TestNativeMutationUsesOperationID
TestExecAbsentFromNormalProfiles
```

### Completion gate

All seed profiles register exactly their expected strict tool sets.

### Suggested commit

```text
feat(mcp): bridge native profile tools to Unity
```

---

## M10. Compact and Full exposure

### Goal

Support dynamic discovery without forcing the full catalog into every model call.

### Target files

```text
internal/mcpserver/compact_tools.go
internal/mcpserver/profiles.go
tests
docs/MCP.md
```

### Required changes

1. implement search/describe/call,
2. add deterministic lexical rank,
3. support legacy custom tools in Compact,
4. add Full-safe profile,
5. add Advanced gated profile,
6. preserve operation/policy behavior.

### Completion gate

- Compact can discover and call a dynamic custom tool.
- Full-safe contains all strict policy-allowed tools.
- Advanced cannot start without explicit arbitrary-code permission.

### Suggested commit

```text
feat(mcp): add compact discovery and opt-in full exposure
```

---

## M11. Approval and MRTR

### Goal

Require user authorization for destructive, package, and arbitrary operations.

### Target files

```text
internal/policy/*
internal/mcpserver/middleware.go
cmd/call.go
Connector policy validation
tests
```

### Required changes

1. deterministic preflight,
2. signed or MAC-protected approval token using a process-local or protected local secret,
3. token binding and expiry,
4. CLI TTY flow,
5. non-interactive fallback,
6. MCP negotiated flow,
7. Connector revalidation.

Do not store a long-lived secret in the repository.

### Tests

```text
TestDestructiveOperationCannotRunWithoutApproval
TestDeniedApprovalCausesZeroMutation
TestApprovalBindsArgumentsHash
TestExpiredApprovalRejected
TestApprovalSingleUse
TestMRTRUnsupportedFallback
```

### Completion gate

No destructive benchmark operation mutates state before approval.

### Suggested commit

```text
feat(safety): enforce approval for risky operations
```

---

## M12. Tasks bridge

### Goal

Expose package and test long-running operations as durable tasks when negotiated.

### Target files

```text
internal/taskbridge/*
internal/mcpserver/*
cmd/test.go
cmd/manage_packages.go
internal/poll/*
tests
```

### Required changes

1. generic task state,
2. package adapter,
3. test adapter,
4. Tasks extension integration,
5. fallback blocking behavior,
6. truthful cancellation reporting,
7. adapter restart recovery.

### Completion gate

A package or test task survives the adapter lifecycle whenever its existing file-bus state survives.

### Suggested commit

```text
feat(tasks): bridge durable Unity jobs to MCP
```

---

## M13. Catalog invalidation and list-changed

### Goal

Handle domain reload and custom-tool changes in a long-lived server.

### Required changes

1. observe domain epoch,
2. refetch catalog,
3. validate before swap,
4. atomically replace registry,
5. notify clients,
6. reject calls against removed/stale tools,
7. keep in-flight snapshot stable.

### Tests

```text
TestDomainEpochInvalidatesCatalog
TestSameCatalogHashAvoidsSpuriousChange
TestListChangedOnCustomToolAdd
TestRemovedToolReturnsCatalogStaleOrUnknownTool
TestInFlightCallUsesOriginalSnapshot
```

### Completion gate

A custom tool add/remove after reload is reflected without restarting the MCP process.

### Suggested commit

```text
feat(mcp): refresh tool catalog across domain reload
```

---

## M14. Large result resources

### Goal

Prevent large Unity results from flooding model context.

### Required changes

1. inline byte cap,
2. atomic result spooling,
3. resource handles,
4. retrieval,
5. retention,
6. summary metadata,
7. sensitive-result guard.

### Completion gate

A result over the configured cap is not placed inline and remains retrievable by handle.

### Suggested commit

```text
feat(mcp): spool oversized tool results as resources
```

---

## M15. Telemetry and benchmark harness

### Goal

Measure successful-task economics rather than invocation folklore.

### Target files

```text
internal/telemetry/*
tools/benchmark-mcp/*
benchmark fixtures
docs/benchmark reports
```

### Required event IDs

```text
benchmark_run_id
conversation_id
model_call_id
host_tool_call_id
process_launch_id
mcp_request_id
operation_id
unity_request_id
task_id
```

### Required metrics

- first-attempt success,
- final task success,
- wrong tool/action,
- invalid argument,
- model calls,
- host calls,
- process launches,
- Unity HTTP requests,
- repair calls,
- raw/cached/billed tokens,
- tool-result tokens,
- elapsed time,
- p50/p95,
- duplicate side effects,
- unsafe mutations,
- reload recovery,
- human intervention.

### Completion gate

A reproducible A to E benchmark can be run on a disposable Unity fixture.

### Suggested commit

```text
test(benchmark): add CLI and MCP task-economics harness
```

---

## M16. Documentation, compatibility, and release hardening

### Goal

Make shipped documentation and rules match the implemented experimental feature.

### Required updates

```text
README.md
README.ko.md
docs/INDEX.md
docs/ARCHITECTURE.md
docs/GO_CLI.md
docs/CSHARP_CONNECTOR.md
docs/COMMANDS.md
docs/MCP.md
docs/TROUBLESHOOTING.md
AGENTS.md and generated guides
CLAUDE.md
CHANGELOG.md
```

### Required subjects

- install and client configuration,
- stdio startup,
- profiles,
- Compact fallback,
- approval,
- operation IDs,
- Tasks fallback,
- old Connector degraded mode,
- feature flags,
- troubleshooting stdout contamination,
- version terminology,
- security boundaries.

### Completion gate

No documentation claims MCP is default. All examples pass smoke tests.

### Suggested commit

```text
docs(mcp): document experimental adapter and compatibility
```

---

## M17. Cross-verification and default decision

### Goal

Decide whether MCP remains optional or becomes primary.

### Required evidence

1. full Go verification,
2. Connector compilation,
3. Unity EditMode tests,
4. disposable-fixture integration suite,
5. MCP conformance,
6. A to E benchmark,
7. safety approval tests,
8. response-loss exactly-once test,
9. catalog reload test,
10. agent-guide sync test.

### Proposed decision gates

Typed contract benefit:

- invalid-argument rate decreases by at least 50 percent relative,
- first-attempt success improves by at least 5 percentage points,
- final success is non-inferior within 2 percentage points,
- unsafe mutations do not increase.

MCP versus Typed CLI:

- median dependent model calls decrease by at least 1, or
- first-attempt success improves by at least 3 percentage points, or
- cost per successful task decreases by at least 10 percent,
- with no safety regression.

Profile versus Full:

- final success non-inferior within 2 percentage points,
- raw tool-definition tokens at least 30 percent lower,
- wrong-tool rate no higher.

These are proposed design thresholds, not measured results.

### Final actions

If gates fail:

- keep Typed CLI primary,
- keep MCP experimental or disable it,
- do not rewrite the Connector.

If gates pass:

- user decides whether to make Profile MCP the AI-client default,
- CLI remains supported.

---

# 28. Test matrix

## 28.1 Go unit tests

```text
tool registry parsing and canonical hash
memory and disk cache
corrupt cache rejection
JSON Schema compilation
profile selection
legacy fallback
approval token
policy evaluation
operation metadata
retry behavior
task state
MCP result mapping
stdout purity
feature flags
agent-guide sync
```

## 28.2 Connector tests

```text
ToolDiscovery contract generation
schema structural validity
action resolution
unknown action
alias normalization
safety completeness
profile completeness
catalog hash
domain epoch
request metadata parsing
ledger state transitions
response replay
approval verification
output validation
```

## 28.3 Live Unity integration

Use disposable fixtures for:

- query,
- GameObject creation,
- component mutation,
- UI creation,
- asset mutation,
- Play Mode,
- tests,
- package job,
- domain reload,
- invalid argument repair,
- missing target,
- destructive approve/deny,
- batch,
- custom tool reload.

Never use a production project as a destructive benchmark fixture.

## 28.4 Full verification commands

Use repository-approved commands and installed tools. At minimum:

```text
gofmt
go vet ./...
go build ./...
go test -count=1 ./...
golangci-lint run ./...
agent-guide sync --check
catalog validation
```

When Connector code changes:

```text
Unity compile
console errors = 0
relevant HeraAgent tests
fixture cleanup
changed target reread
```

Do not use a successful Go test to claim Connector correctness.

---

# 29. Feature flags and rollback

Initial flags:

```text
HERA_MCP_ENABLED=0
HERA_MCP_PROFILE=core
HERA_MCP_EXPOSURE=profile
HERA_STRICT_SCHEMA=0
HERA_OPERATION_LEDGER=0
HERA_MCP_TASKS=0
HERA_MCP_MRTR=0
HERA_MCP_MAX_INLINE_BYTES=131072
HERA_MCP_TELEMETRY=0
```

Rules:

- flags default to legacy-safe behavior until their milestone completes,
- tests cover on and off states,
- no flag silently disables approval for a risky operation,
- removing a flag requires a separate migration milestone,
- rollback must not require downgrading the Unity project.

Rollback order:

1. disable MCP,
2. retain Typed CLI,
3. disable strict mode only for declared legacy tools,
4. disable Tasks extension and use blocking wrappers,
5. retain Connector ledger if already shipped,
6. revert SDK version if protocol regression is isolated.

---

# 30. Versioning and release boundaries

CLI and Connector versions remain independent.

| Change | Version action |
|---|---|
| Go-only registry, CLI, MCP | CLI release tag when explicitly released |
| C# schema, catalog, ledger, heartbeat | Connector package version bump |
| Both | bump both independently |
| Docs only | no automatic version bump |
| Feature flag experimental | document status, do not imply default |

Do not call a Connector package version `vX.Y.Z`.

Do not tag or push merely because a milestone is complete.

---

# 31. Definition of Done

The migration implementation is complete only when all are true:

## Contracts

- [ ] 31 baseline built-ins and any new built-ins are strict.
- [ ] every input schema passes JSON Schema 2020-12 meta-validation.
- [ ] action enums/branches are complete.
- [ ] aliases match handler behavior.
- [ ] output schemas exist where structured output is claimed.
- [ ] catalog hash is deterministic.

## Safety

- [ ] no built-in safety is unspecified.
- [ ] risky operations require enforced approval.
- [ ] normal profiles exclude arbitrary code.
- [ ] approval denial produces zero mutation.
- [ ] unknown outcome never auto-reexecutes non-idempotent work.

## Reliability

- [ ] operation replay survives response loss.
- [ ] domain reload invalidates catalog.
- [ ] task state survives supported reload/restart paths.
- [ ] cancellation claims match actual behavior.
- [ ] queues and caches are bounded.

## Interfaces

- [ ] legacy CLI remains compatible.
- [ ] Typed CLI works without shell-escaped nested JSON.
- [ ] stdio MCP emits protocol-only stdout.
- [ ] Profile, Compact, Full-safe, and Advanced behavior is tested.
- [ ] old Connector enters documented degraded mode.

## Rules and docs

- [ ] `CLAUDE.md` contains the final lock.
- [ ] `AGENTS.md` matches shipped behavior.
- [ ] generated `AGENT.md` and `cmd/AGENT.md` are synchronized.
- [ ] derived agent files pass sync check.
- [ ] README and docs contain no unimplemented claims.
- [ ] no machine-specific absolute path is committed.

## Verification

- [ ] Go verification passes.
- [ ] Connector compiles with zero errors.
- [ ] relevant Unity tests pass.
- [ ] benchmark harness is reproducible.
- [ ] benchmark decision is recorded.
- [ ] working tree contains no temporary fixture or secret.

---

# 32. Codex execution protocol

Use this loop for every work unit.

## 32.1 Before editing

```text
1. Read AGENTS.md.
2. Read CLAUDE.md.
3. Read this implementation document.
4. Read docs/MCP_MIGRATION_PROGRESS.md.
5. Run git branch, HEAD, and status checks.
6. Inspect target files and their tests.
7. Confirm the prior work-unit gate is complete.
```

## 32.2 During editing

```text
1. Modify only the current work-unit files.
2. Prefer small types and pure functions.
3. Preserve existing public behavior unless the work unit explicitly changes it.
4. Add tests with the implementation.
5. Add .meta files for new Connector files/folders.
6. Do not hide a failure with permissive fallback.
7. Do not update completion ledgers before tests pass.
```

## 32.3 Verification order

```text
1. formatter
2. narrow unit tests
3. package tests
4. full Go tests
5. Connector compile if affected
6. Unity console error check
7. relevant Unity tests
8. reread changed state
9. git diff review
10. rule/document sync check
```

## 32.4 Completion report

Use:

```text
Milestone/work unit:
Baseline commit:
Files changed:
Contract changes:
Compatibility behavior:
Tests run:
Unity evidence:
Rule documents updated:
Remaining risks:
Completion gate: PASS | FAIL | BLOCKED
Suggested next unit:
```

Do not include hidden reasoning. Include verifiable evidence.

## 32.5 Progress file update

`docs/MCP_MIGRATION_PROGRESS.md` must record:

```text
status
commit
date
implemented scope
tests
known limitations
rollback
next prerequisite
```

Allowed status:

```text
PENDING
IN_PROGRESS
PASS
BLOCKED
ROLLED_BACK
```

---

# 33. Codex kickoff prompts

## M0 kickoff

```text
Implement only M0 from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Read AGENTS.md and CLAUDE.md first. The user-authorized CLI + MCP adapter
migration supersedes only the old blanket MCP prohibition. Do not add MCP
runtime code in M0. Establish the canonical rule hierarchy, create deterministic
agent-guide synchronization, add docs/MCP_MIGRATION_PROGRESS.md, run the M0
tests, and stop at the M0 completion gate. Do not push or tag.
```

## Generic next milestone

```text
Implement only <WORK_UNIT> from docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md.

Verify the prior completion gate in docs/MCP_MIGRATION_PROGRESS.md. Inspect the
current code instead of trusting stale line numbers. Modify only the declared
scope, add the required tests, update affected rule and documentation sources,
run the work-unit verification, update the progress file, and stop. Do not begin
the next work unit. Do not push or tag.
```

## Cross-review prompt

```text
Review <WORK_UNIT> against docs/CODEX_MCP_MIGRATION_IMPLEMENTATION.md without
changing code first. Check contract drift, compatibility, safety classification,
domain-reload behavior, duplicate side effects, tests, and rule-document sync.
Report blockers with exact files and symbols. Apply fixes only after the review
list is complete, then rerun the declared gate.
```

---

# 34. References

Normative protocol sources:

- MCP 2026-07-28 announcement  
  `https://blog.modelcontextprotocol.io/posts/2026-07-28/`
- MCP 2026-07-28 changelog  
  `https://modelcontextprotocol.io/specification/2026-07-28/changelog`
- MCP Tools  
  `https://modelcontextprotocol.io/specification/2026-07-28/server/tools`
- MCP server discovery  
  `https://modelcontextprotocol.io/specification/2026-07-28/server/discover`
- MCP stdio transport  
  `https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/stdio`
- MCP Tasks  
  `https://modelcontextprotocol.io/extensions/tasks/overview`
- Official Go SDK  
  `https://github.com/modelcontextprotocol/go-sdk`

Repository design sources:

```text
CLAUDE.md
AGENTS.md
docs/ARCHITECTURE.md
docs/GO_CLI.md
docs/CSHARP_CONNECTOR.md
AgentConnector/Editor/HttpServer.cs
AgentConnector/Editor/CommandRouter.cs
AgentConnector/Editor/ToolDiscovery.cs
AgentConnector/Editor/Heartbeat.cs
internal/client/
internal/poll/
```

---

# 35. Final implementation constraint

The migration succeeds by improving contracts and adding an adapter around the existing engine.

It fails if it creates two engines, two registries, two safety policies, or two incompatible truths.

The required invariant is:

```text
one contract registry
one validation meaning
one policy meaning
one operation identity
one Unity execution core
two compatible interfaces: CLI and MCP
```
