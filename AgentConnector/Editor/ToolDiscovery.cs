using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace HeraAgent
{
    /// <summary>
    /// Finds [HeraTool] handlers on demand via reflection.
    /// Result is cached per assembly-reload — a fresh scan happens only when
    /// Unity reloads the domain, which is also when new tools could appear.
    /// </summary>
    [InitializeOnLoad]
    public static class ToolDiscovery
    {
        private struct ToolEntry
        {
            public string Name;
            public Type Type;
            public HeraToolAttribute Attr;
            public MethodInfo Handler;
        }

        private static Dictionary<string, ToolEntry> s_Cache;
        private static readonly object s_CacheLock = new object();

        static ToolDiscovery()
        {
            AssemblyReloadEvents.afterAssemblyReload += InvalidateCache;
        }

        private static void InvalidateCache()
        {
            lock (s_CacheLock) s_Cache = null;
        }

        private static Dictionary<string, ToolEntry> GetCache()
        {
            lock (s_CacheLock)
            {
                if (s_Cache != null) return s_Cache;
                s_Cache = BuildCache();
                return s_Cache;
            }
        }

        private static Dictionary<string, ToolEntry> BuildCache()
        {
            var cache = new Dictionary<string, ToolEntry>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException) { continue; }

                foreach (var type in types)
                {
                    if (!type.IsClass) continue;
                    var attr = type.GetCustomAttribute<HeraToolAttribute>();
                    if (attr == null) continue;
                    if (!attr.Enabled) continue;

                    var name = attr.Name ?? StringCaseUtility.ToSnakeCase(type.Name);
                    if (cache.TryGetValue(name, out var existing))
                    {
                        UnityEngine.Debug.LogError(
                            $"[Hera] Duplicate tool name '{name}': " +
                            $"{existing.Type.FullName} and {type.FullName}. " +
                            $"Rename one or remove the duplicate.");
                        continue;
                    }

                    var staticMethod = type.GetMethod("HandleCommand",
                        BindingFlags.Public | BindingFlags.Static, null,
                        new[] { typeof(JObject) }, null);
                    var instanceMethod = type.GetMethod("HandleCommand",
                        BindingFlags.Public | BindingFlags.Instance, null,
                        new[] { typeof(JObject) }, null);
                    var handler = staticMethod ?? instanceMethod;

                    cache[name] = new ToolEntry
                    {
                        Name = name,
                        Type = type,
                        Attr = attr,
                        Handler = handler,
                    };
                }
            }
            return cache;
        }

        public static MethodInfo FindHandler(string command)
        {
            return GetCache().TryGetValue(command, out var entry) ? entry.Handler : null;
        }

        /// <summary>
        /// Returns tool names within Levenshtein distance <paramref name="maxDistance"/>
        /// of the input. Used by the dispatcher to surface "did you mean" hints on
        /// typo'd command names without forcing the agent to re-run `list`.
        /// </summary>
        public static List<string> SuggestSimilarCommands(string command, int maxDistance = 2, int max = 3)
        {
            if (string.IsNullOrEmpty(command)) return new List<string>();
            var cache = GetCache();

            var candidates = new List<(string name, int dist)>();
            foreach (var name in cache.Keys)
            {
                var d = Levenshtein.Distance(command, name);
                if (d <= maxDistance)
                    candidates.Add((name, d));
            }
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            var result = new List<string>();
            foreach (var (name, _) in candidates)
            {
                if (result.Count >= max) break;
                result.Add(name);
            }
            return result;
        }

        /// <summary>
        /// Slim default for agent consumers: name, description, schema only.
        /// Token-cost of `list` was ~90% redundancy across parameters/schema/metadata.
        /// For full per-tool detail use GetToolSchema(name).
        /// </summary>
        public static List<object> GetToolSchemas()
        {
            var tools = new List<object>();
            foreach (var (name, type, attr) in EnumerateTools())
            {
                var paramsType = type.GetNestedType("Parameters");
                tools.Add(new
                {
                    name,
                    description = attr.Description ?? "",
                    schema = GetToolMetadata(type)?.ParametersSchema
                        ?? GetLegacyParameterSchema(paramsType),
                });
            }
            return tools;
        }

        /// <summary>
        /// Names-only listing — one entry per tool. Use when an agent just needs
        /// to know what's available before picking one to introspect.
        /// </summary>
        public static List<object> GetToolNames()
        {
            var tools = new List<object>();
            foreach (var (name, _, attr) in EnumerateTools())
            {
                tools.Add(new { name, description = attr.Description ?? "" });
            }
            return tools;
        }

        /// <summary>
        /// Full schema for a single tool — returned by `list --tool &lt;name&gt;`.
        /// Includes parameters schema, output schema, and metadata. Null if not found.
        /// </summary>
        public static object GetToolSchema(string toolName)
        {
            foreach (var (name, type, attr) in EnumerateTools())
            {
                if (name != toolName) continue;
                var paramsType = type.GetNestedType("Parameters");
                return new
                {
                    name,
                    description = attr.Description ?? "",
                    group = attr.Group ?? "",
                    groups = attr.Groups ?? new string[0],
                    examples = BuildExamples(attr),
                    schema = GetToolMetadata(type)?.ParametersSchema
                        ?? GetLegacyParameterSchema(paramsType),
                    output_schema = GetToolMetadata(type)?.OutputSchema
                        ?? GetDefaultOutputSchema(),
                    metadata = new
                    {
                        enum_support = HasEnumSupport(paramsType),
                        default_support = HasDefaultSupport(paramsType),
                        output_schema_support = HasOutputSchemaSupport(paramsType),
                        custom_types = GetCustomTypes(paramsType),
                    },
                };
            }
            return null;
        }

        // BuildExamples zips attr.Examples and attr.ExampleDescriptions by
        // index. Missing descriptions become empty strings; tools that don't
        // declare Examples return an empty list. The slim GetToolSchemas()
        // intentionally omits this field — examples are deep-dive material,
        // surfaced only by `list --tool <name>` to keep `list` itself lean.
        private static List<object> BuildExamples(HeraToolAttribute attr)
        {
            var examples = attr.Examples ?? new string[0];
            var descriptions = attr.ExampleDescriptions ?? new string[0];
            var result = new List<object>(examples.Length);
            for (int i = 0; i < examples.Length; i++)
            {
                result.Add(new
                {
                    call = examples[i],
                    description = i < descriptions.Length ? descriptions[i] : "",
                });
            }
            return result;
        }

        private static IEnumerable<(string name, Type type, HeraToolAttribute attr)> EnumerateTools()
        {
            foreach (var entry in GetCache().Values)
                yield return (entry.Name, entry.Type, entry.Attr);
        }

        private static ToolMetadata GetToolMetadata(Type toolType)
        {
            try
            {
                var attr = toolType.GetCustomAttribute<HeraToolAttribute>();
                var toolName = attr?.Name ?? StringCaseUtility.ToSnakeCase(toolType.Name);
                ToolMetadataRegistry.Register(toolType);
                return ToolMetadataRegistry.GetTool(toolName);
            }
            catch
            {
                return null;
            }
        }

        private static JObject GetLegacyParameterSchema(Type paramsType)
        {
            if (paramsType == null) return null;

            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject()
            };

            var requiredParams = new List<string>();

            foreach (var prop in paramsType.GetProperties())
            {
                var attr = prop.GetCustomAttribute<ToolParameterAttribute>();
                if (attr == null) continue;

                var propName = StringCaseUtility.ToSnakeCase(prop.Name);
                var paramSchema = new JObject
                {
                    ["type"] = SchemaUtility.GetJsonTypeName(prop.PropertyType),
                    ["description"] = attr.Description ?? ""
                };

                if (attr.Required)
                {
                    requiredParams.Add(propName);
                }

                schema["properties"][propName] = paramSchema;
            }

            if (requiredParams.Count > 0)
            {
                schema["required"] = new JArray(requiredParams);
            }

            return schema;
        }

        private static bool HasEnumSupport(Type paramsType)
        {
            if (paramsType == null) return false;

            return paramsType.GetProperties()
                .Any(p => p.GetCustomAttribute<ToolParameterAttribute>()?.EnumType != null);
        }

        private static bool HasDefaultSupport(Type paramsType)
        {
            if (paramsType == null) return false;

            return paramsType.GetProperties()
                .Any(p => p.GetCustomAttribute<ToolParameterAttribute>()?.Default != null);
        }

        private static List<string> GetCustomTypes(Type paramsType)
        {
            if (paramsType == null) return new List<string>();

            return paramsType.GetProperties()
                .Where(p => p.GetCustomAttribute<ToolParameterAttribute>()?.EnumType != null)
                .Select(p => p.GetCustomAttribute<ToolParameterAttribute>().EnumType)
                .Where(type => !string.IsNullOrEmpty(type))
                .Distinct()
                .ToList();
        }

        private static JObject GetDefaultOutputSchema()
        {
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["success"] = new JObject { ["type"] = "boolean", ["description"] = "Whether the operation succeeded" },
                    ["message"] = new JObject { ["type"] = "string", ["description"] = "Success or error message" },
                    ["data"] = new JObject { ["type"] = "object", ["description"] = "Tool-specific output data" }
                }
            };
        }

        private static bool HasOutputSchemaSupport(Type paramsType)
        {
            if (paramsType == null) return false;

            return paramsType.GetProperties()
                .Any(p => p.GetCustomAttribute<ToolParameterAttribute>()?.OutputSchema != null);
        }
    }
}
