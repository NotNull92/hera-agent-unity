using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    public static partial class ExecuteCsharp
    {
        private const string LangVersion = "latest";

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

        private static CompileResult CompileToBytes(string source, int userLineOffset, string cscOverride, string dotnetOverride, bool useCache = true)
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
                // Write the snippet WITH a UTF-8 BOM so csc unambiguously detects
                // UTF-8 and never falls back to the system code page (CP949 on
                // Korean Windows) to read the source — that fallback needs
                // System.Text.Encoding.CodePages, which the .NET-Core Roslyn
                // runtime (Unity 6.5+ `dotnet exec csc.dll`) does not ship. The
                // .rsp stays BOM-less (a BOM there would prepend to the first arg
                // and break flag parsing).
                File.WriteAllText(srcFile, source, new UTF8Encoding(true));

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
                // Emit compiler output as UTF-8 instead of the console's OEM code
                // page. Without this, csc on the .NET-Core Roslyn path (Unity 6.5+,
                // `dotnet exec csc.dll`) encodes its redirected output via
                // Encoding.GetEncoding(949) on Korean Windows, which needs
                // System.Text.Encoding.CodePages — absent from the bundled runtime —
                // so csc crashes before compiling anything (every exec returned
                // EXEC_COMPILE_ERROR). Mono csc.exe (Unity 6.0–6.4) also accepts the
                // flag, so this stays version-agnostic and can't regress 6.3/6.4.
                rsp.AppendLine("-utf8output");
                rsp.AppendLine($"-langversion:{LangVersion}");
                if (useCache)
                    rsp.AppendLine($"@\"{ExecCompileCache.GetRefRspPath()}\"");
                else
                    ExecCompileCache.AppendUncachedReferenceArguments(rsp);
                rsp.AppendLine($"\"{srcFile}\"");
                File.WriteAllText(rspFile, rsp.ToString(), utf8);

                var rspArg = $"@\"{rspFile}\"";
                var csc = useCache
                    ? ExecCompileCache.ResolveCsc(cscOverride)
                    : ExecCompileCache.ResolveCscUncached(cscOverride);
                string exe, args;

                if (csc != null && csc.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var dotnet = useCache
                        ? ExecCompileCache.ResolveDotnet(dotnetOverride)
                        : ExecCompileCache.ResolveDotnetUncached(dotnetOverride);
                    if (dotnet == null)
                        return new CompileResult { Error = new ErrorResponse(
                            "EXEC_DOTNET_NOT_FOUND",
                            "Cannot find dotnet runtime under: " +
                            EditorApplication.applicationContentsPath,
                            suggestions: new List<string> { "Pass --dotnet <path-to-dotnet>" }) };
                    exe = dotnet;
                    args = $"exec \"{csc}\" {rspArg} /shared";
                }
                else if (csc != null && Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // Windows can exec the csc.exe PE directly.
                    exe = csc;
                    args = $"{rspArg} /shared";
                }
                else if (csc != null)
                {
                    // csc.exe is a Windows-PE managed assembly; macOS/Linux cannot
                    // exec it directly and must run it through the bundled Mono host.
                    var mono = useCache ? ExecCompileCache.ResolveMono() : ExecCompileCache.ResolveMonoUncached();
                    if (mono == null)
                        return new CompileResult { Error = new ErrorResponse(
                            "EXEC_MONO_NOT_FOUND",
                            "Found a Windows csc.exe but no Mono host to run it under: " +
                            EditorApplication.applicationContentsPath,
                            suggestions: new List<string>
                            {
                                "Pass --csc <path-to-csc.dll> to use the .NET Roslyn compiler instead",
                                "Pass --dotnet <path-to-dotnet>"
                            }) };
                    exe = mono;
                    args = $"\"{csc}\" {rspArg} /shared";
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
                    // An empty working directory makes some hosts resolve the launcher
                    // relative to a bogus CWD; anchor it to our temp dir.
                    WorkingDirectory = tmpDir,
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
                        $"Failed to launch compiler process (platform={Application.platform}, " +
                        $"launcher={exe}, compiler={csc}): {ex.Message}",
                        suggestions: new List<string>
                        {
                            "Check antivirus/sandbox is not blocking the compiler",
                            $"Verify launcher exists: {exe}"
                        }) };
                }

                if (proc == null)
                {
                    return new CompileResult { Error = new ErrorResponse(
                        "EXEC_LAUNCH_FAILED",
                        $"Process.Start returned null; compiler did not launch " +
                        $"(platform={Application.platform}, launcher={exe}, compiler={csc}).",
                        suggestions: new List<string>
                        {
                            "Check antivirus/sandbox is not blocking the compiler",
                            $"Verify launcher exists: {exe}"
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
    }
}
