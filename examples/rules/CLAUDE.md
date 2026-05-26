# hera-agent-unity — Project Rule

For any Unity Editor task in this project, **always reach for the `hera-agent-unity` CLI first**. It bridges directly to the running Editor over localhost HTTP, so you can execute C# inside Unity, read the actual console, drive Play Mode, and inspect live types — instead of guessing APIs from training data.

## When to use it

| Need                                            | Command                                                                |
|-------------------------------------------------|------------------------------------------------------------------------|
| What does this type look like in *our* Unity?   | `hera-agent-unity describe_type UnityEditor.EditorApplication`         |
| Test an assumption                              | `hera-agent-unity exec "return EditorSceneManager.GetActiveScene().name;"` |
| See real console errors                         | `hera-agent-unity console --type error`                                |
| Drive Play Mode                                 | `hera-agent-unity editor play --wait`                                  |
| Run tests                                       | `hera-agent-unity test --mode PlayMode`                                |
| Discover the full catalogue                     | `hera-agent-unity list --names`                                        |

## Must-follow rules

- **`exec` is the most powerful tool.** Use it before inventing custom C# scripts.
- **Default to `return null;`** (or no return). The CLI surfaces return values as JSON — a verbose status string just spends your tokens.
- **Read before you write.** `describe_type` + `console --type error` are usually cheaper than running `exec` and parsing failure.
- **One call per assumption.** Batch via `hera-agent-unity batch --file plan.json` when you have an ordered multi-step plan.
- **Trust `--help`.** When this rule contradicts `hera-agent-unity <cmd> --help`, the CLI wins.

## Full guide

The complete cheatsheet (Tool Selection, Cookbook, Pitfalls, Reference) lives at
<https://github.com/NotNull92/hera-agent-unity/blob/main/AGENT.md>.

Pull the latest Quick Rules + Pitfalls into this file at any time:

```bash
hera-agent-unity doctor --agent-rules >> CLAUDE.md
```
