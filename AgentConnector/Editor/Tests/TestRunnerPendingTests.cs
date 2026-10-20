using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HeraAgent.Tests
{
    public class TestRunnerPendingTests
    {
        [Test]
        public void PendingLookupSurvivesPortChangeAndFiltersOwnership()
        {
            var state = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("HeraAgent.TestRunner.TestRunnerState"))
                .First(t => t != null);
            var runner = state.Assembly.GetType("HeraAgent.TestRunner.RunTests");
            var statusDir = (string)runner.GetField("StatusDir", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var currentPort = HttpServer.Port;
            var dispatchPort = currentPort == 8099 ? 8098 : 8099;
            var runId = "r1-" + Guid.NewGuid().ToString("N");
            var foreignId = runId + "-foreign";
            var ownPath = Path.Combine(statusDir, $"test-pending-{dispatchPort}-{runId}.json");
            var foreignPath = Path.Combine(statusDir, $"test-pending-{dispatchPort}-{foreignId}.json");
            Directory.CreateDirectory(statusDir);
            object Invoke(string name)
            {
                var method = state.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
                return method.Invoke(null, null);
            }
            bool ContainsRun(string id) => ((IEnumerable)Invoke("ListPending")).Cast<object>().Any(item =>
                (string)item.GetType().GetProperty("RunId").GetValue(item) == id);
            try
            {
                var alreadyPending = (bool)Invoke("HasPending");
                var pending = new JObject { ["port"] = dispatchPort, ["run_id"] = foreignId,
                    ["owner_pid"] = ProjectIdentity.CurrentProcessId, ["project_id"] = "another-project", ["mode"] = "PlayMode" };
                File.WriteAllText(foreignPath, pending.ToString());
                Assert.AreEqual(alreadyPending, (bool)Invoke("HasPending"), "Foreign record must not change the lock.");
                Assert.IsFalse(ContainsRun(foreignId), "Foreign project must be ignored even with the same PID.");
                pending["run_id"] = runId;
                pending["project_id"] = ProjectIdentity.CurrentId;
                File.WriteAllText(ownPath, pending.ToString());
                Assert.IsTrue((bool)Invoke("HasPending"), "Pending lock must survive a port change.");
                Assert.IsTrue(ContainsRun(runId), "Cancel lookup must find the original dispatch port.");
                var owned = ((IEnumerable)Invoke("ListPending")).Cast<object>().First(item =>
                    (string)item.GetType().GetProperty("RunId").GetValue(item) == runId);
                Assert.AreEqual(dispatchPort, owned.GetType().GetProperty("Port").GetValue(owned),
                    "Cancellation must use the original dispatch port.");
                Assert.IsFalse(ContainsRun(foreignId));
            }
            finally
            {
                File.Delete(ownPath);
                File.Delete(foreignPath);
            }
        }
    }
}
