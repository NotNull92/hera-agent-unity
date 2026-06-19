using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HeraAgent.Tools
{
    public static partial class ExecuteCsharp
    {
        private const int DefaultSerializeDepth = 3;
        private const int MaxSerializeDepth = 8;

        private static readonly Dictionary<Type, MemberInfo[]> s_SerializableMembersCache = new();

        static ExecuteCsharp()
        {
            AssemblyReloadEvents.afterAssemblyReload += () => s_SerializableMembersCache.Clear();
        }

        private static int ClampDepth(int requested)
        {
            if (requested <= 0) return DefaultSerializeDepth;
            return requested > MaxSerializeDepth ? MaxSerializeDepth : requested;
        }

        private class LoggedError
        {
            public string type;
            public string message;
        }

        private static object Invoke(Assembly compiled, Dictionary<string, long> timings, string stacktraceMode, int depth, bool strict)
        {
            var method = compiled.GetType("__CliDynamic")?.GetMethod("Execute");
            if (method == null)
                return new ErrorResponse("EXEC_INTERNAL_ERROR",
                    "Internal error: compiled type or method not found.");

            // Strict mode: capture LogError/LogException/LogAssert raised by the
            // snippet and surface them as a failure even when Execute() returns
            // normally. Without this, `Debug.LogError(...); return null;` looks
            // identical to a clean run at the CLI/exit-code layer — agents
            // can't tell. AGENT.md Rule 8 documents the contract.
            var logged = strict ? new List<LoggedError>() : null;
            Application.LogCallback handler = null;
            if (strict)
            {
                handler = (string condition, string stack, LogType type) =>
                {
                    if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                        return;
                    if (logged.Count >= 20) return; // cap to keep response bounded
                    logged.Add(new LoggedError { type = type.ToString(), message = condition });
                };
                Application.logMessageReceived += handler;
            }

            var execSw = Stopwatch.StartNew();
            object result;
            try
            {
                try
                {
                    result = method.Invoke(null, null);
                }
                catch (TargetInvocationException tie)
                {
                    execSw.Stop();
                    timings["execute_ms"] = execSw.ElapsedMilliseconds;
                    return BuildRuntimeError(tie.InnerException ?? tie, stacktraceMode);
                }
            }
            finally
            {
                if (handler != null) Application.logMessageReceived -= handler;
            }
            execSw.Stop();
            timings["execute_ms"] = execSw.ElapsedMilliseconds;

            var serSw = Stopwatch.StartNew();
            var serialized = Serialize(result, 0, depth,
                new HashSet<object>(ReferenceEqualityComparer.Instance));
            serSw.Stop();
            timings["serialize_ms"] = serSw.ElapsedMilliseconds;

            if (strict && logged.Count > 0)
            {
                var first = logged[0];
                var summary = first.message != null && first.message.Length > 200
                    ? first.message.Substring(0, 200) + "..."
                    : first.message;
                var msg = logged.Count == 1
                    ? $"{first.type}: {summary}"
                    : $"{first.type}: {summary} (+{logged.Count - 1} more)";
                return new ErrorResponse("EXEC_LOGGED_ERROR",
                    "Snippet logged error(s) in strict mode: " + msg,
                    data: new { logged_errors = logged, returned = serialized });
            }

            return new SuccessResponse("OK", serialized);
        }

        // BuildRuntimeError shapes the EXEC_RUNTIME_ERROR envelope according to
        // the --stacktrace mode that AGENT.md Rule 6 documents:
        //   "none" — no stack_trace field at all
        //   "user" — stack_trace filtered through FilterUserFrames (default)
        //   "full" — raw inner.StackTrace verbatim
        private static object BuildRuntimeError(Exception inner, string mode)
        {
            var innerType = inner.GetType();
            object data;
            switch (mode)
            {
                case "none":
                    data = new { exception_type = innerType.FullName };
                    break;
                case "full":
                    data = new { exception_type = innerType.FullName, stack_trace = inner.StackTrace };
                    break;
                default: // "user"
                    data = new { exception_type = innerType.FullName, stack_trace = FilterUserFrames(inner.StackTrace) };
                    break;
            }
            return new ErrorResponse(
                "EXEC_RUNTIME_ERROR",
                $"Your C# snippet threw {innerType.Name}: {inner.Message}",
                data: data);
        }

        // FilterUserFrames strips framework frames (UnityEngine.*, UnityEditor.*,
        // System.*, reflection wrappers) from a raw stack trace and collapses
        // the synthetic wrapper frame to "(your snippet)". The result reads as
        // if the user had written a regular method — agents stop spending
        // tokens parsing through internal frames.
        private static string FilterUserFrames(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            var lines = raw.Split('\n');
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                var trimmed = line.TrimEnd();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (trimmed.Contains("at UnityEngine.") ||
                    trimmed.Contains("at UnityEditor.") ||
                    trimmed.Contains("at System.") ||
                    trimmed.Contains("(wrapper") ||
                    trimmed.Contains("System.Reflection") ||
                    trimmed.Contains("RuntimeMethodHandle.InvokeMethod") ||
                    trimmed.Contains("MethodBase.Invoke"))
                    continue;
                if (trimmed.Contains("__CliDynamic.Execute"))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append("  at (your snippet)");
                    continue;
                }
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(trimmed);
            }
            return sb.ToString();
        }

        private static object Serialize(object obj, int depth, int maxDepth, HashSet<object> visited)
        {
            if (obj == null) return null;
            if (depth > maxDepth) return obj.ToString();
            var type = obj.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal)) return obj;
            if (type.IsEnum) return obj.ToString();
            if (type.Name.StartsWith("FixedString")) return obj.ToString();
            if (obj is IDictionary dict)
            {
                var r = new Dictionary<string, object>();
                foreach (DictionaryEntry e in dict)
                    r[e.Key.ToString()] = Serialize(e.Value, depth + 1, maxDepth, visited);
                return r;
            }

            // Unity Objects (Component, ScriptableObject, etc.) implement IEnumerable
            // for children iteration on some subtypes. Serialize as object first so
            // properties (Transform.position, Scene.name, ...) survive instead of
            // returning an empty children list.
            var isUnityObject = obj is UnityEngine.Object;
            if (!isUnityObject && obj is IEnumerable enumerable)
            {
                const int limit = 100;
                var list = new List<object>();
                int count = 0;
                bool truncated = false;
                foreach (var item in enumerable)
                {
                    if (count >= limit) { truncated = true; break; }
                    list.Add(Serialize(item, depth + 1, maxDepth, visited));
                    count++;
                }
                if (truncated)
                {
                    list.Add(new Dictionary<string, object>
                    {
                        ["__truncated"] = true,
                        ["returned"] = limit,
                        ["hint"] = $"output capped at {limit} items — filter at source or paginate"
                    });
                }
                return list;
            }

            if (type.IsValueType || type.IsClass)
            {
                // Reference-equality cycle guard for class instances. Value types are
                // copied so they cannot form a real cycle — only class refs are tracked.
                if (!type.IsValueType)
                {
                    if (visited.Contains(obj)) return $"<cycle: {type.Name}>";
                    visited.Add(obj);
                }

                var r = new Dictionary<string, object>();
                foreach (var m in GetSerializableMembers(type))
                {
                    if (m is FieldInfo f)
                    {
                        try { r[f.Name] = Serialize(f.GetValue(obj), depth + 1, maxDepth, visited); }
                        catch (Exception ex) { r[f.Name] = $"<error: {ex.GetType().Name}>"; }
                    }
                    else if (m is PropertyInfo prop)
                    {
                        try { r[prop.Name] = Serialize(prop.GetValue(obj), depth + 1, maxDepth, visited); }
                        catch (Exception ex)
                        {
                            var inner = ex is TargetInvocationException tie ? tie.InnerException ?? tie : ex;
                            r[prop.Name] = $"<error: {inner.GetType().Name}>";
                        }
                    }
                }
                if (r.Count > 0) return r;
            }
            return obj.ToString();
        }

        private static MemberInfo[] GetSerializableMembers(Type type)
        {
            if (s_SerializableMembersCache.TryGetValue(type, out var members))
                return members;

            var list = new List<MemberInfo>();
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == type) continue;
                if (f.GetCustomAttribute<ObsoleteAttribute>() != null) continue;
                list.Add(f);
            }
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;
                if (prop.PropertyType == type) continue;
                // Obsolete shortcut accessors (Component.audio, .camera, .rigidbody, ...)
                // throw NotSupportedException at runtime and would spam responses.
                if (prop.GetCustomAttribute<ObsoleteAttribute>() != null) continue;
                list.Add(prop);
            }
            members = list.ToArray();
            s_SerializableMembersCache[type] = members;
            return members;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
