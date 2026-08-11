# ui_doc Fast A/B Design

## Goal

Measure the practical authoring-accuracy difference between `ui_doc` and the
best generic replacement path in less than one hour, without modifying
production UI code before the measurement selects a branch.

## Scope

The fast protocol compares only these two authoring arms:

- `uidoc`: `ui_doc apply` and `ui_doc export` authoring.
- `primitives_batch`: `manage_ui`, `manage_components`,
  `manage_gameobject`, and validated `batch` authoring.

The unbatched `primitives` arm is excluded. It measures a deliberately slower
generic workflow, not the viable replacement path. Both retained arms keep
the same read/verification capability and use `ui_doc capture` only as neutral
visual measurement infrastructure.

## Frozen measurement protocol

- Unity `6000.3.5f2`, uGUI, `1280x720`, one minimal disposable fixture, and
  one shared warm Unity process per wave.
- Tasks: T01, T02, and T03 from the existing frozen manifest and oracles.
- Matrix: `3 tasks × 2 arms × 2 repetitions = 12 cells`.
- Order by repetition: `uidoc → primitives_batch`, then
  `primitives_batch → uidoc`.
- Each fresh Codex authoring session has a hard 4-minute limit.
- The runner does not start a new cell after minute 53. Reset, scoring, and
  teardown use the remaining seven minutes, keeping the full wave below one
  hour.
- The existing arm shim, fixture reset, fixed asset-config SHA, raw event/call
  logs, out-of-band scoring, capture, Console check, and Scene Recovery check
  remain mandatory.

## Validity and failure handling

A 4-minute authoring timeout is a valid measured result and scores the Unity
state at cutoff. A shim-blocked command is also a valid result.

An infrastructure or audit failure invalidates the entire wave: missing or
malformed `run.json`, zero-call process-start failure, alternate-Hera or MCP
bypass, fixture corruption, shared Unity PID change, frozen asset-config drift,
or nonzero Scene Recovery backups. The runner stops immediately, preserves raw
artifacts, writes the exact invalidation reason, and never selects a favorable
subset.

## Fast decision rule

Each arm has six accepted cells. The fast result is intentionally conservative:

- **Retain pending simplification** when `uidoc` has an overall mean advantage
  of at least 12 points, a per-task mean advantage of at least 15 points, or
  at least two additional strict passes.
- **Reduction candidate** when the absolute overall mean difference is at most
  2 points, every per-task difference is at most 4 points, strict-pass counts
  are identical, and no generic-only critical failure occurs.
- **Inconclusive** for every other outcome. Inconclusive results keep the
  existing production authoring surface; they do not authorize deletion.

A reduction candidate authorizes only the subsequent production reduction
branch and its required no-`ui_doc` smoke. It does not reuse any result from
the two invalid formal waves.

## Expected duration

The hard authoring budget is `12 × 4 = 48` minutes. The fixture warmup,
live resets, scoring, and teardown have a seven-minute reserve. The runner
must abort as incomplete rather than exceed the one-hour wall-clock limit.

## Non-goals

- Do not compare unbatched generic primitives in fast mode.
- Do not change prompts, task oracles, backend, Unity version, model, or
  reasoning effort.
- Do not interpret incomplete or invalid waves as M5 evidence.
- Do not modify production `ui_doc`, UI Toolkit schemas, HTML conversion, or
  capture routing before a valid fast-wave decision.
