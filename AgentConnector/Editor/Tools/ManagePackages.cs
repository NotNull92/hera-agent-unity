using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "manage_packages",
        Description = "Unity Package Manager control. Actions: list (synchronous), add / remove / embed (asynchronous — return a job_id, poll the package-result file for completion). add accepts any Client.Add identifier: 'com.unity.x' registry name, 'com.unity.x@1.2.3' pinned version, 'https://github.com/.../repo.git[?path=...]' git URL, or 'file:..' local path. Avoids manifest.json hand-edits.",
        Examples = new[]
        {
            "manage_packages list",
            "manage_packages add com.unity.ai.navigation",
            "manage_packages add https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
            "manage_packages remove com.unity.ai.navigation",
            "manage_packages embed com.unity.test-framework",
        },
        ExampleDescriptions = new[]
        {
            "List every package the project currently resolves to (returns directly)",
            "Install a registry package (returns job_id; poll the result file)",
            "Install a git-URL package (asynchronous)",
            "Remove an installed package by name (asynchronous)",
            "Move a cached package into Packages/ for local edits (asynchronous)",
        },
        Profiles = new[] { "assets" },
        RiskClass = HeraRiskClass.PackageChange,
        MayReloadDomain = true,
        ContractMode = ToolContractMode.Strict)]
    public static class ManagePackages
    {
        public sealed class EmptyParameters
        {
        }

        public sealed class IdentifierParameters
        {
            [ToolParameter("Package identifier.", Required = true)]
            public string Identifier { get; set; }
        }

        public class Parameters
        {
            [ToolParameter(
                "Action: list, add, remove, embed",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"list\",\"add\",\"remove\",\"embed\"]}")]
            public string Action { get; set; }

            [ToolParameter("Identifier — add: any Client.Add string (com.x.y[@ver] / git URL / file:..). remove / embed: package name (com.x.y).")]
            public string Identifier { get; set; }
        }

        public sealed class PackageResult
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string Source { get; set; }
            public string ResolvedPath { get; set; }
            public bool IsDirectDependency { get; set; }
            public string DisplayName { get; set; }
        }

        public sealed class ListResult
        {
            public PackageResult[] Packages { get; set; }
        }

        public sealed class JobResult
        {
            public string JobId { get; set; }
            public int Port { get; set; }
            public string Action { get; set; }
            public string Identifier { get; set; }
        }

        // ---- Synchronous list ----

        // Client.List returns a ListRequest that resolves on EditorApplication.update
        // ticks. We poll by awaiting the next editor update so the continuation
        // stays on the main thread — request.IsCompleted / request.Status and the
        // PackageCollection in request.Result must be read there. Task.Delay would
        // resume on a thread-pool thread (no SynchronizationContext), touching UPM
        // state off the main thread.
        [HeraAction(
            Name = "list",
            ParametersType = typeof(EmptyParameters),
            ResultType = typeof(ListResult),
            RiskClass = HeraRiskClass.ReadOnly)]
        public static async Task<object> ListAsync(JObject raw)
        {
            var request = Client.List(offlineMode: false, includeIndirectDependencies: true);

            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!request.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                    return new ErrorResponse("PACKAGE_LIST_TIMEOUT",
                        "Client.List did not complete within 60s.");
                await NextEditorUpdate();
            }

            if (request.Status >= StatusCode.Failure)
            {
                return new ErrorResponse(
                    "PACKAGE_LIST_FAILED",
                    request.Error?.message ?? "Client.List failed (no error message).");
            }

            var pkgs = new List<object>();
            foreach (var info in request.Result)
                pkgs.Add(PackageJobState.BuildPackageShallow(info));

            return new SuccessResponse($"{pkgs.Count} packages.", new { packages = pkgs });
        }

        // Completes on the next EditorApplication.update tick, keeping the awaiting
        // continuation on the main thread. (Same pattern as InputQaEventSystem;
        // kept local until a third consumer justifies a Core/ helper.)
        private static Task NextEditorUpdate()
        {
            var source = new TaskCompletionSource<bool>();
            void Tick()
            {
                EditorApplication.update -= Tick;
                source.TrySetResult(true);
            }
            EditorApplication.update += Tick;
            return source.Task;
        }

        // ---- Async add / remove / embed ----

        [HeraAction(
            ParametersType = typeof(IdentifierParameters),
            ResultType = typeof(JobResult),
            RiskClass = HeraRiskClass.PackageChange,
            MayReloadDomain = true)]
        public static object Add(JObject raw)
        {
            var p = new ToolParams(raw);
            var argsToken = p.GetRaw("args") as JArray;
            string identifier = p.Get("identifier")
                ?? (argsToken != null && argsToken.Count >= 2 ? argsToken[1].ToString() : null);
            return StartAsyncJob("add", identifier);
        }

        [HeraAction(
            ParametersType = typeof(IdentifierParameters),
            ResultType = typeof(JobResult),
            RiskClass = HeraRiskClass.PackageChange,
            MayReloadDomain = true)]
        public static object Remove(JObject raw)
        {
            var p = new ToolParams(raw);
            var argsToken = p.GetRaw("args") as JArray;
            string identifier = p.Get("identifier")
                ?? (argsToken != null && argsToken.Count >= 2 ? argsToken[1].ToString() : null);
            return StartAsyncJob("remove", identifier);
        }

        [HeraAction(
            ParametersType = typeof(IdentifierParameters),
            ResultType = typeof(JobResult),
            RiskClass = HeraRiskClass.PackageChange,
            MayReloadDomain = true)]
        public static object Embed(JObject raw)
        {
            var p = new ToolParams(raw);
            var argsToken = p.GetRaw("args") as JArray;
            string identifier = p.Get("identifier")
                ?? (argsToken != null && argsToken.Count >= 2 ? argsToken[1].ToString() : null);
            return StartAsyncJob("embed", identifier);
        }

        private static object StartAsyncJob(string action, string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return new ErrorResponse("MISSING_PARAM", $"'identifier' required for {action}.");

            var jobId = $"pkg-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            var port = HttpServer.Port;

            PackageJobState.MarkPending(port, jobId, action, identifier);

            Request request;
            try
            {
                switch (action)
                {
                    case "add": request = Client.Add(identifier); break;
                    case "remove": request = Client.Remove(identifier); break;
                    case "embed": request = Client.Embed(identifier); break;
                    default:
                        PackageJobState.ClearPending(port, jobId);
                        return new ErrorResponse("UNKNOWN_ACTION", $"Unsupported async action: {action}.");
                }
            }
            catch (Exception ex)
            {
                PackageJobState.ClearPending(port, jobId);
                return new ErrorResponse("PACKAGE_JOB_START_FAILED", $"Failed to start {action} '{identifier}': {ex.Message}");
            }

            PackageJobState.AttachWatcher(port, jobId, action, identifier, request);

            return new SuccessResponse("running", new
            {
                job_id = jobId,
                port,
                action,
                identifier,
            });
        }
    }
}
