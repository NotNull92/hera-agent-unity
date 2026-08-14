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
        Description = "Unity Package Manager control. Actions: list and search (synchronous), add / remove / embed (asynchronous — return a job_id, poll the package-result file for completion). search matches the registry by substring and reports the versions this Editor accepts, so an identifier can be confirmed before add spends a domain reload. add accepts any Client.Add identifier: 'com.unity.x' registry name, 'com.unity.x@1.2.3' pinned version, 'https://github.com/.../repo.git[?path=...]' git URL, or 'file:..' local path. Avoids manifest.json hand-edits.",
        Examples = new[]
        {
            "manage_packages list",
            "manage_packages search --filter navigation",
            "manage_packages add com.unity.ai.navigation",
            "manage_packages add https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
            "manage_packages remove com.unity.ai.navigation",
            "manage_packages embed com.unity.test-framework",
        },
        ExampleDescriptions = new[]
        {
            "List every package the project currently resolves to (returns directly)",
            "Find registry packages matching a substring, with the versions compatible with this Editor",
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
        public sealed class IdentifierParameters
        {
            [ToolParameter("Package identifier.", Required = true)]
            public string Identifier { get; set; }
        }

        public sealed class SearchParameters
        {
            [ToolParameter(
                "Case-insensitive substring matched against package name, display name, description, and keywords.",
                Required = true)]
            public string Filter { get; set; }

            [ToolParameter(
                "Maximum results (default 25, max 200).",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}")]
            public int? Limit { get; set; }
        }

        public class Parameters
        {
            [ToolParameter(
                "Action: list, search, add, remove, embed",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"list\",\"search\",\"add\",\"remove\",\"embed\"]}")]
            public string Action { get; set; }

            [ToolParameter("Identifier — add: any Client.Add string (com.x.y[@ver] / git URL / file:..). remove / embed: package name (com.x.y).")]
            public string Identifier { get; set; }

            [ToolParameter("For search: case-insensitive substring to match against registry packages.")]
            public string Filter { get; set; }

            [ToolParameter("Maximum search results (default 25, max 200).")]
            public int Limit { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class PackageResult
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string Source { get; set; }
            public string ResolvedPath { get; set; }
            public bool IsDirectDependency { get; set; }
            public string DisplayName { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class ListResult
        {
            public PackageResult[] Packages { get; set; }
        }

        public sealed class SearchHit
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string[] CompatibleVersions { get; set; }
            public string Recommended { get; set; }
            public bool Deprecated { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SearchResult
        {
            public string Filter { get; set; }
            public int Total { get; set; }
            public int Returned { get; set; }
            public bool Truncated { get; set; }
            public SearchHit[] Packages { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
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
            ParametersType = typeof(object),
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
                await EditorUpdate.Next();
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

        // ---- Synchronous search ----

        // Client.Search is an exact-id lookup — Search("navigation") fails
        // NotFound — so it cannot answer "what is the navigation package
        // called?", which is the question that precedes an add. SearchAll
        // returns the whole visible registry (174 packages in ~4s on
        // 6000.3.5f2) with versions and keywords populated, so one call plus a
        // local substring match covers both discovery and exact lookup. Pumped
        // on editor updates for the same main-thread reason as ListAsync.
        [HeraAction(
            Name = "search",
            ParametersType = typeof(SearchParameters),
            ResultType = typeof(SearchResult),
            RiskClass = HeraRiskClass.ReadOnly)]
        public static async Task<object> SearchAsync(JObject raw)
        {
            var p = new ToolParams(raw ?? new JObject());
            var filter = p.Get("filter", "").Trim();
            if (string.IsNullOrEmpty(filter))
                return new ErrorResponse("MISSING_PARAM", "'filter' required for search.");
            var limit = Math.Min(Math.Max(p.GetInt("limit", 25).Value, 1), 200);

            var request = Client.SearchAll(offlineMode: false);

            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!request.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                    return new ErrorResponse("PACKAGE_SEARCH_TIMEOUT",
                        "Client.SearchAll did not complete within 60s.");
                await EditorUpdate.Next();
            }

            if (request.Status >= StatusCode.Failure)
            {
                return new ErrorResponse(
                    "PACKAGE_SEARCH_FAILED",
                    request.Error?.message ?? "Client.SearchAll failed (no error message).");
            }

            var hits = new List<object>();
            int total = 0;
            foreach (var info in request.Result)
            {
                if (!Matches(info.name, filter)
                    && !Matches(info.displayName, filter)
                    && !Matches(info.description, filter)
                    && !MatchesAny(info.keywords, filter))
                    continue;

                total++;
                if (hits.Count >= limit) continue;

                hits.Add(new
                {
                    name = info.name,
                    version = info.version,
                    display_name = info.displayName,
                    description = Summarize(info.description),
                    compatible_versions = NewestFirst(info.versions.compatible),
                    recommended = info.versions.recommended,
                    deprecated = info.isDeprecated,
                });
            }

            bool truncated = total > hits.Count;
            var response = new SuccessResponse(
                $"{total} package(s) match '{filter}'.",
                new { filter, total, returned = hits.Count, truncated, packages = hits });
            if (truncated)
                response.agent_hint = $"Showing {hits.Count} of {total}. Narrow --filter or raise --limit.";
            return response;
        }

        static bool Matches(string value, string filter) =>
            !string.IsNullOrEmpty(value)
            && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

        static bool MatchesAny(string[] values, string filter)
        {
            if (values == null) return false;
            foreach (var value in values)
                if (Matches(value, filter)) return true;
            return false;
        }

        // First sentence only. Registry descriptions run to several hundred
        // characters and the agent needs enough to tell two packages apart,
        // not the marketing copy.
        static string Summarize(string description)
        {
            if (string.IsNullOrEmpty(description)) return null;
            var text = description.Replace("\r", " ").Replace("\n", " ").Trim();
            var stop = text.IndexOf(". ", StringComparison.Ordinal);
            if (stop > 0) text = text.Substring(0, stop + 1);
            return text.Length > 200 ? text.Substring(0, 197) + "..." : text;
        }

        // Unity reports compatible versions oldest-first; the newest ones are
        // what an add would target, so keep those and cap the field so a
        // long-lived package cannot dominate the payload.
        static string[] NewestFirst(string[] versions)
        {
            if (versions == null || versions.Length == 0) return Array.Empty<string>();
            var take = Math.Min(versions.Length, 10);
            var result = new string[take];
            for (int i = 0; i < take; i++)
                result[i] = versions[versions.Length - 1 - i];
            return result;
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

            var jobId = CreateJobId();
            var port = HttpServer.Port;

            if (!PackageJobState.TryMarkPending(port, jobId, action, identifier, out var persistenceError))
            {
                return new ErrorResponse("PACKAGE_JOB_STATE_WRITE_FAILED",
                    $"Cannot start {action} '{identifier}' because its recovery state could not be persisted: {persistenceError}");
            }

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

        internal static string CreateJobId() => $"pkg-{Guid.NewGuid().ToString("N")}";
    }
}
