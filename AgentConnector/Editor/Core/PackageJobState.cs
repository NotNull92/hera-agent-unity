using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
// `using UnityEditor;` also pulls in the legacy AssetStore PackageInfo type;
// alias the PackageManager one so bare `PackageInfo` is unambiguous.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HeraAgent
{
    /// <summary>
    /// Tracks asynchronous Package Manager jobs across domain reloads. add /
    /// remove / embed often complete fast enough to finish before Unity's
    /// resolver triggers a reload, but git-URL installs and large packages
    /// can straddle one. This class wires Client.* request polling into
    /// EditorApplication.update and reattaches a Client.List-based verifier
    /// via [InitializeOnLoad] when the domain comes back so the CLI's
    /// package-result file always materialises.
    /// </summary>
    [InitializeOnLoad]
    public static class PackageJobState
    {
        internal static readonly string StatusDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".hera-agent-unity", "status");

        private const long StaleJobMs = 10 * 60 * 1000;

        static PackageJobState()
        {
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        public static void MarkPending(int port, string jobId, string action, string identifier)
        {
            try
            {
                Directory.CreateDirectory(StatusDir);
                var pending = new
                {
                    job_id = jobId,
                    port,
                    action,
                    identifier,
                    started_unix_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
                File.WriteAllText(PendingPath(port, jobId), JsonConvert.SerializeObject(pending));
            }
            catch { }
        }

        public static void ClearPending(int port, string jobId)
        {
            try
            {
                var p = PendingPath(port, jobId);
                if (File.Exists(p)) File.Delete(p);
            }
            catch { }
        }

        public static void AttachWatcher(int port, string jobId, string action, string identifier, Request request)
        {
            EditorApplication.CallbackFunction watcher = null;
            watcher = () =>
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= watcher;
                WriteResult(port, jobId, action, identifier, request);
                ClearPending(port, jobId);
            };
            EditorApplication.update += watcher;
        }

        static void OnAfterAssemblyReload()
        {
            try
            {
                if (!Directory.Exists(StatusDir)) return;

                foreach (var file in Directory.GetFiles(StatusDir, "package-pending-*.json"))
                {
                    string json;
                    try { json = File.ReadAllText(file); }
                    catch { continue; }

                    JObject pending;
                    try { pending = JObject.Parse(json); }
                    catch { TryDelete(file); continue; }

                    int port = pending["port"]?.Value<int>() ?? 0;
                    string jobId = pending["job_id"]?.Value<string>();
                    string action = pending["action"]?.Value<string>();
                    string identifier = pending["identifier"]?.Value<string>();
                    long startedMs = pending["started_unix_ms"]?.Value<long>() ?? 0;

                    if (port == 0 || string.IsNullOrEmpty(jobId) || string.IsNullOrEmpty(action) || string.IsNullOrEmpty(identifier))
                    {
                        TryDelete(file);
                        continue;
                    }

                    var ageMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startedMs;
                    if (ageMs > StaleJobMs)
                    {
                        WriteTimeoutResult(port, jobId, action, identifier);
                        ClearPending(port, jobId);
                        continue;
                    }

                    ResumeJob(port, jobId, action, identifier);
                }
            }
            catch { }
        }

        // After a domain reload the in-flight Request handle is gone. Read
        // the post-reload package set and infer success/failure from whether
        // the intended identifier is present (or absent, for remove).
        static void ResumeJob(int port, string jobId, string action, string identifier)
        {
            var listRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);
            EditorApplication.CallbackFunction watcher = null;
            watcher = () =>
            {
                if (!listRequest.IsCompleted) return;
                EditorApplication.update -= watcher;
                WriteResumedResult(port, jobId, action, identifier, listRequest);
                ClearPending(port, jobId);
            };
            EditorApplication.update += watcher;
        }

        static void WriteResult(int port, string jobId, string action, string identifier, Request request)
        {
            object payload;
            if (request.Status >= StatusCode.Failure)
            {
                payload = new
                {
                    success = false,
                    message = $"{action} '{identifier}' failed: {request.Error?.message ?? "unknown error"}",
                    code = "PACKAGE_" + action.ToUpperInvariant() + "_FAILED",
                    data = new { action, identifier, error = request.Error?.message },
                };
            }
            else
            {
                PackageInfo pkg = null;
                if (request is AddRequest ar) pkg = ar.Result;
                else if (request is EmbedRequest er) pkg = er.Result;
                // RemoveRequest has no Result payload.

                payload = new
                {
                    success = true,
                    message = $"{action} '{identifier}' completed.",
                    data = new
                    {
                        action,
                        identifier,
                        package = pkg != null ? BuildPackageShallow(pkg) : null,
                    },
                };
            }
            TryWriteJson(ResultPath(port, jobId), payload);
        }

        static void WriteResumedResult(int port, string jobId, string action, string identifier, ListRequest listRequest)
        {
            if (listRequest.Status >= StatusCode.Failure)
            {
                TryWriteJson(ResultPath(port, jobId), new
                {
                    success = false,
                    message = $"{action} '{identifier}': could not verify post-reload state — Client.List failed: {listRequest.Error?.message}",
                    code = "PACKAGE_RESUME_LIST_FAILED",
                    data = new { action, identifier },
                });
                return;
            }

            PackageInfo found = null;
            foreach (var p in listRequest.Result)
            {
                if (string.Equals(p.name, identifier, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.packageId, identifier, StringComparison.OrdinalIgnoreCase))
                {
                    found = p;
                    break;
                }
            }

            bool succeeded = action == "remove" ? (found == null) : (found != null);

            TryWriteJson(ResultPath(port, jobId), new
            {
                success = succeeded,
                message = succeeded
                    ? $"{action} '{identifier}' completed (verified post-reload)."
                    : $"{action} '{identifier}' could not be verified after domain reload.",
                code = succeeded ? null : "PACKAGE_RESUME_VERIFY_FAILED",
                data = new
                {
                    action,
                    identifier,
                    package = found != null ? BuildPackageShallow(found) : null,
                },
            });
        }

        static void WriteTimeoutResult(int port, string jobId, string action, string identifier)
        {
            TryWriteJson(ResultPath(port, jobId), new
            {
                success = false,
                message = $"{action} '{identifier}' timed out (no result for >10m).",
                code = "PACKAGE_TIMEOUT",
                data = new { action, identifier },
            });
        }

        internal static object BuildPackageShallow(PackageInfo info)
        {
            return new
            {
                name = info.name,
                version = info.version,
                source = info.source.ToString(),
                resolved_path = info.resolvedPath,
                is_direct_dependency = info.isDirectDependency,
                display_name = info.displayName,
            };
        }

        static string PendingPath(int port, string jobId) =>
            Path.Combine(StatusDir, $"package-pending-{port}-{jobId}.json");

        internal static string ResultPath(int port, string jobId) =>
            Path.Combine(StatusDir, $"package-result-{port}-{jobId}.json");

        static void TryWriteJson(string path, object payload)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(payload));
            }
            catch { }
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
