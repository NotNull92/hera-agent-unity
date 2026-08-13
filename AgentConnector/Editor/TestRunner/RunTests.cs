using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeraAgent.TestRunner
{
    [HeraTool(
        Description = "Run Unity EditMode or PlayMode tests and return results. 'list' enumerates the tests that exist without running them — use it to confirm a selector before spending a run, and copy an exact full name back into --filter. 'cancel' ends an active run and releases the pending-run lock.",
        Examples = new[]
        {
            "test --mode EditMode",
            "test list --mode EditMode",
            "test list --mode EditMode --category Smoke",
            "test --mode EditMode --category Smoke",
            "test cancel",
        },
        ExampleDescriptions = new[]
        {
            "Run every EditMode test",
            "Show per-assembly and per-category test counts without running anything",
            "List the tests carrying the Smoke category",
            "Run only the Smoke-category tests",
            "Cancel the active run and clear its pending-run lock",
        },
        Profiles = new[] { "diagnostics", "testing" },
        RiskClass = HeraRiskClass.Write,
        ContractMode = ToolContractMode.Strict)]
    public static class RunTests
    {
        internal static readonly string StatusDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hera-agent-unity", "status");

        public class Parameters
        {
            [ToolParameter(
                "Test mode: EditMode or PlayMode",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"EditMode\",\"PlayMode\"]}")]
            public string Mode { get; set; }

            [ToolParameter("Filter by namespace, class, or full test name")]
            public string Filter { get; set; }

            [ToolParameter("Comma-separated NUnit category names to select")]
            public string Category { get; set; }

            [ToolParameter("Comma-separated test assembly names to select (no .dll extension)")]
            public string Assembly { get; set; }

            [ToolParameter("Request run-scoped asynchronous results (new CLI capability)")]
            public bool AsyncResults { get; set; }
        }

        public sealed class ListParameters
        {
            [ToolParameter(
                "Test mode: EditMode or PlayMode (default EditMode)",
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"EditMode\",\"PlayMode\"]}")]
            public string Mode { get; set; }

            [ToolParameter("Case-insensitive substring matched against each test's full name")]
            public string Filter { get; set; }

            [ToolParameter("Comma-separated NUnit category names to select")]
            public string Category { get; set; }

            [ToolParameter("Comma-separated test assembly names to select (no .dll extension)")]
            public string Assembly { get; set; }

            [ToolParameter(
                "Maximum tests to return when a selector is given (default 200, max 2000).",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":2000}")]
            public int? Limit { get; set; }
        }

        public sealed class CancelParameters
        {
        }

        public sealed class Result
        {
            public int Port { get; set; }
            public string RunId { get; set; }
            public int Total { get; set; }
            public int Passed { get; set; }
            public int Failed { get; set; }
            public int Skipped { get; set; }
            public string[] Failures { get; set; }
            public string[] Passes { get; set; }
        }

        public sealed class TestEntry
        {
            public string FullName { get; set; }
            public string Assembly { get; set; }
            public string[] Categories { get; set; }
        }

        public sealed class CountEntry
        {
            public string Name { get; set; }
            public int Tests { get; set; }
        }

        public sealed class ListResult
        {
            public string Mode { get; set; }
            public int Total { get; set; }
            public int Returned { get; set; }
            public bool Truncated { get; set; }
            public CountEntry[] Assemblies { get; set; }
            public CountEntry[] Categories { get; set; }
            public TestEntry[] Tests { get; set; }
        }

        public sealed class CancelResult
        {
            public bool WasRunning { get; set; }
            public string[] RunIds { get; set; }
            public bool NunitCancelRequested { get; set; }
        }

        public static Task<object> HandleCommand(JObject @params)
        {
            if (@params == null)
                return Task.FromResult<object>(new ErrorResponse("MISSING_PARAM", "Parameters cannot be null."));

            var p = new ToolParams(@params);

            var modeResult = p.GetRequired("mode");
            if (!modeResult.IsSuccess)
                return Task.FromResult<object>(new ErrorResponse("MISSING_PARAM", modeResult.ErrorMessage));

            var modeStr = modeResult.Value.Trim();
            TestMode testMode;
            if (modeStr.Equals("EditMode", StringComparison.OrdinalIgnoreCase))
                testMode = TestMode.EditMode;
            else if (modeStr.Equals("PlayMode", StringComparison.OrdinalIgnoreCase))
                testMode = TestMode.PlayMode;
            else
                return Task.FromResult<object>(new ErrorResponse("INVALID_PARAM", $"Unknown mode '{modeStr}'. Use EditMode or PlayMode."));

            var filter = p.Get("filter", null);
            var categories = SplitList(p.Get("category", null));
            var assemblies = SplitList(p.Get("assembly", null));
            var asyncResults = p.GetBool("async_results");

            if (testMode == TestMode.EditMode && !asyncResults)
                return ExecuteLegacyEditMode(filter, categories, assemblies);

            return Task.FromResult<object>(StartTestRun(testMode, filter, categories, assemblies));
        }

        // Any of --filter / --category / --assembly narrows the run. A narrowed
        // run that matches nothing is a selector mistake, not a green build, so
        // the distinction has to reach BuildResponse and survive to the file bus.
        internal static bool IsSelective(string filter, string[] categories, string[] assemblies) =>
            !string.IsNullOrEmpty(filter)
            || (categories != null && categories.Length > 0)
            || (assemblies != null && assemblies.Length > 0);

        internal static string[] SplitList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var parts = raw.Split(',');
            var kept = new List<string>();
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0) kept.Add(trimmed);
            }
            return kept.Count == 0 ? null : kept.ToArray();
        }

        private static Task<object> ExecuteLegacyEditMode(string filter, string[] categories, string[] assemblies)
        {
            var port = HttpServer.Port;
            if (TestRunnerState.HasPending(port))
            {
                return Task.FromResult<object>(new ErrorResponse("TEST_RUN_ALREADY_RUNNING",
                    $"A test run is already active for port {port}."));
            }

            var passed = new List<string>();
            var failed = new List<string>();
            var skipped = new List<string>();
            var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestRunnerApi api = null;
            TestCallbacks callbacks = null;
            var completed = false;
            var cleanedUp = false;
            Action cleanup = () =>
            {
                if (cleanedUp) return;
                cleanedUp = true;
                DisposeApi(api, callbacks);
            };

            try
            {
                api = ScriptableObject.CreateInstance<TestRunnerApi>();
                callbacks = new TestCallbacks(
                    onResult: r => CollectResult(r, passed, failed, skipped),
                    onFinished: _ =>
                    {
                        if (completed) return;
                        completed = true;
                        var response = BuildResponse(passed, failed, skipped,
                            IsSelective(filter, categories, assemblies));
                        cleanup();
                        completion.TrySetResult(response);
                    });

                api.RegisterCallbacks(callbacks);
                api.Execute(new ExecutionSettings(BuildFilter(TestMode.EditMode, filter, categories, assemblies)));
                return completion.Task;
            }
            catch (Exception ex)
            {
                cleanup();
                return Task.FromResult<object>(new ErrorResponse("TEST_RUN_START_FAILED",
                    $"Unable to start EditMode tests: {ex.Message}"));
            }
        }

        private static object StartTestRun(TestMode mode, string filter, string[] categories, string[] assemblies)
        {
            var port = HttpServer.Port;

            if (TestRunnerState.HasPending(port))
            {
                return new ErrorResponse("TEST_RUN_ALREADY_RUNNING",
                    $"A test run is already active for port {port}.");
            }

            var runId = Guid.NewGuid().ToString("N");

            try
            {
                var resultPath = ResultsFilePath(port, runId);
                if (File.Exists(resultPath)) File.Delete(resultPath);
                var legacyPath = LegacyResultsFilePath(port);
                if (File.Exists(legacyPath)) File.Delete(legacyPath);
            }
            catch { }
            var selective = IsSelective(filter, categories, assemblies);
            TestRunnerState.MarkPending(port, runId, filter, mode, selective);

            var passed  = new List<string>();
            var failed  = new List<string>();
            var skipped = new List<string>();

            TestRunnerApi api = null;
            TestCallbacks callbacks = null;
            var completed = false;
            var cleanedUp = false;
            Action cleanup = () =>
            {
                if (cleanedUp) return;
                cleanedUp = true;
                DisposeApi(api, callbacks);
            };

            try
            {
                api = ScriptableObject.CreateInstance<TestRunnerApi>();
                callbacks = new TestCallbacks(
                onResult: r => CollectResult(r, passed, failed, skipped),
                onFinished: _ =>
                {
                    if (completed) return;
                    completed = true;
                    if (WriteResultsFile(port, runId, passed, failed, skipped, selective))
                        TestRunnerState.ClearPending(port, runId);
                    cleanup();
                }
                );

                api.RegisterCallbacks(callbacks);
                // Execute returns NUnit's own run guid — the only handle
                // CancelTestRun accepts. Persist it so `test cancel` still works
                // after the PlayMode domain reload.
                var nunitGuid = api.Execute(new ExecutionSettings(BuildFilter(mode, filter, categories, assemblies)));
                TestRunnerState.AttachRunGuid(port, runId, nunitGuid);
                return new SuccessResponse("running", new { port, run_id = runId });
            }
            catch (Exception ex)
            {
                cleanup();
                TestRunnerState.ClearPending(port, runId);
                return new ErrorResponse("TEST_RUN_START_FAILED", $"Unable to start {mode} tests: {ex.Message}");
            }
        }

        // RetrieveTestList is asynchronous — measured on 6000.3.5f2, the
        // callback had not fired by the time the calling frame returned — so the
        // handler pumps editor updates until it lands, the way manage_packages
        // list awaits its ListRequest.
        [HeraAction(
            Name = "list",
            ParametersType = typeof(ListParameters),
            ResultType = typeof(ListResult),
            RiskClass = HeraRiskClass.ReadOnly)]
        public static async Task<object> ListAsync(JObject raw)
        {
            var p = new ToolParams(raw ?? new JObject());
            var modeStr = p.Get("mode", "EditMode").Trim();
            TestMode mode;
            if (modeStr.Equals("EditMode", StringComparison.OrdinalIgnoreCase))
                mode = TestMode.EditMode;
            else if (modeStr.Equals("PlayMode", StringComparison.OrdinalIgnoreCase))
                mode = TestMode.PlayMode;
            else
                return new ErrorResponse("INVALID_PARAM", $"Unknown mode '{modeStr}'. Use EditMode or PlayMode.");

            var filter = p.Get("filter", null);
            var categories = SplitList(p.Get("category", null));
            var assemblies = SplitList(p.Get("assembly", null));
            var limit = Math.Min(Math.Max(p.GetInt("limit", 200).Value, 1), 2000);

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var collected = new List<TestEntry>();
            var done = false;
            try
            {
                api.RetrieveTestList(mode, root =>
                {
                    if (root != null) Collect(root, null, collected);
                    done = true;
                });

                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (!done)
                {
                    if (DateTime.UtcNow > deadline)
                        return new ErrorResponse("TEST_LIST_TIMEOUT",
                            "Unity did not return the test list within 60s.");
                    await EditorUpdate.Next();
                }
            }
            finally
            {
                Object.DestroyImmediate(api);
            }

            var selective = IsSelective(filter, categories, assemblies);
            if (!selective)
                return SummarizeList(modeStr, collected);

            var matched = new List<object>();
            var total = 0;
            foreach (var entry in collected)
            {
                if (!MatchesSelectors(entry, filter, categories, assemblies)) continue;
                total++;
                if (matched.Count >= limit) continue;
                matched.Add(new
                {
                    full_name = entry.FullName,
                    assembly = entry.Assembly,
                    categories = entry.Categories,
                });
            }

            var truncated = total > matched.Count;
            var response = new SuccessResponse(
                $"{total} test(s) match in {modeStr}.",
                new
                {
                    mode = modeStr,
                    total,
                    returned = matched.Count,
                    truncated,
                    tests = matched,
                });
            if (truncated)
                response.agent_hint = $"Showing {matched.Count} of {total}. Narrow the selector or raise --limit.";
            else if (total == 0)
                response.agent_hint = "Nothing matched. Run `test list` without a selector to see which assemblies and categories exist.";
            return response;
        }

        // Without a selector a real project would flood the agent's context, so
        // the unfiltered answer is counts per assembly and per category — enough
        // to pick a selector, which the filtered call then expands.
        static object SummarizeList(string modeStr, List<TestEntry> collected)
        {
            var assemblyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var categoryCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in collected)
            {
                var assembly = entry.Assembly ?? "(unknown)";
                assemblyCounts.TryGetValue(assembly, out var ac);
                assemblyCounts[assembly] = ac + 1;
                foreach (var category in entry.Categories)
                {
                    categoryCounts.TryGetValue(category, out var cc);
                    categoryCounts[category] = cc + 1;
                }
            }

            var response = new SuccessResponse(
                $"{collected.Count} {modeStr} test(s) in {assemblyCounts.Count} assembly(ies).",
                new
                {
                    mode = modeStr,
                    total = collected.Count,
                    assemblies = ToCounts(assemblyCounts),
                    categories = ToCounts(categoryCounts),
                });
            response.agent_hint = "Add --assembly, --category, or --filter to list the individual test names.";
            return response;
        }

        static List<object> ToCounts(Dictionary<string, int> counts)
        {
            var keys = new List<string>(counts.Keys);
            keys.Sort(StringComparer.Ordinal);
            var list = new List<object>(keys.Count);
            foreach (var key in keys)
                list.Add(new { name = key, tests = counts[key] });
            return list;
        }

        static bool MatchesSelectors(TestEntry entry, string filter, string[] categories, string[] assemblies)
        {
            if (!string.IsNullOrEmpty(filter)
                && entry.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            if (assemblies != null && assemblies.Length > 0 && !ContainsIgnoreCase(assemblies, entry.Assembly))
                return false;
            if (categories != null && categories.Length > 0)
            {
                var hit = false;
                foreach (var category in entry.Categories)
                {
                    if (!ContainsIgnoreCase(categories, category)) continue;
                    hit = true;
                    break;
                }
                if (!hit) return false;
            }
            return true;
        }

        static bool ContainsIgnoreCase(string[] values, string candidate)
        {
            if (candidate == null) return false;
            foreach (var value in values)
                if (string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // The tree is root suite → assembly suite → namespace suites → class
        // suite → cases. Only the assembly suite carries a usable identity, and
        // it reports the built .dll path, so it is reduced to the bare assembly
        // name that Filter.assemblyNames accepts.
        static void Collect(ITestAdaptor node, string assembly, List<TestEntry> into)
        {
            if (node.IsTestAssembly)
                assembly = Path.GetFileNameWithoutExtension(node.FullName);

            if (!node.IsSuite)
            {
                into.Add(new TestEntry
                {
                    FullName = node.FullName,
                    Assembly = assembly,
                    Categories = RealCategories(node.Categories),
                });
                return;
            }

            if (node.Children == null) return;
            foreach (var child in node.Children)
                Collect(child, assembly, into);
        }

        // Unity reports "Uncategorized" for a test with no [Category]. That is
        // the framework's placeholder, not a name --category can select, so it
        // is dropped rather than echoed back as if it were selectable.
        static string[] RealCategories(string[] categories)
        {
            if (categories == null || categories.Length == 0) return Array.Empty<string>();
            var kept = new List<string>(categories.Length);
            foreach (var category in categories)
            {
                if (string.IsNullOrEmpty(category)) continue;
                if (string.Equals(category, "Uncategorized", StringComparison.Ordinal)) continue;
                kept.Add(category);
            }
            return kept.ToArray();
        }

        // Two independent layers. Asking NUnit to cancel can fail — the guid may
        // predate a domain reload, or the run may already be tearing down — but
        // clearing the pending record must not, because that record is the only
        // thing standing between the agent and a permanent
        // TEST_RUN_ALREADY_RUNNING lockout.
        [HeraAction(
            ParametersType = typeof(CancelParameters),
            ResultType = typeof(CancelResult),
            RiskClass = HeraRiskClass.Write,
            Idempotent = true)]
        public static object Cancel(JObject raw)
        {
            var port = HttpServer.Port;
            var pending = TestRunnerState.ListPending(port);
            if (pending.Count == 0)
                return new SuccessResponse("No test run was active.", new { was_running = false });

            var runIds = new List<string>();
            var cancelRequested = false;
            foreach (var entry in pending)
            {
                var runId = entry.Key;
                var nunitGuid = entry.Value;
                if (!string.IsNullOrEmpty(nunitGuid))
                {
                    try
                    {
                        cancelRequested |= TestRunnerApi.CancelTestRun(nunitGuid);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Hera] I could not ask the test framework to cancel run {nunitGuid}: {ex.Message}");
                    }
                }

                if (WriteErrorResultsFile(port, runId, "TEST_RUN_CANCELLED", "The test run was cancelled."))
                    TestRunnerState.ClearPending(port, runId);
                runIds.Add(runId);
            }

            return new SuccessResponse(
                cancelRequested
                    ? $"Cancelled {runIds.Count} test run(s)."
                    : $"Released {runIds.Count} test run record(s); the test framework did not accept a cancel request.",
                new
                {
                    was_running = true,
                    run_ids = runIds,
                    nunit_cancel_requested = cancelRequested,
                });
        }

        internal static void CollectResult(ITestResultAdaptor result,
            List<string> passed, List<string> failed, List<string> skipped)
        {
            if (result.Test.IsSuite) return;
            var name = result.Test.FullName;
            switch (result.TestStatus)
            {
                case TestStatus.Passed:  passed.Add(name); break;
                case TestStatus.Failed:  failed.Add($"{name}: {result.Message}"); break;
                default:                 skipped.Add(name); break;
            }
        }

        internal static bool WriteResultsFile(int port, string runId, List<string> passed, List<string> failed, List<string> skipped, bool selective)
        {
            return WriteResponseFile(port, runId, BuildResponse(passed, failed, skipped, selective));
        }

        internal static bool WriteErrorResultsFile(int port, string runId, string code, string message)
        {
            return WriteResponseFile(port, runId, new ErrorResponse(code, message));
        }

        private static bool WriteResponseFile(int port, string runId, object response)
        {
            try
            {
                var json = JsonConvert.SerializeObject(response);
                HeraAgent.AtomicFile.WriteAllText(ResultsFilePath(port, runId), json);
                try
                {
                    HeraAgent.AtomicFile.WriteAllText(LegacyResultsFilePath(port), json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Hera] Failed to write legacy test results: {ex.Message}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Hera] Failed to write test results: {ex.Message}");
                return false;
            }
        }

        internal static string ResultsFilePath(int port, string runId) =>
            Path.Combine(StatusDir, $"test-results-{port}-{runId}.json");

        internal static string LegacyResultsFilePath(int port) =>
            Path.Combine(StatusDir, $"test-results-{port}.json");

        // TestRunnerApi is a ScriptableObject and Unity destroys it before
        // RunFinished reaches us — measured on 6000.3.5f2, DisposeApi runs on
        // every EditMode run with the api already destroyed. Guarding the
        // unregister on `api != null` (Unity's overloaded comparison, true for a
        // destroyed object) therefore skipped it every time, leaking one
        // registration per run into the framework's callbacks holder where it
        // kept collecting later runs' results and rewriting the earlier run's
        // result file. The holder is a singleton, so any live TestRunnerApi can
        // remove the registration; a throwaway instance is enough.
        internal static void DisposeApi(TestRunnerApi api, TestCallbacks callbacks)
        {
            if (callbacks != null)
            {
                var unregisterVia = api != null ? api : ScriptableObject.CreateInstance<TestRunnerApi>();
                try
                {
                    unregisterVia.UnregisterCallbacks(callbacks);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Hera] I couldn't unregister the test callbacks: {ex.Message}");
                }
                finally
                {
                    if (!ReferenceEquals(unregisterVia, api))
                        TryDestroy(unregisterVia);
                }
            }

            TryDestroy(api);
        }

        static void TryDestroy(TestRunnerApi api)
        {
            try
            {
                if (api != null)
                    Object.DestroyImmediate(api);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Hera] I couldn't destroy the TestRunnerApi instance: {ex.Message}");
            }
        }

        internal static object BuildResponse(List<string> passed, List<string> failed, List<string> skipped, bool selective)
        {
            var total = passed.Count + failed.Count + skipped.Count;
            var summary = new
            {
                total,
                passed  = passed.Count,
                failed  = failed.Count,
                skipped = skipped.Count,
                failures = failed,
                passes   = passed,
            };
            if (failed.Count > 0)
                return new ErrorResponse("TESTS_FAILED", $"{failed.Count} test(s) failed.", summary);
            // Zero tests under a selector means the selector matched nothing —
            // reporting that as a pass tells the agent its work is verified when
            // nothing ran. An unfiltered run of a project with no tests stays a
            // success, because that is a true statement about the project.
            if (total == 0 && selective)
            {
                return new ErrorResponse(
                    "NO_TESTS_MATCHED",
                    "No test matched the selector, so nothing ran. This is not a pass.",
                    summary,
                    new List<string>
                    {
                        "Enumerate what exists: hera-agent-unity test list --mode <EditMode|PlayMode>",
                        "Copy an exact full name from that list into --filter, or select with --category / --assembly.",
                    });
            }
            return new SuccessResponse($"All {passed.Count} test(s) passed.", summary);
        }

        internal static Filter BuildFilter(TestMode mode, string filterStr, string[] categories, string[] assemblies)
        {
            var f = new Filter { testMode = mode };
            if (!string.IsNullOrEmpty(filterStr))
            {
                f.testNames  = new[] { filterStr };
                f.groupNames = new[] { filterStr };
            }
            if (categories != null && categories.Length > 0)
                f.categoryNames = categories;
            if (assemblies != null && assemblies.Length > 0)
                f.assemblyNames = assemblies;
            return f;
        }

        internal class TestCallbacks : ICallbacks
        {
            private readonly Action<ITestResultAdaptor> _onResult;
            private readonly Action<ITestResultAdaptor> _onFinished;

            public TestCallbacks(Action<ITestResultAdaptor> onResult, Action<ITestResultAdaptor> onFinished)
            {
                _onResult   = onResult;
                _onFinished = onFinished;
            }

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor result) => _onFinished(result);
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) => _onResult(result);
        }
    }
}
