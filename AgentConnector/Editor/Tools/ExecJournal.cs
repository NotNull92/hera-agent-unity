using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace HeraAgent.Tools
{
    internal static class ExecJournal
    {
        internal const int MaxBytes = 1024 * 1024;

        internal sealed class Observation
        {
            internal string Mode = "full";
            internal string CompileCache = "miss";
            internal readonly string DomainEpoch = ToolCatalogRuntime.DomainEpoch;
            internal readonly Dictionary<string, long> Timings = new Dictionary<string, long>();
        }

        internal static void Append(string code, string operationId, Observation observation, object response, string directory = null)
        {
            try
            {
                var path = Path.Combine(directory ?? ExecCompileCache.CacheDir, "exec-journal.jsonl");
                using var sha = SHA256.Create();
                var hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(code))).Replace("-", "").ToLowerInvariant();
                observation.Timings.TryGetValue("compile_ms", out var compileMs);
                observation.Timings.TryGetValue("execute_ms", out var executeMs);
                var row = new
                {
                    schema_version = 1,
                    ts_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    operation_id = operationId,
                    code_sha256 = hash,
                    mode = observation.Mode,
                    compile_cache = observation.CompileCache,
                    compile_ms = compileMs,
                    execute_ms = executeMs,
                    outcome = response is SuccessResponse ? "ok" : (response as ErrorResponse)?.code ?? "TOOL_FAILED",
                    domain_epoch = observation.DomainEpoch,
                };
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path, JsonConvert.SerializeObject(row) + "\n", new UTF8Encoding(false));
                if (new FileInfo(path).Length <= MaxBytes) return;
                string tail;
                using (var stream = File.OpenRead(path))
                {
                    stream.Seek(-MaxBytes / 2, SeekOrigin.End);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    reader.ReadLine(); // Discard the partial oldest row at the byte boundary.
                    tail = reader.ReadToEnd();
                }
                AtomicFile.WriteAllText(path, tail);
            }
            catch
            {
                // Journal failures must never change an exec response or its approval/ledger outcome.
            }
        }
    }
}
