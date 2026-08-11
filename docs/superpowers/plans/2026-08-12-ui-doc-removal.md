# ui_doc Complete Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the `ui_doc` pipeline while preserving neutral ScreenSpaceOverlay visual capture through `screenshot`.

**Architecture:** Delete the dedicated CLI and Connector document-authoring surfaces rather than leaving compatibility aliases. Move only the overlay renderer into the existing screenshot tool, then make generic uGUI tools the sole supported authoring path. Keep historical benchmark records as evidence but remove active runner code that depends on the retired tool.

**Tech Stack:** Go CLI, C# Unity Editor connector, NUnit, PowerShell benchmark tooling, Markdown documentation.

## Global Constraints

- Preserve the current dirty benchmark artifacts and do not change a user Unity project or TMP importer settings.
- Do not add a top-level capture tool; extend the existing `screenshot` contract.
- `ui_system=uitk` and the bundled version-specific UI Toolkit schema path are intentionally removed.
- No compatibility alias for `ui_doc` or `html-to-uidoc`.
- C# changes require live Unity compile and console-error evidence; unavailable compatibility buckets are recorded as blocked.
- Do not commit or push unless the user explicitly requests it.

---

### Task 1: Lock the retired-surface contract with red tests

**Files:**
- Modify: `cmd/help_test.go`, `cmd/doctor_test.go`, `cmd/legacy_approval_test.go`
- Modify: `AgentConnector/Editor/Tests/ToolDiscoveryTests.cs`, `ToolProfileTests.cs`, `ToolContractTests.cs`, `ToolSafetyExpectations.cs`

**Interfaces:**
- Consumes: the current `ui_doc` and `html-to-uidoc` command/tool contracts.
- Produces: tests that require those commands to be absent and require the neutral screenshot overlay action to be discoverable.

- [ ] Change the Go help and doctor expectations from presence of `ui_doc`/`html-to-uidoc` to their absence and from `ui_doc capture` to the screenshot overlay example.
- [ ] Run the affected Go tests and confirm they fail only because the retired command remains registered.
- [ ] Change the C# discovery/profile/contract/safety expectations to remove the five `ui_doc` actions and include the screenshot overlay contract.
- [ ] Run the focused Unity test filter and confirm it fails only because `ui_doc` still exists.

### Task 2: Remove CLI authoring and conversion surfaces

**Files:**
- Delete: `cmd/ui_doc.go`, `cmd/html_to_uidoc.go`, `cmd/html_to_uidoc_test.go`, `cmd/help/ui_doc.txt`, `cmd/help/html-to-uidoc.txt`
- Modify: `cmd/dispatch.go`, `cmd/help/general.txt`, `cmd/help_test.go`, `cmd/doctor_agent_rules.go`, `cmd/doctor_test.go`, `cmd/legacy_approval_test.go`

**Interfaces:**
- Consumes: `Execute()` standalone and Unity command routing.
- Produces: neither `ui_doc` nor `html-to-uidoc` reaches the Connector; help has no topic for either command.

- [ ] Delete the CLI handlers and help topic files.
- [ ] Remove the two dispatcher cases and every help/doctor/approval test expectation tied to the retired surfaces.
- [ ] Run `go test ./cmd/...` and the full Go test suite; format changed Go sources.

### Task 3: Replace overlay capture and delete the Connector pipeline

**Files:**
- Modify: `AgentConnector/Editor/Tools/EditorScreenshot.cs` and its tests/contracts
- Modify: `AgentConnector/Editor/Tools/ManageUI.cs`, `AgentConnector/Editor/Core/HeraSettings.cs`, `AgentConnector/Editor/HeraAgentAssetConfigWindow.*.cs`, `AgentConnector/Editor/Tools/UiSlop.cs`, and affected settings tests
- Modify: `cmd/asset_config.go`, `internal/assetconfig/config.go`, `internal/assetconfig/json.go`, and their tests
- Delete: `AgentConnector/Editor/Tools/UiDoc.cs`, `UiDoc.Capture.cs`, `AgentConnector/Editor/Core/UiDocSchema.cs`, `UiDocFixer.cs`, `UiBackendSelection.cs`, `UiToolkitDocument.cs`, `UiToolkitFixer.cs`, `UiToolkitStore.cs`, the `uitk_schema_*` data bundles, and the dedicated ui_doc/UI Toolkit tests that have no remaining consumer

**Interfaces:**
- Consumes: the existing `screenshot` tool contract and its output path safety policy.
- Produces: `screenshot --overlay` (or the existing command’s equivalent action parameter) renders ScreenSpaceOverlay canvases to PNG; `manage_ui` supports uGUI only.

- [ ] Add the failing screenshot overlay test and run it to prove the renderer is not yet available through `screenshot`.
- [ ] Move the bounded overlay rendering implementation from the retired partial `UiDoc` class into `EditorScreenshot`, retaining its output-path and overwrite safeguards.
- [ ] Remove the document IR, UI Toolkit schema maintenance path, all now-unreachable `manage_ui` UITK branches, and the `ui_system` selector from the Connector, CLI, settings window, and persisted-config model. Existing config files are not written during this removal.
- [ ] Remove strict contract/profile/discovery/safety declarations for `ui_doc` and add the screenshot overlay declaration.
- [ ] Run the affected Unity tests, refresh the live Editor, and confirm the Console contains no errors.

### Task 4: Remove live documentation and active benchmark machinery

**Files:**
- Modify: `AGENTS.md`, `README.md`, `README.ko.md`, `docs/COMMANDS.md`, `docs/INDEX.md`, `docs/CSHARP_CONNECTOR.md`, `docs/GO_CLI.md`, `docs/metrics/catalog-payload-baseline.json`, `CHANGELOG.md`, `docs/handoffs/ACTIVE.md`, and `tools/build-uitk-schema/` references
- Delete: `docs/UI_DOC_IR.md`, `tools/html-to-uidoc/`
- Delete or retire: active `tools/benchmark-ui-authoring/` runner and test sources that require a `uidoc` arm
- Regenerate: `AGENT.md`, `cmd/AGENT.md`, `.cursor/rules/hera-agent-unity.mdc`, `.agents/skills/hera-agent-unity/SKILL.md`, and other generated guide derivatives

**Interfaces:**
- Consumes: source agent guidance and the live tool catalog.
- Produces: current documentation names generic UI authoring and screenshot overlay capture; historic benchmark evidence remains unedited.

- [ ] Remove live usage guidance, examples, command catalog rows, and schema references.
- [ ] Remove the obsolete A/B runner source while leaving `docs/benchmarks/ui-doc-ab/results/` immutable.
- [ ] Regenerate agent-guide derivatives and run the guide drift check.
- [ ] Regenerate the catalog baseline from a live catalog and record the reduced tool/action footprint.

### Task 5: Verify the removal through real surfaces

**Files:**
- Modify: `docs/handoffs/ACTIVE.md`

**Interfaces:**
- Consumes: installed `hera-agent-unity`, the live disposable Unity Editor, and all changed source/tests.
- Produces: observed absence of retired commands, working generic UI creation, working neutral overlay PNG capture, and a clean Console.

- [ ] Run Go format/lint/test gates and record any unavailable tool as blocked.
- [ ] Run C# namespace ambiguity scans for every changed C# file.
- [ ] Bootstrap the target Editor, refresh/compile, list tools, and verify `ui_doc` is absent.
- [ ] Create a temporary uGUI element only through generic tools, capture it with `screenshot` overlay mode, inspect the PNG, remove the temporary state, and confirm zero Console errors.
- [ ] Attempt the five supported disposable-Editor compile buckets; mark any unavailable bucket blocked instead of passing it.
- [ ] Update the active handoff with exact verification results and remaining blocked evidence.
