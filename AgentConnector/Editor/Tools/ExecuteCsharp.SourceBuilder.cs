using System;
using System.Collections.Generic;
using System.Text;

namespace HeraAgent.Tools
{
    public static partial class ExecuteCsharp
    {
        private static readonly string[] DefaultUsings =
        {
            "System",
            "System.Collections.Generic",
            "System.IO",
            "System.Linq",
            "System.Reflection",
            "System.Threading.Tasks",
            "UnityEngine",
            "UnityEngine.SceneManagement",
            "UnityEditor",
            "UnityEditor.SceneManagement",
            "UnityEditorInternal",
        };

        private struct BuiltSource
        {
            public string Source;
            // csc reports diagnostics against the wrapped source. Subtract this
            // offset from a raw error line to map back to the user's snippet
            // line (1-based) so AGENT.md's "L<line>: <message>" hints land on
            // the actual offending line of the user's code.
            public int UserCodeLineOffset;
        }

        private static BuiltSource BuildSource(string code, List<string> extraUsings)
        {
            // Hoist leading `using` directives out of the snippet body. The body
            // is wrapped inside a method, where a top-level `using X;` is illegal
            // and csc reports a bare "Identifier expected" (CS1001) at the `;` —
            // a misdirecting message. Agents and humans naturally write usings at
            // the top of a multi-line file, so lift them into the generated using
            // block instead of failing. Lines are blanked (not removed) in place
            // so csc error lines still map 1:1 to the user's snippet via
            // UserCodeLineOffset.
            var hoisted = new List<string>();
            var bodyLines = SplitLines(code);
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var u in DefaultUsings) known.Add(u);
            foreach (var u in extraUsings) known.Add(u.Trim());
            for (int i = 0; i < bodyLines.Length; i++)
            {
                var trimmed = bodyLines[i].Trim();
                // Blank/comment lines don't end the leading-using run.
                if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;
                if (!TryExtractUsingDirective(trimmed, out var directive))
                    break; // first real statement — stop hoisting
                if (known.Add(directive))
                    hoisted.Add(directive);
                bodyLines[i] = string.Empty; // preserve line count for offset math
            }

            var sb = new StringBuilder();
            foreach (var u in DefaultUsings)
                sb.AppendLine($"using {u};");
            foreach (var u in extraUsings)
                sb.AppendLine($"using {u};");
            foreach (var u in hoisted)
                sb.AppendLine($"using {u};");

            sb.AppendLine();
            sb.AppendLine("public static class __CliDynamic {");
            sb.AppendLine("  public static object Execute() {");
            sb.AppendLine(string.Join("\n", bodyLines));
            // Fallthrough so snippets without a trailing `return` still compile,
            // resolving to null. AGENT.md documents this — Rule 1's "no return"
            // examples rely on it. CS0162 is suppressed in CompileToBytes for
            // the case where the user's snippet already returns.
            sb.AppendLine("    return null;");
            sb.AppendLine("  }");
            sb.AppendLine("}");

            // Wrapper above user code: N usings + 1 blank line + `class` line
            // + method-header line. The first line of the user's snippet sits
            // at line (offset + 1) in the wrapped source. Hoisted directives add
            // their own using lines, so they count toward the offset too.
            int offset = DefaultUsings.Length + extraUsings.Count + hoisted.Count + 3;
            return new BuiltSource { Source = sb.ToString(), UserCodeLineOffset = offset };
        }

        private static string[] SplitLines(string s)
        {
            return s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        // Recognizes a using *directive* (namespace import, `using static`, or
        // alias) and returns the part after `using` minus the trailing `;`.
        // Returns false for `using (...)` / `using var ...` resource statements,
        // which are real code and must stay in the body.
        private static bool TryExtractUsingDirective(string trimmed, out string directive)
        {
            directive = null;
            if (!trimmed.StartsWith("using", StringComparison.Ordinal))
                return false;
            if (trimmed.Length == 5 || !char.IsWhiteSpace(trimmed[5]))
                return false; // "usingX" or bare "using"
            var rest = trimmed.Substring(5).TrimStart();
            if (rest.StartsWith("(", StringComparison.Ordinal))
                return false; // using (resource) { ... }
            if (rest.StartsWith("var", StringComparison.Ordinal) &&
                (rest.Length == 3 || char.IsWhiteSpace(rest[3])))
                return false; // using var x = ...;
            if (!rest.EndsWith(";", StringComparison.Ordinal))
                return false; // multi-line / not a simple directive
            var content = rest.Substring(0, rest.Length - 1).Trim();
            if (content.Length == 0 || content.Contains("("))
                return false;
            directive = content;
            return true;
        }
    }
}
