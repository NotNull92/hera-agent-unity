using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace HeraAgent.TestRunner
{
    /// <summary>
    /// Survives domain reloads via [InitializeOnLoad].
    /// Re-registers TestRunnerApi callbacks after PlayMode domain reload
    /// so RunFinished still fires and results are written to file.
    /// </summary>
    [InitializeOnLoad]
    public static class TestRunnerState
    {
        static TestRunnerState()
        {
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        public static void MarkPending(int port, string runId, string filter, TestMode mode)
        {
            var pending = new
            {
                port,
                run_id = runId,
                filter = filter ?? "",
                mode = mode == TestMode.EditMode ? "EditMode" : "PlayMode",
                owner_pid = System.Diagnostics.Process.GetCurrentProcess().Id
            };
            try
            {
                HeraAgent.AtomicFile.WriteAllText(PendingFilePath(port, runId), JsonConvert.SerializeObject(pending));
            }
            catch { }
        }

        public static void ClearPending(int port, string runId)
        {
            try
            {
                var path = PendingFilePath(port, runId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        internal static bool HasPending(int port)
        {
            try
            {
                if (!Directory.Exists(RunTests.StatusDir)) return false;

                foreach (var file in Directory.GetFiles(RunTests.StatusDir, $"test-pending-{port}-*.json"))
                {
                    if (!TryReadPending(file, out var pending))
                    {
                        TryDelete(file);
                        continue;
                    }

                    if (File.Exists(RunTests.ResultsFilePath(pending.Port, pending.RunId)))
                    {
                        ClearPending(pending.Port, pending.RunId);
                        continue;
                    }

                    if (pending.OwnerPid == CurrentProcessId)
                        return true;

                    CompleteInterruptedRun(file, pending, "Test run belongs to a previous Unity Editor process.");
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        static void OnAfterAssemblyReload()
        {
            try
            {
                Directory.CreateDirectory(RunTests.StatusDir);
                foreach (var file in Directory.GetFiles(RunTests.StatusDir, "test-pending-*.json"))
                {
                    if (!TryReadPending(file, out var pending))
                    {
                        TryDelete(file);
                        continue;
                    }

                    if (File.Exists(RunTests.ResultsFilePath(pending.Port, pending.RunId)))
                    {
                        ClearPending(pending.Port, pending.RunId);
                        continue;
                    }

                    if (pending.OwnerPid != CurrentProcessId)
                    {
                        CompleteInterruptedRun(file, pending, "Test run belongs to a previous Unity Editor process.");
                        continue;
                    }

                    if (string.Equals(pending.Mode, "EditMode", System.StringComparison.OrdinalIgnoreCase))
                    {
                        CompleteInterruptedRun(file, pending, "EditMode tests were interrupted by an assembly reload.");
                        continue;
                    }

                    ReattachCallbacks(pending.Port, pending.RunId, pending.Filter);
                }
            }
            catch { }
        }

        static void ReattachCallbacks(int port, string runId, string filter)
        {
            var passed  = new List<string>();
            var failed  = new List<string>();
            var skipped = new List<string>();

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            RunTests.TestCallbacks callbacks = null;
            var completed = false;
            var cleanedUp = false;
            System.Action cleanup = () =>
            {
                if (cleanedUp) return;
                cleanedUp = true;
                RunTests.DisposeApi(api, callbacks);
            };
            callbacks = new RunTests.TestCallbacks(
                onResult: r => RunTests.CollectResult(r, passed, failed, skipped),
                onFinished: _ =>
                {
                    if (completed) return;
                    completed = true;
                    if (RunTests.WriteResultsFile(port, runId, passed, failed, skipped))
                        ClearPending(port, runId);
                    cleanup();
                }
            );

            try
            {
                api.RegisterCallbacks(callbacks);
            }
            catch (System.Exception ex)
            {
                cleanup();
                if (RunTests.WriteErrorResultsFile(port, runId, "TEST_RUN_RECOVERY_FAILED",
                    $"Unable to recover PlayMode test callbacks: {ex.Message}"))
                    ClearPending(port, runId);
            }
        }

        static int CurrentProcessId => System.Diagnostics.Process.GetCurrentProcess().Id;

        static bool TryReadPending(string path, out PendingRun pending)
        {
            pending = null;
            try
            {
                var json = File.ReadAllText(path);
                var data = JObject.Parse(json);
                var port = data["port"]?.Value<int>() ?? 0;
                var runId = data["run_id"]?.Value<string>();
                var ownerPid = data["owner_pid"]?.Value<int>() ?? 0;
                if (port == 0 || string.IsNullOrEmpty(runId) || ownerPid == 0) return false;

                pending = new PendingRun
                {
                    Port = port,
                    RunId = runId,
                    Filter = data["filter"]?.Value<string>(),
                    Mode = data["mode"]?.Value<string>(),
                    OwnerPid = ownerPid
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void CompleteInterruptedRun(string file, PendingRun pending, string message)
        {
            if (File.Exists(RunTests.ResultsFilePath(pending.Port, pending.RunId)))
            {
                ClearPending(pending.Port, pending.RunId);
                return;
            }

            if (RunTests.WriteErrorResultsFile(pending.Port, pending.RunId, "TEST_RUN_INTERRUPTED", message))
                ClearPending(pending.Port, pending.RunId);
            else
                TryDelete(file);
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        static string PendingFilePath(int port, string runId) =>
            Path.Combine(RunTests.StatusDir, $"test-pending-{port}-{runId}.json");

        sealed class PendingRun
        {
            public int Port { get; set; }
            public string RunId { get; set; }
            public string Filter { get; set; }
            public string Mode { get; set; }
            public int OwnerPid { get; set; }
        }
    }
}
