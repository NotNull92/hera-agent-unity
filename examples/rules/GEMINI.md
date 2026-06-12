# hera-agent-unity — Project Rule (AntiGravity)

`hera-agent-unity` is a CLI that drives the **running Unity Editor** over localhost HTTP — execute C# inside the Editor, read the real console, drive Play Mode, inspect live types. For any Unity Editor task in this project, **reach for it first** instead of guessing APIs from training data.

> AntiGravity reads this `GEMINI.md` with **higher priority than `AGENTS.md`**. The full cross-tool guide lives in [`AGENTS.md`](AGENTS.md); this file only adds the AntiGravity-specific rules. Don't duplicate — read both.

## Bootstrap on discovery

When the user says anything like "find hera-agent-unity", "hera-agent-unity 찾아봐", "is it installed?", "check the editor connection", "에디터 붙어있어?", "connect to unity", "unity에 연결해줘" — run this **without asking**:

1. `hera-agent-unity doctor --json`
2. `hera-agent-unity status`
3. `hera-agent-unity list --names`

Report one line: `Connected: <project> · port=<N> · unity=<version> · state=<state> · tools=<N>`. If any step fails, surface the error verbatim and stop — don't silently retry.

## AntiGravity-specific notes

- **Terminal/Bash tool only.** hera-agent-unity is a plain shell binary returning JSON — call it through AntiGravity's terminal tool, never as an MCP server. There is no daemon to start; every call is a stateless HTTP round-trip.
- **No parallel calls.** The Unity connector serializes every command on the Editor main thread (a 120s `SemaphoreSlim` lock). Issuing two hera-agent-unity calls concurrently just makes the second wait — run them **sequentially**, or compose them into one `exec` / `batch`.
- **Domain reloads drop the connection.** Any `exec` that recompiles scripts or imports assets triggers a domain reload that kills the in-flight HTTP request (the CLI auto-retries ~5s). For large projects raise the budget: `--timeout 120000` or `HERA_AGENT_TIMEOUT_MS=120000`. Use `editor refresh --compile` when you need compilation finished before continuing.
- **PowerShell quoting trap.** PowerShell `'single quotes'` pass `\"` as a literal `\` → csc `CS1056`. `"double quotes"` eat `$`, backtick, `;`. The reliable pattern is a here-string piped to stdin:

  ```powershell
  @'
  return AssetDatabase.FindAssets("t:Material").Length;
  '@ | hera-agent-unity exec --compact-json
  ```

  bash equivalent: a `<<'EOF'` heredoc or single-quoted `'...'`. Avoid `\"` unless the wrapper is `"..."`.

## Command cheatsheet

| Need                          | Command                                                       |
|-------------------------------|--------------------------------------------------------------|
| Editor state / liveness       | `hera-agent-unity status`                                     |
| Discover tools                | `hera-agent-unity list --names`                              |
| Run C# in the Editor          | `hera-agent-unity exec "return Application.unityVersion;" --compact-json` |
| Active scene info             | `hera-agent-unity scene info --compact-json`                  |
| Real console errors           | `hera-agent-unity console --type error --compact-json`        |
| Run tests                     | `hera-agent-unity test --mode PlayMode --compact-json`        |
| Drive Play Mode               | `hera-agent-unity editor play --wait` / `hera-agent-unity editor stop` |

## Must-follow rules

- **Default to `return null;`** in `exec` (or omit the return). A verbose status string just spends tokens; the OK response is 3 bytes.
- **Never return a `UnityEngine.Object`** (`Transform`, `GameObject`, …) — they expand to thousands of bytes. Return `new { name, instanceID }` instead.
- **Branch on the `code` field** of error envelopes (`EXEC_COMPILE_ERROR`, `EXEC_RUNTIME_ERROR`, `UNKNOWN_COMMAND`, …), not on the message text.
- **Pass `--compact-json`** on every call so AntiGravity consumes minimal tokens.
- **Trust `--help`.** When this rule contradicts `hera-agent-unity <cmd> --help`, the CLI wins.

## On-demand skill

A reusable skill is available at [`.agents/skills/hera-agent-unity/SKILL.md`](.agents/skills/hera-agent-unity/SKILL.md). Invoke it with `@hera-agent-unity` or "Use the hera-agent-unity skill" for the full command playbook. (`.agents/skills/` is AntiGravity's current default; older builds read the legacy `.agent/skills/` path.)
