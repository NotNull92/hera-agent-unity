using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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
        })]
    public static class ManagePackages
    {
        public class Parameters
        {
            [ToolParameter("Action: list, add, remove, embed", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Identifier — add: any Client.Add string (com.x.y[@ver] / git URL / file:..). remove / embed: package name (com.x.y).")]
            public string Identifier { get; set; }
        }

        public static async Task<object> HandleCommand(JObject parameters)
        {
            if (parameters == null)
                return new ErrorResponse("Parameters cannot be null.");

            var p = new ToolParams(parameters);
            var argsToken = p.GetRaw("args") as JArray;

            string action = p.Get("action")
                ?? (argsToken != null && argsToken.Count >= 1 ? argsToken[0].ToString() : null);
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' required: list, add, remove, embed");
            action = action.ToLowerInvariant();

            string identifier = p.Get("identifier")
                ?? (argsToken != null && argsToken.Count >= 2 ? argsToken[1].ToString() : null);

            switch (action)
            {
                case "list": return await ListAsync();
                case "add": return StartAsyncJob("add", identifier);
                case "remove": return StartAsyncJob("remove", identifier);
                case "embed": return StartAsyncJob("embed", identifier);
                default:
                    return new ErrorResponse(
                        $"Unknown manage_packages action: '{action}'. Use list, add, remove, embed.");
            }
        }

        // ---- Synchronous list ----

        // Client.List returns a ListRequest that resolves on EditorApplication.update
        // ticks. We yield with Task.Delay so Unity's main loop keeps pumping
        // (Thread.Sleep would freeze it and the request would never complete).
        private static async Task<object> ListAsync()
        {
            var request = Client.List(offlineMode: false, includeIndirectDependencies: true);

            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!request.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                    return new ErrorResponse("PACKAGE_LIST_TIMEOUT",
                        "Client.List did not complete within 60s.");
                await Task.Delay(50);
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

        // ---- Async add / remove / embed ----

        private static object StartAsyncJob(string action, string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return new ErrorResponse($"'identifier' required for {action}.");

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
                        return new ErrorResponse($"Unsupported async action: {action}.");
                }
            }
            catch (Exception ex)
            {
                PackageJobState.ClearPending(port, jobId);
                return new ErrorResponse($"Failed to start {action} '{identifier}': {ex.Message}");
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
