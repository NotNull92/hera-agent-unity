# UI Authoring A/B Harness

This directory contains benchmark-only infrastructure for the v0.2.1 `ui_doc` authoring accuracy study.

Canonical benchmark protocol:

`docs/benchmarks/ui-doc-ab/README.md`

Active handoff:

`docs/handoffs/ui-doc-ab-reduction-2026-08-11.md`

## Shim

`shim/hera-agent-unity.cmd` is placed first on the child Codex `PATH`.
It invokes `shim/hera-arm.ps1`, which forwards allowed calls to the pinned real Hera executable and writes one JSONL record per invocation.

Required environment variables:

```text
HERA_AB_ARM       uidoc | primitives | primitives_batch
HERA_AB_REAL_CLI  absolute path to the pinned real hera-agent-unity executable
HERA_AB_LOG       output JSONL call-log path
```

The shim exists to enforce the benchmark arm rather than relying on prompt compliance.

Important policy:

- all arms reject arbitrary `exec` UI authoring;
- all arms reject `html-to-uidoc`;
- typed `call` for UI mutation tools is rejected so policy inspection stays deterministic;
- `uidoc` allows `ui_doc apply/export/capture` and rejects generic UI mutation;
- `primitives` rejects `ui_doc` except neutral `capture` and rejects `batch`;
- `primitives_batch` allows generic mutation and `batch`, but parses the complete `--file` batch and rejects nested `ui_doc`/`exec`;
- read/verification commands outside the mutation surfaces are forwarded.

`ui_doc capture` is deliberately common measurement infrastructure during the authoring A/B. If the removal branch wins, that capture capability must migrate to a neutral verification surface before the final no-`ui_doc` rerun.

## Policy smoke

Run:

```powershell
pwsh -File tools/benchmark-ui-authoring/test-shim.ps1
```

The smoke does not need a Unity Editor. It uses the installed Hera binary only for a harmless `version` forwarding check; forbidden cases are rejected before any Unity connection is attempted.

## Next runner work

M2 requires a one-run orchestrator that:

1. resets a disposable Unity 6000.3.5f2 fixture to a blank scene;
2. pins the source-built Hera CLI and local Connector under test;
3. installs this shim first on the child Codex PATH;
4. runs `codex exec --ephemeral --json` with the frozen task prompt and optional reference image;
5. records Codex events and shim call logs;
6. scores the final Unity state out-of-band against the task oracle;
7. saves capture, annotations, Console errors, and `run.json`.

The 27-run formal matrix remains historical protocol only. The approved
execution path is the fast profile below, after its reset, policy, logging, and
scoring smokes pass.

## Approved fast wave

For the approved one-hour protocol, use the existing runner with the locked
profile:

```powershell
pwsh -NoProfile -File tools/benchmark-ui-authoring/Run-Screening.ps1 `
  -Protocol fast `
  -Wave screening-v021-fast-<timestamp> `
  -Model gpt-5.6-sol `
  -ReasoningEffort medium
```

`-Protocol fast` rejects attempts to override its two arms, two repetitions,
four-minute session timeout, single attempt, 53-minute admission cutoff, or
60-minute deadline. Check the schedule without creating a fixture or opening
Unity via `Run-Screening.ps1 -Protocol fast -PlanOnly`.

Only a `fast_complete` result is comparable. An `invalid` or `incomplete`
result is raw forensic evidence only and cannot select a product branch.
