using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "describe_shader",
        Description = "Inspect a shader's properties (name, type, display label, range) or search shader names. Use this before manage_material to learn which properties a shader exposes (e.g. _BaseColor, _Metallic) and their types. Pass a shader name to describe it; pass --list to search names instead.",
        Examples = new[]
        {
            "describe_shader \"Universal Render Pipeline/Lit\"",
            "describe_shader Standard --limit 80",
            "describe_shader --list --filter URP",
            "describe_shader --list --include_builtin false",
        },
        ExampleDescriptions = new[]
        {
            "Describe one shader — its properties with type and range (default limit 60)",
            "Raise the per-property limit for property-heavy shaders",
            "List mode — shader names containing 'URP' (built-in included by default)",
            "List only project (asset) shaders, skipping built-ins",
        })]
    public static class DescribeShader
    {
        public class Parameters
        {
            [ToolParameter("Shader name to describe (e.g. 'Universal Render Pipeline/Lit', 'Standard'). Omit and pass --list to search names instead.")]
            public string Name { get; set; }

            [ToolParameter("List mode: search shader names instead of describing one. Combine with --filter.")]
            public bool List { get; set; }

            [ToolParameter("List mode: case-insensitive substring filter on the shader name.")]
            public string Filter { get; set; }

            [ToolParameter("Max items returned. get: properties (default 60). list: shaders (default 50). Over the limit truncates with a note.")]
            public int Limit { get; set; }

            [ToolParameter("List mode: include built-in shaders, not just project assets. Default true.")]
            public bool IncludeBuiltin { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var name = p.Get("name")
                ?? (p.GetRaw("args") as JArray)?[0]?.ToString();
            var listMode = p.GetBool("list");

            if (listMode)
                return ListShaders(p.Get("filter"), p.GetInt("limit") ?? 50, p.GetBool("include_builtin", true));

            if (string.IsNullOrWhiteSpace(name))
                return new ErrorResponse(
                    "'name' required. Pass a shader name to describe, or --list to search names.");

            return DescribeOne(name, p.GetInt("limit") ?? 60);
        }

        private static object DescribeOne(string name, int limit)
        {
            if (limit <= 0) limit = 60;
            var shader = Shader.Find(name);
            if (shader == null)
            {
                var suggests = SuggestSimilar(name);
                var data = suggests.Count > 0 ? (object)new { did_you_mean = suggests } : null;
                var hints = suggests.Select(s => $"describe_shader \"{s}\"").ToList();
                if (hints.Count == 0) hints.Add("describe_shader --list --filter <substring>");
                return new ErrorResponse(
                    "SHADER_NOT_FOUND",
                    $"No shader named '{name}'. Names are case-sensitive; use --list to search.",
                    data: data,
                    suggestions: hints);
            }

            var count = ShaderUtil.GetPropertyCount(shader);
            var shown = Math.Min(count, limit);
            var properties = new List<object>(shown);
            for (var i = 0; i < shown; i++)
            {
                var type = ShaderUtil.GetPropertyType(shader, i);
                var prop = new Dictionary<string, object>
                {
                    ["name"] = ShaderUtil.GetPropertyName(shader, i),
                    ["type"] = type.ToString(),
                };
                var display = ShaderUtil.GetPropertyDescription(shader, i);
                if (!string.IsNullOrEmpty(display)) prop["display"] = display;
                if (type == ShaderUtil.ShaderPropertyType.Range)
                    prop["range"] = new[]
                    {
                        ShaderUtil.GetRangeLimits(shader, i, 1),
                        ShaderUtil.GetRangeLimits(shader, i, 2),
                    };
                properties.Add(prop);
            }

            return new SuccessResponse(
                $"describe_shader: {shader.name} ({count} properties)",
                new
                {
                    name = shader.name,
                    property_count = count,
                    truncated = count > shown,
                    properties,
                });
        }

        private static object ListShaders(string filter, int limit, bool includeBuiltin)
        {
            if (limit <= 0) limit = 50;

            IEnumerable<string> source;
            if (includeBuiltin)
            {
                source = ShaderUtil.GetAllShaderInfo().Select(si => si.name);
            }
            else
            {
                source = AssetDatabase.FindAssets("t:Shader")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<Shader>)
                    .Where(s => s != null)
                    .Select(s => s.name);
            }

            // Hidden/Internal-* shaders are engine plumbing, not material-authorable —
            // drop them so the list surfaces shaders a caller could actually use.
            var names = source
                .Where(n => !string.IsNullOrEmpty(n) && !n.StartsWith("Hidden/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Where(n => string.IsNullOrEmpty(filter) ||
                            n.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var total = names.Count;
            var shown = names.Take(limit).ToArray();
            var truncated = total > shown.Length;
            var message = truncated
                ? $"{total} shaders (showing first {shown.Length} — use --filter to narrow or --limit to expand)"
                : $"{total} shaders";

            return new SuccessResponse(message, new
            {
                total,
                truncated,
                shaders = shown,
            });
        }

        // Edit-distance suggestion over real shader names, mirroring unity_docs'
        // "did you mean". Corpus is a few hundred names, so a bounded full scan
        // is cheap — no need for the prefix-bucket pre-filter UnityDocsStore uses.
        private static List<string> SuggestSimilar(string query)
        {
            if (string.IsNullOrEmpty(query)) return new List<string>();
            var budget = Math.Max(2, query.Length / 3);
            return ShaderUtil.GetAllShaderInfo()
                .Select(si => si.name)
                .Where(n => !string.IsNullOrEmpty(n) && !n.StartsWith("Hidden/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Select(n => (name: n, dist: Levenshtein.DistanceBounded(query, n, budget)))
                .Where(x => x.dist <= budget)
                .OrderBy(x => x.dist)
                .Take(5)
                .Select(x => x.name)
                .ToList();
        }
    }
}
