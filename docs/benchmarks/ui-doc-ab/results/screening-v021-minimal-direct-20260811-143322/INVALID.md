# Invalid benchmark wave

Wave: `screening-v021-minimal-direct-20260811-143322`

Status: **INVALID — do not use any result in M2–M5.**

The runner reached `T03 / uidoc / rep-01 / attempt-01`, where all three
attempts produced invalid records with zero Hera calls and agent runtimes of
about 90 ms. `Run-Screening.ps1` then exited with `No valid run after 3
attempt(s): task=T03 arm=uidoc rep=1`.

The user requested that the slow protocol stop and be redesigned. At
invalidation, the shared fixture Unity process was gone and the Scene Recovery
backup count was zero. Six valid records and three invalid records remain as
raw evidence only; no subset may be used for M2–M5.
