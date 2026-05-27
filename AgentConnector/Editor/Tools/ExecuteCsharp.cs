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

            [ToolParameter("Compile-only dry run. Returns success on a clean compile, EXEC_COMPILE_ERROR otherwise. No Execute() call, no side effects.")]
            public bool CompileOnly { get; set; }

            [ToolParameter("Skip compile/assembly cache. Forces a fresh csc invocation.")]
            public bool NoCache { get; set; }

            [ToolParameter("EXEC_RUNTIME_ERROR stack-trace mode: 'none' (exception_type only), 'user' (drop framework frames, default), 'full' (raw inner.StackTrace).")]
            public string Stacktrace { get; set; }

            [ToolParameter("Capture Debug.LogError/LogException/LogAssert raised during the snippet and surface them as EXEC_LOGGED_ERROR even if Execute() returned normally.")]
            public bool Strict { get; set; }

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
            var stacktrace = (p.Get("stacktrace") ?? "user").ToLowerInvariant();
            var strict = p.GetBool("strict");
            var depth = ClampDepth(p.GetInt("depth") ?? 0);

            var built = BuildSource(code, extraUsings);
            return CompileAndExecute(built.Source, built.UserCodeLineOffset, cscOverride, dotnetOverride, compileOnly, noCache, stacktrace, depth, strict);
        }

        private struct BuiltSource
        {
            public string Source;
            // csc reports diagnostics against the wrapped source. Subtract this
            // offset from a raw error line to map back to the user's snippet
            // line (1-based) so AGENT.md's "L<line>: <message>" hints land on
            // the actual offending line of the user's code.
            public int UserCodeLineOffset;
        }

        private const int DefaultSerializeDepth = 3;
        private const int MaxSerializeDepth = 8;

        private static int ClampDepth(int requested)
        {
            if (requested <= 0) return DefaultSerializeDepth;
            return requested > MaxSerializeDepth ? MaxSerializeDepth : requested;
        }

        private static BuiltSource BuildSource(string code, List<string> extraUsings)
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
            // Fallthrough so snippets without a trailing `return` still compile,
            // resolving to null. AGENT.md documents this — Rule 1's "no return"
            // examples rely on it. CS0162 is suppressed in CompileToBytes for
            // the case where the user's snippet already returns.
            sb.AppendLine("    return null;");
            sb.AppendLine("  }");
            sb.AppendLine("}");

            // Wrapper above user code: N usings + 1 blank line + `class` line
            // + method-header line. The first line of the user's snippet sits
            // at line (offset + 1) in the wrapped source.
            int offset = DefaultUsings.Length + extraUsings.Count + 3;
            return new BuiltSource { Source = sb.ToString(), UserCodeLineOffset = offset };
        }

        private static object CompileAndExecute(string source, int userLineOffset, string cscOverride, string dotnetOverride, bool compileOnly, bool noCache, string stacktraceMode, int depth, bool strict)
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
                var compileResult = CompileToBytes(source, userLineOffset, cscOverride, dotnetOverride);
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
            var result = Invoke(compiled, timings, stacktraceMode, depth, strict);
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

        // Per-call temp dir under %TEMP%\hera-agent-unity-exec collects .cs / .rsp
        // files that should be deleted right after csc returns, but a hard kill of
        // the editor or a stale file lock can leave debris behind. Sweep on each
        // call to bound the directory.
        private static void CleanupOldTempFiles(string tmpDir)
        {
            try
            {
                if (!Directory.Exists(tmpDir)) return;
                var cutoff = DateTime.Now.AddHours(-24);
                foreach (var file in Directory.GetFiles(tmpDir))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoff)
                            File.Delete(file);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static CompileResult CompileToBytes(string source, int userLineOffset, string cscOverride, string dotnetOverride)
        {
            var utf8 = new UTF8Encoding(false);
            var tmpDir = Path.Combine(Path.GetTempPath(), "hera-agent-unity-exec");
            Directory.CreateDirectory(tmpDir);
            CleanupOldTempFiles(tmpDir);

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
                // 0162: unreachable code — the auto-appended `return null;` below
                // is unreachable when the user's snippet already ends with a return.
                rsp.AppendLine("-nowarn:0105,0162,1701,1702");
                // Force English csc diagnostics. On Korean / Japanese / Chinese
                // Windows the compiler emits localized error messages in the
                // system code page (CP949 etc.), and reading them as UTF-8 via
                // StandardErrorEncoding produces mojibake in the response.
                rsp.AppendLine("-preferreduilang:en-US");
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
                        var parsed = ParseErrors(output, userLineOffset);
                        return new CompileResult { Error = new ErrorResponse(
                            "EXEC_COMPILE_ERROR",
                            $"Compile error:\n{FormatErrors(output, userLineOffset)}",
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
            object data;
            switch (mode)
            {
                case "none":
                    data = new { exception_type = inner.GetType().FullName };
                    break;
                case "full":
                    data = new { exception_type = inner.GetType().FullName, stack_trace = inner.StackTrace };
                    break;
                default: // "user"
                    data = new { exception_type = inner.GetType().FullName, stack_trace = FilterUserFrames(inner.StackTrace) };
                    break;
            }
            return new ErrorResponse(
                "EXEC_RUNTIME_ERROR",
                $"Your C# snippet threw {inner.GetType().Name}: {inner.Message}",
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

        private static string FormatErrors(string raw, int userLineOffset)
        {
            var lines = raw.Split('\n');
            var errors = new List<string>();
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var m = Regex.Match(trimmed, @"\((\d+),\d+\):\s*error\s+\w+:\s*(.+)");
                if (m.Success)
                {
                    int userLine = Math.Max(1, int.Parse(m.Groups[1].Value) - userLineOffset);
                    errors.Add($"L{userLine}: {m.Groups[2].Value}");
                }
                else if (trimmed.Contains("error"))
                    errors.Add(trimmed);
            }
            return errors.Count > 0 ? string.Join("\n", errors) : raw;
        }

        private static List<object> ParseErrors(string raw, int userLineOffset)
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
                        line = Math.Max(1, int.Parse(m.Groups[1].Value) - userLineOffset),
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
