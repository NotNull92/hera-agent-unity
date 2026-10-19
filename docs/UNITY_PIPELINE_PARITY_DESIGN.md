# Unity CLI and Pipeline parity design

Status: approved on 2026-08-13. Implementation approach: evidence-gated parity.

## Goal

Re-audit the complete Hera Go CLI and Unity Editor connector, compare their
actual behavior with the public command surface of Unity CLI and
`com.unity.pipeline`, fix existing defects first, then add only the missing
Editor workflows that fit Hera's established architecture and can be verified
on the supported Unity 6 matrix.

The comparison baseline is Unity CLI `1.0.0-beta.3` and
`com.unity.pipeline` `0.5.0-exp.1`. The source inventory contains 153 public
Pipeline commands after excluding eight internal test commands. The pre-change
Hera catalog contains 33 tools and 111 actions.

Official references:

- <https://docs.unity.com/en-us/unity-cli/unity-cli-reference>
- <https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package>
- <https://docs.unity.com/en-us/unity-production-pipeline/overview>

## Architecture boundary

Hera remains a thin Go CLI driving a single selected, running Unity Editor over
loopback HTTP. Unity mutation and optional-package reflection stay in the C#
connector. Long operations retain the existing reload-durable result-file and
operation-ledger patterns. This work does not replace the CLI with MCP, add a
second runtime/player server, or introduce generic detached jobs.

The following official CLI areas are host-management concerns and are not Hera
parity targets: Hub/editor installation, modules, licensing, templates, cloud
services, shell completion, and standalone process management. The following
Pipeline commands remain deliberate duplicates of existing safer surfaces:
filesystem text/script writes, `eval_file`, bulk GameObject creation, prefab
save sessions, target frame rate, and timescale.

Locked decisions remain locked:

- no active build-target switch or Build Profile orchestration;
- no Unity Search surface;
- no package `resolve`, editor `autotick`, or `quit` command;
- no CLI/connector version handshake;
- no bidirectional streaming or arbitrary in-memory job registry;
- no runtime hot-reload/file-watcher server.

## Admission rule

A candidate ships only when all of these are true:

1. It answers an Editor workflow that the existing dedicated tools cannot
   express without handwritten `exec` or direct filesystem work.
2. The Unity API is public, or an optional integration can fail closed through
   the repository's established reflection pattern.
3. Input, output, safety, approval, Undo/dirty/save, reload, and retry contracts
   can be stated precisely.
4. A failing-first regression or feature test exists at the narrowest truthful
   boundary.
5. The behavior compiles and runs on the representative Editor of the supported
   range (`6000.6.6f1` today), and on any additional representative the change
   crosses.
6. Catalog growth remains bounded and the public guides are synchronized.

Project Auditor is conditional: the 6000.5 and 6000.6 representatives expose the
built-in module, but no current fixture contains a usable rules package. It does
not ship until a positive audit result can be exercised; absence-only behavior is
not enough.

## Work waves

### Wave 0: evidence and baseline

- Produce a generated inventory and a coverage ledger for every in-scope Go,
  C#, test, installer, workflow, and contract document.
- Read every ledger entry and trace commands, actions, shared helpers, state
  channels, and public response contracts.
- Record the 153-row official Pipeline comparison as covered, missing,
  duplicate, architecture-expanding, rejected, or conditional.
- Preserve the Windows guide-generation fix: `.mdc` and `.gitattributes` are
  always LF so a clean checkout passes the existing drift test.

### Wave 1: confirmed defects

- Apply `development`, `allow_debugging`, and `build_scripts_only` to the
  actual `BuildPlayerOptions.options` used by `build start`.
- Route MCP `tasks/cancel` for test tasks through the connector's existing
  `run_tests cancel` action. Package cancellation stays explicitly unsupported.
- Prove whether a no-cursor console read returns the oldest entries; if the red
  test confirms it, return the newest bounded window while preserving forward
  `since` pagination.

### Wave 2: low-risk Editor parity

- `scene`: create, set active, and save all loaded scenes.
- `manage_gameobject`: set position/rotation/scale as one transform mutation,
  plus tag and layer mutation.
- `manage_animation`: add Animator layers and remove curves.
- `manage_settings`: graphics render pipeline, legacy Input Manager axes,
  lighting settings, and Navigation settings with get/set, dry-run, approval,
  and truthful recompile/reload reporting.
- `describe_shader`: list shaders with a bounded filter and limit.
- `manage_editor`: focus a supported Editor window without inventing OS-level
  click guarantees.

### Wave 3: optional/editor surfaces

- Timeline creation, inspection, track creation, and clip creation use
  reflection so the connector keeps no Timeline dependency.
- Editor UI element capture is metadata-first and bounded. Runtime/player UI
  capture remains outside the Editor-only connector.
- Project Auditor ships only if a rules-enabled fixture provides a positive
  path during this work.

### Wave 4: cleanup and synchronization

- Run structural, adversarial, and omission passes across the full coverage
  ledger.
- Remove only proven unreachable code, delegation-only wrappers, duplicate
  parsers, or stale contracts. Intentional duplication across the file-bus,
  operation ledger, platform installers, or public compatibility paths stays.
- Regenerate tool contracts and catalog metrics, synchronize help, command
  docs, English/Korean README files, agent guides, changelog, handoff, and
  package version.

## Rebaseline: duplicate rows and the payload metric (2026-10-17)

Recorded with CLI `v0.3.1` / Connector `0.2.0`. This rebaseline adds no tool and
no action: the catalog stays at **34 tools / 133 actions**, inside the 145-action
budget.

- **Duplicate rows point at the exact primitive.** Nine rows keep `exec` as their
  sanctioned answer and now name the call (`exec --file` for `eval_file`,
  `PrefabUtility.SaveAsPrefabAsset` for `save_prefab_contents`,
  `Application.targetFrameRate` for `set_target_framerate`, and so on). Three rows
  are reclassified `covered` because the audit undercounted the catalog:
  `import_asset` → `manage_assets import` and `rename_asset` →
  `manage_assets move` shipped in Connector `0.1.3`, and `set_timescale` →
  `manage_settings set_time` (`time_scale`) already existed. Tally: **129 covered,
  9 duplicate**, 7 rejected, 6 excluded, 2 conditional. Nothing here changes the
  admission rule: a workflow `exec` already expresses is not a candidate for a
  new action.
- **The retired MCP measurement is replaced by a CLI one.** The payload report
  dropped the `mcp_compact` block with the adapter and now reports `cli_summary`:
  the `list --names` array plus the machine-independent compact agent rules
  (`doctor --agent-rules --compact` at the documented default mode; local asset
  opt-ins are deliberately excluded so a checked-in baseline stays reproducible).
- **Measured budgets:** `cli_summary` 2,754 B (name list 477 + rules 2,277) against
  3,000; `agent_rules_compact_bytes` 2,277 against 3,500; normalized catalog
  268,417 B (262.1 KiB) unchanged; tool descriptions 9,940 characters against
  10,000; profiles unchanged. `--fail-on-change` accepts the regenerated baseline,
  and the only delta against the previous baseline is the new metric itself.
- **Fixtures:** M2 itself changed no connector code and was measured on the then
  representative `6000.5.6f1`. The support floor then moved to `6000.6` (see the
  ledger), and the Connector evidence for this release comes from `6000.6.6f1`.

## Verification and rollback

Every behavioral task follows red, green, refactor. Each task is independently
reversible and must be green before the next begins. The final gates are:

```text
go clean -testcache
gofmt -w .
golangci-lint run ./...
golangci-lint fmt --diff
go vet ./...
go test -race -shuffle=on -count=1 ./...
go run ./tools/validate-connector-package
go run ./tools/validate-tool-catalog < captured live catalog
```

Connector verification uses the exact-source compiler and package-test runner
for the representative of the supported range (`6000.6.6f1` today), then the
installed CLI against that live fixture. Each mutation is tested on disposable
scenes/assets and cleans up its own fixture. Console errors must be zero after
every compile and at the final gate.

No commit, tag, publish, package install, or external write is part of this
implementation unless separately requested.
