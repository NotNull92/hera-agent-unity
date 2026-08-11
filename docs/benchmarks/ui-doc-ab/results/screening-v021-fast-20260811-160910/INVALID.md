# Invalid fast wave

This wave is retained as raw infrastructure evidence only and must not be used
for M2–M5.

Terminal reason: `T03/uidoc/rep-01` produced no `hera-calls.jsonl`, so the fast
runner rejected the cell and stopped after its single permitted attempt.

Root cause confirmed after the run: `Run-One.ps1` left child standard input at
the Windows `ks_c_5601-1987` default. The frozen T03 prompt contains `◆` and
`·`; Codex rejected the resulting non-UTF-8 stdin before it could start an
agent turn or invoke the arm shim. The harness now explicitly uses UTF-8 for
redirected stdin and has a regression test. All five run records remain
ineligible because the wave did not reach `fast_complete`.
