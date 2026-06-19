using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraTool(Name = "exec", Description = "Execute arbitrary C# code at runtime. Full access to Unity and all loaded assemblies.")]
    public static partial class ExecuteCsharp
    {
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

        /// <summary>
        /// Compiles a trivial snippet in the background to warm the VBCSCompiler
        /// server after a domain reload. Called from HttpServer on startup; failures
        /// are silently ignored because this is purely an optimization.
        /// </summary>
        public static void PreWarmCompiler()
        {
            try
            {
                // Skip if the editor is still compiling scripts or importing assets —
                // the assembly reference set may be incomplete at this point.
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    return;

                var built = BuildSource("return null;", new List<string>());
                // Result bytes are intentionally discarded; the goal is only to keep
                // the compiler server process alive and JIT-warmed.
                CompileToBytes(built.Source, built.UserCodeLineOffset, null, null);
            }
            catch
            {
                // Best-effort optimization; never spam the console on failure.
            }
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
    }
}
