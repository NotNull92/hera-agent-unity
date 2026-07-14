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
    [HeraTool(Description = "Run Unity EditMode or PlayMode tests and return results.")]
    public static class RunTests
    {
        internal static readonly string StatusDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hera-agent-unity", "status");

        public class Parameters
        {
            [ToolParameter("Test mode: EditMode or PlayMode", Required = true)]
            public string Mode { get; set; }

            [ToolParameter("Filter by namespace, class, or full test name")]
            public string Filter { get; set; }

            [ToolParameter("Request run-scoped asynchronous results (new CLI capability)")]
            public bool AsyncResults { get; set; }
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
            var asyncResults = p.GetBool("async_results");

            if (testMode == TestMode.EditMode && !asyncResults)
                return ExecuteLegacyEditMode(filter);

            return Task.FromResult<object>(StartTestRun(testMode, filter));
        }

        private static Task<object> ExecuteLegacyEditMode(string filter)
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
                        var response = BuildResponse(passed, failed, skipped);
                        cleanup();
                        completion.TrySetResult(response);
                    });

                api.RegisterCallbacks(callbacks);
                api.Execute(new ExecutionSettings(BuildFilter(TestMode.EditMode, filter)));
                return completion.Task;
            }
            catch (Exception ex)
            {
                cleanup();
                return Task.FromResult<object>(new ErrorResponse("TEST_RUN_START_FAILED",
                    $"Unable to start EditMode tests: {ex.Message}"));
            }
        }

        private static object StartTestRun(TestMode mode, string filter)
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
            TestRunnerState.MarkPending(port, runId, filter, mode);

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
                    if (WriteResultsFile(port, runId, passed, failed, skipped))
                        TestRunnerState.ClearPending(port, runId);
                    cleanup();
                }
                );

                api.RegisterCallbacks(callbacks);
                api.Execute(new ExecutionSettings(BuildFilter(mode, filter)));
                return new SuccessResponse("running", new { port, run_id = runId });
            }
            catch (Exception ex)
            {
                cleanup();
                TestRunnerState.ClearPending(port, runId);
                return new ErrorResponse("TEST_RUN_START_FAILED", $"Unable to start {mode} tests: {ex.Message}");
            }
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

        internal static bool WriteResultsFile(int port, string runId, List<string> passed, List<string> failed, List<string> skipped)
        {
            return WriteResponseFile(port, runId, BuildResponse(passed, failed, skipped));
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

        internal static void DisposeApi(TestRunnerApi api, TestCallbacks callbacks)
        {
            try
            {
                if (api != null && callbacks != null)
                    api.UnregisterCallbacks(callbacks);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Hera] Failed to unregister test callbacks: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (api != null)
                        Object.DestroyImmediate(api);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Hera] Failed to destroy TestRunnerApi: {ex.Message}");
                }
            }
        }

        internal static object BuildResponse(List<string> passed, List<string> failed, List<string> skipped)
        {
            var summary = new
            {
                total   = passed.Count + failed.Count + skipped.Count,
                passed  = passed.Count,
                failed  = failed.Count,
                skipped = skipped.Count,
                failures = failed,
                passes   = passed,
            };
            return failed.Count > 0
                ? (object)new ErrorResponse("TESTS_FAILED", $"{failed.Count} test(s) failed.", summary)
                : new SuccessResponse($"All {passed.Count} test(s) passed.", summary);
        }

        internal static Filter BuildFilter(TestMode mode, string filterStr)
        {
            var f = new Filter { testMode = mode };
            if (!string.IsNullOrEmpty(filterStr))
            {
                f.testNames  = new[] { filterStr };
                f.groupNames = new[] { filterStr };
            }
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
