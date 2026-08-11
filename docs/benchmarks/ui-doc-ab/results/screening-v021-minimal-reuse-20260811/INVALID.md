# Invalid benchmark wave

Wave: `screening-v021-minimal-reuse-20260811`

Status: **INVALID — do not use any result in M2–M5.**

At `2026-08-11T05:26:01.4131894Z`, `T02 / uidoc / rep-01 / attempt-01`
ended after the last logged Hera call and before `Run-One.ps1` wrote its
required `agent-events.jsonl`, `agent-stderr.txt`, `score.json`, and
`run.json` artifacts. The `Run-Screening.ps1` process group and its shared
fixture Unity process were no longer alive at inspection. The available
PowerShell, Unity, and Windows event evidence does not identify the
cancellation source.

This is an infrastructure/audit failure, not a scored timeout or arm result.
The three completed T01 cells remain preserved as raw evidence only and are
excluded as a group. A fresh uniquely named formal wave must start at cell 1
with the unchanged manifest, arms, model, reasoning effort, and time budget.
