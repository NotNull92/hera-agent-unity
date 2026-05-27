using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraTool(Name = "exec", Description = "Execute arbitrary C# code at runtime. Full access to Unity and all loaded assemblies.")]
    public static class ExecuteCsharp
    {
        private const string LangVersion = "latest";

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

        public class Parameters
        {
            [ToolParameter("C# code to execute. Use 'return' for output.", Required = true)]
            public string Code { get; set; }

            [ToolParameter("Additional using directives (comma-separated, e.g. Unity.Entities,Unity.Mathematics)")]
            public string[] Usings { get; set; }

            [ToolParameter("Path to csc compiler (csc.dll or csc.exe). Auto-detected if omitted.")]
            public string Csc { get; set; }

            [ToolParameter("Path to dotnet runtime. Auto-detected if omitted.")]
            public string Dotnet { get; set; }

            [ToolParameter("Skip compile/assembly cache. Forces a fresh csc invocation.")]
            public bool NoCache { get; set; }

            [ToolParameter("Max object graph depth in serialized return value (default 3, max 8).")]
            public int Depth { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            var p = new ToolParams(parameters);
            var code = p.Get("code")
                ?? (p.GetRaw("args") as JArray)?[0]?.ToString();
            if (string.IsNullOrEmpty(code))
                return new ErrorResponse("MISSING_PARAM", "'code' required",
                    suggestions: new List<string> { "Pass code as first positional arg or --code <text>" });

            var usingsToken = p.GetRaw("usings");
            var extraUsings = new List<string>();
            if (usingsToken != null)
            {
                if (usingsToken.Type == JTokenType.Array)
                    extraUsings.AddRange(usingsToken.ToObject<string[]>());
                else
                    extraUsings.AddRange(usingsToken.ToString().Split(','));
            }

            var cscOverride = p.Get("csc");
            var dotnetOverride = p.Get("dotnet");
            var compileOnly = p.GetBool("compile_only");
            var noCache = p.GetBool("no_cache") || p.GetBool("nocache") || p.GetBool("no-cache");
            var depth = ClampDepth(p.GetInt("depth") ?? 0);

            return CompileAndExecute(BuildSource(code, extraUsings), cscOverride, dotnetOverride, compileOnly, noCache, depth);
        }

        private const int DefaultSerializeDepth = 3;
        private const int MaxSerializeDepth = 8;

        private static int ClampDepth(int requested)
        {
            if (requested <= 0) return DefaultSerializeDepth;
            return requested > MaxSerializeDepth ? MaxSerializeDepth : requested;
        }

        private static string BuildSource(string code, List<string> extraUsings)
        {
            var sb = new StringBuilder();
            foreach (var u in DefaultUsings)
                sb.AppendLine($"using {u};");
            foreach (var u in extraUsings)
                sb.AppendLine($"using {u};");

            sb.AppendLine();
            sb.AppendLine("public static class __CliDynamic {");
            sb.AppendLine("  public static object Execute() {");
            sb.AppendLine(code);
            sb.AppendLine("  }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static object CompileAndExecute(string source, string cscOverride, string dotnetOverride, bool compileOnly, bool noCache, int depth)
        {
            var timings = new Dictionary<string, long>();
            string cacheKey;
            try
            {
                cacheKey = ExecCompileCache.ComputeKey(source, LangVersion);
            }
            catch (Exception ex)
            {
                return new ErrorResponse("EXEC_INTERNAL_ERROR",
                    $"Internal error preparing exec cache: {ex.Message}");
            }

            Assembly compiled = null;
            object transientAlc = null; // ALC owned by this call when --no-cache; unloaded after Invoke

            // cache: 0 = freshly compiled, 1 = disk hit, 2 = memory hit
            long cacheState = 0;

            if (!noCache && ExecCompileCache.TryGetAssembly(cacheKey, out compiled))
            {
                timings["compile_ms"] = 0;
                timings["load_ms"] = 0;
                cacheState = 2;
                if (compileOnly)
                {
                    timings["cache"] = cacheState;
                    var ok = new SuccessResponse("Compile OK", null);
                    ResponseTimings.Merge(ok, timings);
                    return ok;
                }
            }

            var dllPath = Path.Combine(ExecCompileCache.BinCacheDir, cacheKey + ".dll");
            if (compiled == null && !noCache && File.Exists(dllPath))
            {
                if (compileOnly)
                {
                    // A prior successful compile produced this DLL — sufficient
                    // evidence the source still type-checks. Skip the load step.
                    timings["compile_ms"] = 0;
                    timings["cache"] = 1;
                    var ok = new SuccessResponse("Compile OK", null);
                    ResponseTimings.Merge(ok, timings);
                    return ok;
                }
                var loadSw = Stopwatch.StartNew();
                try
                {
                    var loaded = LoadAssembly(File.ReadAllBytes(dllPath), cacheKey);
                    compiled = loaded.Assembly;
                    ExecCompileCache.StoreAssembly(cacheKey, compiled, loaded.LoadContext);
                    timings["compile_ms"] = 0;
                    timings["load_ms"] = loadSw.ElapsedMilliseconds;
                    cacheState = 1;
                }
                catch
                {
                    compiled = null;
                }
                loadSw.Stop();
            }

            if (compiled == null)
            {
                var compileSw = Stopwatch.StartNew();
                var compileResult = CompileToBytes(source, cscOverride, dotnetOverride);
                compileSw.Stop();
                timings["compile_ms"] = compileSw.ElapsedMilliseconds;
                if (compileResult.Error != null)
                {
                    ResponseTimings.Merge(compileResult.Error, timings);
                    return compileResult.Error;
                }

                try
                {
                    Directory.CreateDirectory(ExecCompileCache.BinCacheDir);
                    File.WriteAllBytes(dllPath, compileResult.Bytes);
                }
                catch { }

                if (compileOnly)
                {
                    // Compiled cleanly; the persisted DLL above is enough for any
                    // subsequent real exec. No need to load or invoke.
                    timings["cache"] = 0;
                    var ok = new SuccessResponse("Compile OK", null);
                    ResponseTimings.Merge(ok, timings);
                    return ok;
                }

                var loadSw = Stopwatch.StartNew();
                LoadedAssembly loaded;
                try
                {
                    loaded = LoadAssembly(compileResult.Bytes, cacheKey);
                }
                catch (Exception ex)
                {
                    loadSw.Stop();
                    timings["load_ms"] = loadSw.ElapsedMilliseconds;
                    var err = new ErrorResponse("EXEC_LOAD_FAILED",
                        $"Failed to load compiled assembly: {ex.Message}");
                    ResponseTimings.Merge(err, timings);
                    return err;
                }
                loadSw.Stop();
                timings["load_ms"] = loadSw.ElapsedMilliseconds;

                compiled = loaded.Assembly;
                if (!noCache)
                    ExecCompileCache.StoreAssembly(cacheKey, compiled, loaded.LoadContext);
                else
                    transientAlc = loaded.LoadContext;
            }

            timings["cache"] = cacheState;
            var result = Invoke(compiled, timings, depth);
            ResponseTimings.Merge(result, timings);

            // For --no-cache we own the ALC and must unload it. Serialize already
            // copied the result into primitive containers, so the response holds
            // no live references into the compiled assembly's type graph.
            if (transientAlc != null)
                ExecCompileCache.TryUnload(transientAlc);

            return result;
        }

        private struct CompileResult
        {
            public byte[] Bytes;
            public ErrorResponse Error;
        }

        private static CompileResult CompileToBytes(string source, string cscOverride, string dotnetOverride)
        {
            var utf8 = new UTF8Encoding(false);
            var tmpDir = Path.Combine(Path.GetTempPath(), "hera-agent-unity-exec");
            Directory.CreateDirectory(tmpDir);

            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            var srcFile = Path.Combine(tmpDir, $"{id}.cs");
            var outFile = Path.Combine(tmpDir, $"{id}.dll");
            var rspFile = Path.Combine(tmpDir, $"{id}.rsp");

            try
            {
                File.WriteAllText(srcFile, source, utf8);

                var refsRsp = ExecCompileCache.GetRefRspPath();

                var rsp = new StringBuilder();
                rsp.AppendLine("-target:library");
                rsp.AppendLine($"-out:\"{outFile}\"");
                rsp.AppendLine("-nologo");
                rsp.AppendLine("-nowarn:0105,1701,1702");
                rsp.AppendLine($"-langversion:{LangVersion}");
                rsp.AppendLine($"@\"{refsRsp}\"");
                rsp.AppendLine($"\"{srcFile}\"");
                File.WriteAllText(rspFile, rsp.ToString(), utf8);

                var rspArg = $"@\"{rspFile}\"";
                var csc = ExecCompileCache.ResolveCsc(cscOverride);
                string exe, args;

                if (csc != null && csc.EndsWith(".dll"))
                {
                    var dotnet = ExecCompileCache.ResolveDotnet(dotnetOverride);
                    if (dotnet == null)
                        return new CompileResult { Error = new ErrorResponse(
                            "EXEC_DOTNET_NOT_FOUND",
                            "Cannot find dotnet runtime under: " +
                            EditorApplication.applicationContentsPath,
                            suggestions: new List<string> { "Pass --dotnet <path-to-dotnet>" }) };
                    exe = dotnet;
                    args = $"exec \"{csc}\" {rspArg} /shared";
                }
                else if (csc != null)
                {
                    exe = csc;
                    args = $"{rspArg} /shared";
                }
                else
                {
                    return new CompileResult { Error = new ErrorResponse(
                        "EXEC_CSC_NOT_FOUND",
                        "Cannot find csc compiler under: " +
                        EditorApplication.applicationContentsPath,
                        suggestions: new List<string> { "Pass --csc <path-to-csc.dll-or-csc.exe>" }) };
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                Process proc;
                try
                {
                    proc = Process.Start(psi);
                }
                catch (Exception ex)
                {
                    return new CompileResult { Error = new ErrorResponse(
                        "EXEC_LAUNCH_FAILED",
                        $"Failed to launch compiler process: {ex.Message}",
                        suggestions: new List<string>
                        {
                            "Check antivirus/sandbox is not blocking csc",
                            $"Verify executable exists: {exe}"
                        }) };
                }

                if (proc == null)
                {
                    return new CompileResult { Error = new ErrorResponse(
                        "EXEC_LAUNCH_FAILED",
                        "Process.Start returned null. Compiler did not launch.",
                        suggestions: new List<string>
                        {
                            "Check antivirus/sandbox is not blocking csc",
                            $"Verify executable exists: {exe}"
                        }) };
                }

                using (proc)
                {
                    // Async drain so a large stderr (hundreds of compile errors) cannot
                    // fill the pipe buffer and deadlock the synchronous read sibling.
                    var stdoutSb = new StringBuilder();
                    var stderrSb = new StringBuilder();
                    proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutSb.AppendLine(e.Data); };
                    proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrSb.AppendLine(e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    if (!proc.WaitForExit(30000))
                    {
                        try { proc.Kill(); } catch { }
                        return new CompileResult { Error = new ErrorResponse(
                            "EXEC_COMPILE_TIMEOUT",
                            "Compilation timed out (30s). The compiler process was killed.") };
                    }
                    // WaitForExit(int) returning true does not guarantee the async
                    // pipe drains have flushed. The parameterless overload does.
                    proc.WaitForExit();

                    if (proc.ExitCode != 0)
                    {
                        var stdout = stdoutSb.ToString();
                        var stderr = stderrSb.ToString();
                        var output = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                        var parsed = ParseErrors(output);
                        return new CompileResult { Error = new ErrorResponse(
                            "EXEC_COMPILE_ERROR",
                            $"Compile error:\n{FormatErrors(output)}",
                            data: new { compile_errors = parsed }) };
                    }
                }

                return new CompileResult { Bytes = File.ReadAllBytes(outFile) };
            }
            finally
            {
                try { File.Delete(srcFile); } catch { }
                try { File.Delete(outFile); } catch { }
                try { File.Delete(rspFile); } catch { }
            }
        }

        private struct LoadedAssembly
        {
            public Assembly Assembly;
            public object LoadContext; // collectible ALC; null when fallback to Assembly.Load
        }

        private static LoadedAssembly LoadAssembly(byte[] bytes, string id)
        {
            try
            {
                var alcType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader");
                if (alcType != null)
                {
                    var ctor = alcType.GetConstructor(new[] { typeof(string), typeof(bool) });
                    var alc = ctor?.Invoke(new object[] { "hera-agent-unity-exec-" + id, true });
                    var loadMethod = alcType.GetMethod("LoadFromStream", new[] { typeof(Stream) });
                    if (alc != null && loadMethod != null)
                    {
                        using (var ms = new MemoryStream(bytes))
                        {
                            var asm = (Assembly)loadMethod.Invoke(alc, new object[] { ms });
                            return new LoadedAssembly { Assembly = asm, LoadContext = alc };
                        }
                    }
                }
            }
            catch { }
            return new LoadedAssembly { Assembly = Assembly.Load(bytes), LoadContext = null };
        }

        private static object Invoke(Assembly compiled, Dictionary<string, long> timings, int depth)
        {
            var method = compiled.GetType("__CliDynamic")?.GetMethod("Execute");
            if (method == null)
                return new ErrorResponse("EXEC_INTERNAL_ERROR",
                    "Internal error: compiled type or method not found.");

            var execSw = Stopwatch.StartNew();
            object result;
            try
            {
                result = method.Invoke(null, null);
            }
            catch (TargetInvocationException tie)
            {
                execSw.Stop();
                timings["execute_ms"] = execSw.ElapsedMilliseconds;
                var inner = tie.InnerException ?? tie;
                return new ErrorResponse("EXEC_RUNTIME_ERROR",
                    $"Runtime error: {inner.GetType().Name}: {inner.Message}",
                    data: new
                    {
                        exception_type = inner.GetType().FullName,
                        stack_trace = inner.StackTrace
                    });
            }
            execSw.Stop();
            timings["execute_ms"] = execSw.ElapsedMilliseconds;

            var serSw = Stopwatch.StartNew();
            var serialized = Serialize(result, 0, depth,
                new HashSet<object>(ReferenceEqualityComparer.Instance));
            serSw.Stop();
            timings["serialize_ms"] = serSw.ElapsedMilliseconds;
            return new SuccessResponse("OK", serialized);
        }

        private static string FormatErrors(string raw)
        {
            var lines = raw.Split('\n');
            var errors = new List<string>();
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var m = Regex.Match(trimmed, @"\((\d+),\d+\):\s*error\s+\w+:\s*(.+)");
                if (m.Success)
                    errors.Add($"L{m.Groups[1].Value}: {m.Groups[2].Value}");
                else if (trimmed.Contains("error"))
                    errors.Add(trimmed);
            }
            return errors.Count > 0 ? string.Join("\n", errors) : raw;
        }

        private static List<object> ParseErrors(string raw)
        {
            var lines = raw.Split('\n');
            var parsed = new List<object>();
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var m = Regex.Match(trimmed, @"\((\d+),(\d+)\):\s*error\s+(\w+):\s*(.+)");
                if (m.Success)
                {
                    parsed.Add(new
                    {
                        line = int.Parse(m.Groups[1].Value),
                        col = int.Parse(m.Groups[2].Value),
                        error_code = m.Groups[3].Value,
                        message = m.Groups[4].Value
                    });
                }
            }
            return parsed;
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
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (f.FieldType == type) continue;
                    if (f.GetCustomAttribute<ObsoleteAttribute>() != null) continue;
                    try { r[f.Name] = Serialize(f.GetValue(obj), depth + 1, maxDepth, visited); }
                    catch (Exception ex) { r[f.Name] = $"<error: {ex.GetType().Name}>"; }
                }
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanRead) continue;
                    if (prop.GetIndexParameters().Length > 0) continue;
                    if (prop.PropertyType == type) continue;
                    // Obsolete shortcut accessors (Component.audio, .camera, .rigidbody, ...)
                    // throw NotSupportedException at runtime and would spam responses.
                    if (prop.GetCustomAttribute<ObsoleteAttribute>() != null) continue;
                    try { r[prop.Name] = Serialize(prop.GetValue(obj), depth + 1, maxDepth, visited); }
                    catch (Exception ex)
                    {
                        var inner = ex is TargetInvocationException tie ? tie.InnerException ?? tie : ex;
                        r[prop.Name] = $"<error: {inner.GetType().Name}>";
                    }
                }
                if (r.Count > 0) return r;
            }
            return obj.ToString();
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
