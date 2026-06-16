using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "describe_type",
        Description = "Describe a type loaded in the Unity Editor: kind, base, interfaces, public members, and known Unity pitfalls. Use this before writing exec code that touches a specific type to avoid signature/namespace mistakes.",
        Examples = new[]
        {
            "describe_type UnityEditor.EditorApplication",
            "describe_type GameObject --members all",
            "describe_type AssetDatabase --members methods --limit 50",
            "describe_type EditorApplication --include_private true",
        },
        ExampleDescriptions = new[]
        {
            "Default — methods only, top 30 (token-friendly first look)",
            "Show all categories: methods, properties, fields, events",
            "Filter members + raise limit (default limit 30)",
            "Include private/internal members (default: public only)",
        })]
    public static class DescribeType
    {
        public class Parameters
        {
            [ToolParameter("Type name — either full ('UnityEditor.EditorApplication') or simple ('EditorApplication'). Full names preferred.", Required = true)]
            public string Type { get; set; }

            [ToolParameter("Which members to include: 'methods' (default), 'properties', 'fields', 'events', or 'all'.")]
            public string Members { get; set; }

            [ToolParameter("Include private/internal/protected members. Default false.")]
            public bool IncludePrivate { get; set; }

            [ToolParameter("Maximum members returned per category. Default 30. Anything over truncates with a note.")]
            public int Limit { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var typeName = p.Get("type")
                ?? (p.GetRaw("args") as JArray)?[0]?.ToString();
            if (string.IsNullOrWhiteSpace(typeName))
                return new ErrorResponse("'type' parameter required.");

            var filterRaw = p.Get("members") ?? "methods";
            var membersFilter = filterRaw.ToLowerInvariant();
            var includePrivate = p.GetBool("include_private");
            var limit = p.GetInt("limit") ?? 30;
            if (limit <= 0) limit = 30;

            var resolved = ResolveType(typeName);
            if (resolved == null)
                return new ErrorResponse($"Type '{typeName}' not found in loaded assemblies. Try the full name (e.g. 'UnityEditor.EditorApplication') or run list_assemblies to confirm the package is loaded.");

            var bindingPublic = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var bindingAll = bindingPublic | BindingFlags.NonPublic;
            var binding = includePrivate ? bindingAll : bindingPublic;

            object methods = null, properties = null, fields = null, events = null;
            if (membersFilter == "all" || membersFilter == "methods")
                methods = DescribeMethods(resolved.GetMethods(binding), limit);
            if (membersFilter == "all" || membersFilter == "properties")
                properties = DescribeProperties(resolved.GetProperties(binding), limit);
            if (membersFilter == "all" || membersFilter == "fields")
                fields = DescribeFields(resolved.GetFields(binding), limit);
            if (membersFilter == "all" || membersFilter == "events")
                events = DescribeEvents(resolved.GetEvents(binding), limit);

            var pitfalls = UnityPitfalls.Lookup(resolved.FullName);

            return new SuccessResponse($"Described {resolved.FullName}", new
            {
                name = resolved.Name,
                full_name = resolved.FullName,
                @namespace = resolved.Namespace,
                assembly = resolved.Assembly.GetName().Name,
                kind = GetKind(resolved),
                is_static = resolved.IsAbstract && resolved.IsSealed,
                base_type = resolved.BaseType?.FullName,
                interfaces = resolved.GetInterfaces().Select(i => i.FullName).ToArray(),
                methods,
                properties,
                fields,
                events,
                pitfalls = pitfalls.Count == 0
                    ? null
                    : pitfalls.Select(p => new { text = p.Text, doc_url = p.DocUrl }).ToArray(),
            });
        }

        private static Type ResolveType(string name)
        {
            // Exact full-name match across all loaded assemblies.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (string.Equals(t.FullName, name, StringComparison.Ordinal))
                        return t;
                }
            }

            // Fallback: simple-name match (first hit wins, but prefer Unity* over others).
            Type fallback = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!string.Equals(t.Name, name, StringComparison.Ordinal)) continue;
                    if (fallback == null) { fallback = t; continue; }
                    // Prefer UnityEngine.*/UnityEditor.* over arbitrary matches.
                    if (IsPreferredNamespace(t.Namespace) && !IsPreferredNamespace(fallback.Namespace))
                        fallback = t;
                }
            }
            return fallback;
        }

        private static bool IsPreferredNamespace(string ns)
        {
            return ns != null && (ns.StartsWith("UnityEngine", StringComparison.Ordinal) || ns.StartsWith("UnityEditor", StringComparison.Ordinal));
        }

        private static string GetKind(Type t)
        {
            if (t.IsEnum) return "enum";
            if (t.IsInterface) return "interface";
            if (t.IsValueType) return "struct";
            return "class";
        }

        private static object DescribeMethods(MethodInfo[] methods, int limit)
        {
            var filtered = methods
                .Where(m => !m.IsSpecialName) // skip property/event accessors
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var truncated = filtered.Length > limit;
            return new
            {
                count = filtered.Length,
                truncated,
                items = filtered.Take(limit).Select(m => new
                {
                    name = m.Name,
                    signature = FormatMethodSignature(m),
                }).ToArray(),
            };
        }

        private static object DescribeProperties(PropertyInfo[] props, int limit)
        {
            var truncated = props.Length > limit;
            return new
            {
                count = props.Length,
                truncated,
                items = props
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(limit)
                    .Select(p => new
                    {
                        name = p.Name,
                        type = FormatTypeName(p.PropertyType),
                        can_read = p.CanRead,
                        can_write = p.CanWrite,
                        is_static = (p.GetMethod ?? p.SetMethod)?.IsStatic ?? false,
                    }).ToArray(),
            };
        }

        private static object DescribeFields(FieldInfo[] fields, int limit)
        {
            var truncated = fields.Length > limit;
            return new
            {
                count = fields.Length,
                truncated,
                items = fields
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(limit)
                    .Select(f => new
                    {
                        name = f.Name,
                        type = FormatTypeName(f.FieldType),
                        is_static = f.IsStatic,
                        is_const = f.IsLiteral,
                        is_readonly = f.IsInitOnly,
                    }).ToArray(),
            };
        }

        private static object DescribeEvents(EventInfo[] events, int limit)
        {
            var truncated = events.Length > limit;
            return new
            {
                count = events.Length,
                truncated,
                items = events
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(limit)
                    .Select(e => new
                    {
                        name = e.Name,
                        handler = FormatTypeName(e.EventHandlerType),
                        is_static = e.AddMethod?.IsStatic ?? false,
                    }).ToArray(),
            };
        }

        internal static string FormatMethodSignature(MethodInfo m)
        {
            var parameters = string.Join(", ", m.GetParameters().Select(pi => $"{FormatTypeName(pi.ParameterType)} {pi.Name}"));
            var staticPrefix = m.IsStatic ? "static " : "";
            return $"{staticPrefix}{FormatTypeName(m.ReturnType)} {m.Name}({parameters})";
        }

        internal static string FormatTypeName(Type t)
        {
            if (t == null) return "void";
            if (t == typeof(void)) return "void";
            if (t == typeof(string)) return "string";
            if (t == typeof(int)) return "int";
            if (t == typeof(long)) return "long";
            if (t == typeof(float)) return "float";
            if (t == typeof(double)) return "double";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(object)) return "object";

            if (t.IsArray) return FormatTypeName(t.GetElementType()) + "[]";

            if (t.IsGenericType)
            {
                var def = t.GetGenericTypeDefinition().Name;
                var tickIdx = def.IndexOf('`');
                if (tickIdx > 0) def = def.Substring(0, tickIdx);
                var args = string.Join(", ", t.GetGenericArguments().Select(FormatTypeName));
                return $"{def}<{args}>";
            }

            return t.Name;
        }
    }
}
