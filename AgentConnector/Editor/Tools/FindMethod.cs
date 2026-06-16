using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "find_method",
        Description = "Search method names across loaded assemblies by substring. Returns declaring type and signature (the signature includes the static modifier and return type). Useful when you remember 'something with Refresh' but not the exact class.",
        Examples = new[]
        {
            "find_method Refresh",
            "find_method GetActiveScene --namespace UnityEditor",
            "find_method Find --limit 20",
            "find_method GetComponent --group_by_type false",
        },
        ExampleDescriptions = new[]
        {
            "Find every method whose name contains 'Refresh' (grouped by declaring type)",
            "Restrict search to types whose namespace starts with 'UnityEditor'",
            "Limit results (default 50; higher counts may truncate context)",
            "Use the flat per-hit format (legacy; larger payload)",
        })]
    public static class FindMethod
    {
        public class Parameters
        {
            [ToolParameter("Case-insensitive substring of the method name.", Required = true)]
            public string Pattern { get; set; }

            [ToolParameter("Restrict to types whose namespace starts with this prefix. Example: 'UnityEditor'.")]
            public string Namespace { get; set; }

            [ToolParameter("Maximum results. Default 50.")]
            public int Limit { get; set; }

            [ToolParameter("Include private/internal/protected methods. Default false.")]
            public bool IncludePrivate { get; set; }

            [ToolParameter("Group hits by declaring type (default true — smaller payload). Set false for legacy flat list.")]
            public bool GroupByType { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var pattern = p.Get("pattern")
                ?? (p.GetRaw("args") as JArray)?[0]?.ToString();
            if (string.IsNullOrWhiteSpace(pattern))
                return new ErrorResponse("'pattern' parameter required.");

            var nsFilter = p.Get("namespace");
            var limit = p.GetInt("limit") ?? 50;
            if (limit <= 0) limit = 50;
            var includePrivate = p.GetBool("include_private");
            // group_by_type defaults to true; only flat when explicitly disabled.
            var groupByType = @params?["group_by_type"]?.Value<bool?>() ?? true;

            var binding = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            if (includePrivate) binding |= BindingFlags.NonPublic;

            // Internal accumulator: keep a single representation, transform at the end.
            var rawHits = new List<(string DeclaringType, string Assembly, string Signature)>();
            var totalMatches = 0;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!string.IsNullOrEmpty(nsFilter) &&
                        (t.Namespace == null || !t.Namespace.StartsWith(nsFilter, StringComparison.Ordinal)))
                        continue;

                    MethodInfo[] methods;
                    try { methods = t.GetMethods(binding); }
                    catch { continue; }

                    foreach (var m in methods)
                    {
                        if (m.IsSpecialName) continue;
                        if (m.Name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) < 0) continue;

                        totalMatches++;
                        if (rawHits.Count < limit)
                        {
                            rawHits.Add((t.FullName, asm.GetName().Name, DescribeType.FormatMethodSignature(m)));
                        }
                    }
                }
            }

            var truncated = totalMatches > rawHits.Count;
            var message = truncated
                ? $"{totalMatches} matches (showing first {rawHits.Count} — use --limit to expand or --namespace to narrow)"
                : $"{totalMatches} matches";

            if (groupByType)
            {
                var grouped = rawHits
                    .GroupBy(h => h.DeclaringType)
                    .Select(g => new
                    {
                        type = g.Key,
                        assembly = g.First().Assembly,
                        // Bare signature strings — the signature already encodes the
                        // static modifier and the method name, so an object wrapper
                        // per method was pure token overhead.
                        methods = g.Select(h => h.Signature).ToArray(),
                    })
                    .ToArray();

                return new SuccessResponse(message, new
                {
                    total = totalMatches,
                    truncated,
                    results = grouped,
                });
            }

            var flat = rawHits.Select(h => (object)new
            {
                declaring_type = h.DeclaringType,
                assembly = h.Assembly,
                signature = h.Signature,
            }).ToList();

            return new SuccessResponse(message, new
            {
                total = totalMatches,
                truncated,
                results = flat,
            });
        }
    }
}
