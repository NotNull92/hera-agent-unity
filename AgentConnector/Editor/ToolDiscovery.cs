using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
            public MethodInfo DefaultHandler;
        }

        private struct ActionEntry
        {
            public string ToolName;
            public string ActionName;
            public MethodInfo Method;
            public HeraActionAttribute Attr;
        }

        private static Dictionary<string, ToolEntry> s_Tools;
        private static Dictionary<string, ActionEntry> s_Actions;
        private static readonly object s_CacheLock = new object();

        static ToolDiscovery()
        {
            AssemblyReloadEvents.afterAssemblyReload += InvalidateCache;
        }

        private static void InvalidateCache()
        {
            lock (s_CacheLock)
            {
                s_Tools = null;
                s_Actions = null;
            }
        }

        private static void BuildCache()
        {
            lock (s_CacheLock)
            {
                if (s_Tools != null) return;

                var tools = new Dictionary<string, ToolEntry>(StringComparer.Ordinal);
                var actions = new Dictionary<string, ActionEntry>(StringComparer.Ordinal);

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                    .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
                    .ThenBy(a => a.FullName, StringComparer.Ordinal))
                {
                    foreach (var type in GetLoadableTypes(assembly)
                        .OrderBy(t => t.FullName, StringComparer.Ordinal))
                    {
                        if (!type.IsClass) continue;
                        var attr = type.GetCustomAttribute<HeraToolAttribute>();
                        if (attr == null) continue;
                        if (!attr.Enabled) continue;

                        var name = string.IsNullOrWhiteSpace(attr.Name)
                            ? StringCaseUtility.ToSnakeCase(type.Name)
                            : attr.Name.Trim();
                        if (tools.TryGetValue(name, out var existing))
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
                        var defaultHandler = staticMethod ?? instanceMethod;

                        tools[name] = new ToolEntry
                        {
                            Name = name,
                            Type = type,
                            Attr = attr,
                            DefaultHandler = defaultHandler,
                        };

                        foreach (var method in type.GetMethods(
                            BindingFlags.Public | BindingFlags.NonPublic |
                            BindingFlags.Static | BindingFlags.Instance)
                            .OrderBy(m => m.Name, StringComparer.Ordinal)
                            .ThenBy(m => m.ToString(), StringComparer.Ordinal))
                        {
                            var actionAttr = method.GetCustomAttribute<HeraActionAttribute>();
                            if (actionAttr != null && !IsSupportedActionHandler(method, out var diagnostic))
                            {
                                UnityEngine.Debug.LogWarning(
                                    $"[Hera] Ignored [HeraAction] '{type.FullName}.{method.Name}': {diagnostic}");
                                continue;
                            }

                            bool isLegacyAction = actionAttr == null
                                && method.IsPublic
                                && method.IsStatic
                                && method.Name != "Handle"
                                && method.Name != "HandleCommand"
                                && IsSupportedActionHandler(method, out _);

                            if (actionAttr == null && !isLegacyAction)
                                continue;

                            var configuredName = actionAttr?.Name;
                            var actionName = (string.IsNullOrWhiteSpace(configuredName)
                                ? StringCaseUtility.ToSnakeCase(method.Name)
                                : configuredName.Trim()).ToLowerInvariant();
                            var key = $"{name}:{actionName}";
                            if (actions.ContainsKey(key))
                            {
                                UnityEngine.Debug.LogWarning(
                                    $"[Hera] Duplicate action '{key}' in {type.FullName}; " +
                                    $"later definition ignored.");
                                continue;
                            }

                            actions[key] = new ActionEntry
                            {
                                ToolName = name,
                                ActionName = actionName,
                                Method = method,
                                Attr = actionAttr,
                            };
                        }
                    }
                }

                s_Tools = tools;
                s_Actions = actions;
            }
        }

        internal static bool IsSupportedActionHandler(MethodInfo method, out string diagnostic)
        {
            if (!method.IsPublic || !method.IsStatic)
            {
                diagnostic = "actions must be public static methods.";
                return false;
            }

            var parms = method.GetParameters();
            if (parms.Length != 1 || parms[0].ParameterType != typeof(JObject))
            {
                diagnostic = "actions must accept exactly one Newtonsoft.Json.Linq.JObject parameter.";
                return false;
            }

            if (method.ReturnType != typeof(object)
                && method.ReturnType != typeof(Task<object>)
                && method.ReturnType != typeof(Task))
            {
                diagnostic = "actions must return object, Task<object>, or Task.";
                return false;
            }

            diagnostic = null;
            return true;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                var loadable = RecoverLoadableTypes(exception);
                var loaderErrors = exception.LoaderExceptions?
                    .Where(error => error != null)
                    .Select(error => error.GetType().Name + ": " + error.Message)
                    .Take(3)
                    .ToArray() ?? new string[0];
                var detail = loaderErrors.Length == 0 ? "no loader details" : string.Join(" | ", loaderErrors);
                UnityEngine.Debug.LogWarning(
                    $"[Hera] Tool discovery recovered {loadable.Length} loadable type(s) from " +
                    $"'{assembly.FullName}' after ReflectionTypeLoadException ({detail}).");
                return loadable;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"[Hera] Tool discovery skipped assembly '{assembly.FullName}': " +
                    $"{exception.GetType().Name}: {exception.Message}");
                return Array.Empty<Type>();
            }
        }

        internal static Type[] RecoverLoadableTypes(ReflectionTypeLoadException exception)
        {
            if (exception?.Types == null) return Array.Empty<Type>();
            return exception.Types.Where(type => type != null).ToArray();
        }

        private static Dictionary<string, ToolEntry> GetTools()
        {
            BuildCache();
            return s_Tools;
        }

        private static Dictionary<string, ActionEntry> GetActions()
        {
            BuildCache();
            return s_Actions;
        }

        public static MethodInfo FindDefaultHandler(string command)
        {
            return GetTools().TryGetValue(command, out var entry) ? entry.DefaultHandler : null;
        }

        public static MethodInfo FindActionHandler(string command, string action)
        {
            if (string.IsNullOrEmpty(action)) return null;
            var key = $"{command}:{action.ToLowerInvariant()}";
            return GetActions().TryGetValue(key, out var entry) ? entry.Method : null;
        }

        /// <summary>
        /// Returns tool names within Levenshtein distance <paramref name="maxDistance"/>
        /// of the input. Used by the dispatcher to surface "did you mean" hints on
        /// typo'd command names without forcing the agent to re-run `list`.
        /// </summary>
        public static List<string> SuggestSimilarCommands(string command, int maxDistance = 2, int max = 3)
        {
            if (string.IsNullOrEmpty(command)) return new List<string>();
            var tools = GetTools();

            var candidates = new List<(string name, int dist)>();
            foreach (var name in tools.Keys)
            {
                var d = Levenshtein.Distance(command, name);
                if (d <= maxDistance)
                    candidates.Add((name, d));
            }
            candidates.Sort((a, b) =>
            {
                var byDistance = a.dist.CompareTo(b.dist);
                return byDistance != 0 ? byDistance : string.CompareOrdinal(a.name, b.name);
            });

            var result = new List<string>();
            foreach (var (name, _) in candidates)
            {
                if (result.Count >= max) break;
                result.Add(name);
            }
            return result;
        }

        /// <summary>
        /// Default `list` for agent consumers: name + description, no schema.
        /// Per-tool schemas (the bulk of the bytes) are fetched on demand via
        /// `list --tool &lt;name&gt;`, so the default catalogue stays cheap — the
        /// full-schema dump was ~6k tokens for ~26 tools, almost all of it the
        /// per-parameter JSON Schema the agent rarely needs up front.
        /// </summary>
        public static List<object> GetToolSummaries()
        {
            var tools = new List<object>();
            foreach (var (name, _, attr) in EnumerateTools())
                tools.Add(new { name, description = attr.Description ?? "" });
            return tools;
        }

        /// <summary>
        /// Names-only listing — a flat array of tool names, nothing else. The
        /// cheapest discovery surface (the AGENTS.md bootstrap runs this every
        /// session); descriptions live in the default `list`, schemas in
        /// `list --tool &lt;name&gt;`.
        /// </summary>
        public static List<object> GetToolNames()
        {
            var tools = new List<object>();
            foreach (var (name, _, _) in EnumerateTools())
                tools.Add(name);
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
                var toolMeta = GetToolMetadata(type);
                return new
                {
                    name,
                    description = attr.Description ?? "",
                    group = attr.Group ?? "",
                    groups = attr.Groups ?? new string[0],
                    examples = BuildExamples(attr),
                    actions = GetActionDescriptors(name),
                    schema = toolMeta?.ParametersSchema
                        ?? GetLegacyParameterSchema(paramsType),
                    output_schema = toolMeta?.OutputSchema
                        ?? GetDefaultOutputSchema(),
                    metadata = new
                    {
                        enum_support = HasEnumSupport(paramsType),
                        default_support = HasDefaultSupport(paramsType),
                        output_schema_support = HasOutputSchemaSupport(paramsType),
                        custom_types = GetCustomTypes(paramsType),
                        safety = BuildSafetyMetadata(
                            attr.ReadOnly,
                            attr.Destructive,
                            attr.Idempotent,
                            attr.MayReloadDomain,
                            attr.RequiresPlayMode),
                        action_safety = BuildActionSafetyMetadata(type),
                    },
                };
            }
            return null;
        }

        // BuildExamples zips attr.Examples and attr.ExampleDescriptions by
        // index. Missing descriptions become empty strings; tools that don't
        // declare Examples return an empty list. The slim GetToolSummaries()
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

        private static object BuildSafetyMetadata(
            bool readOnly,
            bool destructive,
            bool idempotent,
            bool mayReloadDomain,
            bool requiresPlayMode)
        {
            return new
            {
                read_only = readOnly,
                destructive,
                idempotent,
                may_reload_domain = mayReloadDomain,
                requires_play_mode = requiresPlayMode,
            };
        }

        private static Dictionary<string, object> BuildActionSafetyMetadata(Type toolType)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var safety in toolType.GetCustomAttributes<HeraActionSafetyAttribute>()
                .OrderBy(safety => safety.Action, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(safety.Action))
                    continue;
                result[safety.Action.ToLowerInvariant()] = BuildSafetyMetadata(safety);
            }

            foreach (var method in toolType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ThenBy(method => method.ToString(), StringComparer.Ordinal))
            {
                foreach (var safety in method.GetCustomAttributes<HeraActionSafetyAttribute>())
                {
                    var action = safety.Action;
                    if (string.IsNullOrWhiteSpace(action))
                    {
                        var actionAttr = method.GetCustomAttribute<HeraActionAttribute>();
                        action = actionAttr?.Name ?? StringCaseUtility.ToSnakeCase(method.Name);
                    }
                    if (!string.IsNullOrWhiteSpace(action))
                        result[action.Trim().ToLowerInvariant()] = BuildSafetyMetadata(safety);
                }
            }

            return result.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        private static object BuildSafetyMetadata(HeraActionSafetyAttribute safety)
        {
            return BuildSafetyMetadata(
                safety.ReadOnly,
                safety.Destructive,
                safety.Idempotent,
                safety.MayReloadDomain,
                safety.RequiresPlayMode);
        }

        private static IEnumerable<(string name, Type type, HeraToolAttribute attr)> EnumerateTools()
        {
            foreach (var entry in GetTools().Values.OrderBy(entry => entry.Name, StringComparer.Ordinal))
                yield return (entry.Name, entry.Type, entry.Attr);
        }

        private static List<object> GetActionDescriptors(string toolName)
        {
            return GetActions().Values
                .Where(entry => entry.ToolName == toolName)
                .OrderBy(entry => entry.ActionName, StringComparer.Ordinal)
                .Select(entry => (object)new
                {
                    name = entry.ActionName,
                    description = entry.Attr?.Description ?? "",
                })
                .ToList();
        }

        private static ToolMetadata GetToolMetadata(Type toolType)
        {
            try
            {
                var attr = toolType.GetCustomAttribute<HeraToolAttribute>();
                var toolName = string.IsNullOrWhiteSpace(attr?.Name)
                    ? StringCaseUtility.ToSnakeCase(toolType.Name)
                    : attr.Name.Trim();
                var cached = ToolMetadataRegistry.GetTool(toolName);
                if (cached != null) return cached;
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
                .Any(p => !string.IsNullOrWhiteSpace(
                    p.GetCustomAttribute<ToolParameterAttribute>()?.EnumType));
        }

        private static bool HasDefaultSupport(Type paramsType)
        {
            if (paramsType == null) return false;

            return paramsType.GetProperties()
                .Any(p =>
                {
                    var attr = p.GetCustomAttribute<ToolParameterAttribute>();
                    return attr != null && (attr.Default != null || attr.DefaultValue != null);
                });
        }

        private static List<string> GetCustomTypes(Type paramsType)
        {
            if (paramsType == null) return new List<string>();

            return paramsType.GetProperties()
                .Select(p => p.GetCustomAttribute<ToolParameterAttribute>()?.EnumType)
                .Where(type => !string.IsNullOrWhiteSpace(type))
                .Select(type => type.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(type => type, StringComparer.Ordinal)
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
                .Any(p => !string.IsNullOrWhiteSpace(
                    p.GetCustomAttribute<ToolParameterAttribute>()?.OutputSchema));
        }
    }
}
