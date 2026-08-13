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

        public static void MarkPending(int port, string runId, string filter, TestMode mode, bool selective)
        {
            WritePending(port, runId, filter, mode == TestMode.EditMode ? "EditMode" : "PlayMode", selective, null);
        }

        /// <summary>
        /// NUnit's run guid only exists once TestRunnerApi.Execute returns, but
        /// the pending record has to be on disk before that so a concurrent run
        /// is refused. This fills the guid in afterwards.
        /// </summary>
        public static void AttachRunGuid(int port, string runId, string nunitGuid)
        {
            if (string.IsNullOrEmpty(nunitGuid)) return;
            if (!TryReadPending(PendingFilePath(port, runId), out var pending)) return;
            WritePending(port, runId, pending.Filter, pending.Mode, pending.Selective, nunitGuid);
        }

        /// <summary>
        /// Every pending run this Editor owns on the port, as
        /// (run_id, nunit_guid) pairs. HasPending blocks on *any* record, so a
        /// cancel that cleared only one would leave the lock in place — an
        /// interrupted run can leave a record behind without an active run.
        /// </summary>
        internal static List<KeyValuePair<string, string>> ListPending(int port)
        {
            var found = new List<KeyValuePair<string, string>>();
            try
            {
                if (!Directory.Exists(RunTests.StatusDir)) return found;
                foreach (var file in Directory.GetFiles(RunTests.StatusDir, $"test-pending-{port}-*.json"))
                {
                    if (!TryReadPending(file, out var pending)) continue;
                    if (!OwnsCurrentProject(pending)) continue;
                    found.Add(new KeyValuePair<string, string>(pending.RunId, pending.NunitGuid));
                }
            }
            catch { }
            return found;
        }

        static void WritePending(int port, string runId, string filter, string mode, bool selective, string nunitGuid)
        {
            var pending = new
            {
                port,
                run_id = runId,
                filter = filter ?? "",
                mode,
                selective,
                nunit_guid = nunitGuid ?? "",
                owner_pid = HeraAgent.ProjectIdentity.CurrentProcessId,
                project_id = HeraAgent.ProjectIdentity.CurrentId
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
                        continue;
                    }

                    if (!OwnsCurrentProject(pending))
                        continue;

                    if (File.Exists(RunTests.ResultsFilePath(pending.Port, pending.RunId)))
                    {
                        ClearPending(pending.Port, pending.RunId);
                        continue;
                    }

                    if (pending.OwnerPid == CurrentProcessId)
                        return true;
                    if (!HeraAgent.ProjectIdentity.IsProcessConfirmedDead(pending.OwnerPid))
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
                        continue;
                    }

                    if (!OwnsCurrentProject(pending))
                        continue;

                    if (File.Exists(RunTests.ResultsFilePath(pending.Port, pending.RunId)))
                    {
                        ClearPending(pending.Port, pending.RunId);
                        continue;
                    }

                    if (pending.OwnerPid != CurrentProcessId)
                    {
                        if (HeraAgent.ProjectIdentity.IsProcessConfirmedDead(pending.OwnerPid))
                            CompleteInterruptedRun(file, pending, "Test run belongs to a previous Unity Editor process.");
                        continue;
                    }

                    if (string.Equals(pending.Mode, "EditMode", System.StringComparison.OrdinalIgnoreCase))
                    {
                        CompleteInterruptedRun(file, pending, "EditMode tests were interrupted by an assembly reload.");
                        continue;
                    }

                    ReattachCallbacks(pending.Port, pending.RunId, pending.Selective);
                }
            }
            catch { }
        }

        static void ReattachCallbacks(int port, string runId, bool selective)
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
                    if (RunTests.WriteResultsFile(port, runId, passed, failed, skipped, selective))
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

        static int CurrentProcessId => HeraAgent.ProjectIdentity.CurrentProcessId;

        static bool OwnsCurrentProject(PendingRun pending) =>
            HeraAgent.ProjectIdentity.OwnsState(pending.State, CurrentProcessId);

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
                    // Records written before this field existed carry a filter
                    // string only; treat a non-empty filter as selective so a
                    // run in flight across an upgrade still reports honestly.
                    Selective = data["selective"]?.Value<bool>()
                        ?? !string.IsNullOrEmpty(data["filter"]?.Value<string>()),
                    NunitGuid = data["nunit_guid"]?.Value<string>(),
                    OwnerPid = ownerPid,
                    State = data
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
            public bool Selective { get; set; }
            public string NunitGuid { get; set; }
            public int OwnerPid { get; set; }
            public JObject State { get; set; }
        }
    }
}
