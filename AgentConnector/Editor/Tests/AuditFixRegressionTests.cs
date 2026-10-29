using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HeraAgent.Tests
{
    public sealed class AuditFixRegressionTests
    {
        const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

#if UNITY_2022_3
        [UnityTest]
        public IEnumerator RouterFailureDoesNotLogException()
        {
            var task = RouterFailureDoesNotLogExceptionAsync();
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }

        private async Task RouterFailureDoesNotLogExceptionAsync()
#else
        [Test]
        public async Task RouterFailureDoesNotLogException()
#endif
        {
            ToolDiscovery.FindDefaultHandler("describe_type");
            var tools = (IDictionary)typeof(ToolDiscovery).GetField("s_Tools", PrivateStatic).GetValue(null);
            var original = tools["describe_type"];
            var replacement = tools["describe_type"];
            replacement.GetType().GetField("DefaultHandler").SetValue(replacement,
                GetType().GetMethod(nameof(ThrowForRouter)));
            tools["describe_type"] = replacement;
            try
            {
                var response = await CommandRouter.Dispatch("describe_type", new JObject { ["type"] = "Transform" });
                Assert.AreEqual("TOOL_FAILED", (response as ErrorResponse)?.code);
                // Unity Test Runner fails this test on any unexpected Exception log.
            }
            finally { tools["describe_type"] = original; }
        }

        public static object ThrowForRouter(JObject parameters) => throw new InvalidOperationException("audit router failure");

        [Test]
        public void PendingWriteFailureEmitsWarning()
        {
            Type state = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                state = state ?? assembly.GetType("HeraAgent.TestRunner.TestRunnerState");
            Assert.IsNotNull(state);
            string warning = null;
            void Capture(string message, string stack, LogType type)
            {
                if (type == LogType.Warning && message.StartsWith("[Hera] I couldn't write the pending test record:"))
                    warning = message;
            }
            Application.logMessageReceived += Capture;
            try
            {
                state.GetMethod("WritePending", PrivateStatic).Invoke(null,
                    new object[] { 8090, "\0", "", "PlayMode", false, null, null });
                Assert.IsNotNull(warning, "An invalid pending path must not fail silently.");
            }
            finally { Application.logMessageReceived -= Capture; }
        }

        [Test]
        public void ConsoleWarningFilterIsCultureInvariant()
        {
            var oldCulture = CultureInfo.CurrentCulture;
            var marker = "audit-warning-" + Guid.NewGuid().ToString("N");
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                UnityEngine.Debug.LogWarning(marker);
                var response = (SuccessResponse)ReadConsole.HandleCommand(new JObject
                {
                    ["type"] = "WARNING", ["lines"] = 1, ["stacktrace"] = "none"
                });
                StringAssert.Contains(marker, JObject.FromObject(response.data)["entries"].ToString());
            }
            finally { CultureInfo.CurrentCulture = oldCulture; }
        }

        [Test]
        public void RepeatedHintsKeepOnlyShortTopicPointers()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "hera-hint-test-" + Guid.NewGuid().ToString("N"));
            var configPath = Path.Combine(tempDirectory, "asset-config.json");
            var previousConfigPath = HeraSettings.s_ConfigPathOverride;
            try
            {
                HeraSettings.s_ConfigPathOverride = configPath;
                Assert.AreEqual(configPath, typeof(HeraSettings).GetMethod("ConfigPath", PrivateStatic).Invoke(null, null));
                StringAssert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(configPath));
                Directory.CreateDirectory(tempDirectory);
                File.WriteAllText(configPath, "{\"game_feel_mode\":true,\"ui_slop_mode\":true}");
                HeraSettings.ResetForTests();
                CheckHint(typeof(ManageComponents), "WithGameFeelHint", typeof(Camera), "game_feel camera");
                CheckHint(typeof(ManageComponents), "WithUiSlopHint", ComponentTypeResolver.Resolve("Image"), "ui_slop raycast-target-overuse");
                CheckHint(typeof(ManageUI), "WithUiSlopHint", "panel", "ui_slop box-in-box");
            }
            finally
            {
                HeraSettings.s_ConfigPathOverride = previousConfigPath;
                HeraSettings.ResetForTests();
                ClearHints(typeof(ManageComponents));
                ClearHints(typeof(ManageUI));
                if (File.Exists(configPath)) File.Delete(configPath);
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory);
            }
        }

        [Test]
        public void InputSystemTimeoutPreservesMessageAndOtherFailuresKeepPrefix()
        {
            var method = typeof(InputQaInputSystem).GetMethod("InvocationFailure", PrivateStatic);
            Assert.IsNotNull(method);
            const string timeoutMessage = "[Hera] I timed out waiting for a player input update.";
            foreach (var error in new Exception[]
            {
                new TimeoutException(timeoutMessage),
                new TargetInvocationException(new TimeoutException(timeoutMessage)),
            })
            {
                var response = (ErrorResponse)method.Invoke(null, new object[] { error });
                Assert.AreEqual("INPUTSYSTEM_INVOCATION_FAILED", response.code);
                Assert.AreEqual(timeoutMessage, response.message);
            }
            var other = (ErrorResponse)method.Invoke(null,
                new object[] { new InvalidOperationException("fixture failure") });
            Assert.AreEqual("INPUTSYSTEM_INVOCATION_FAILED", other.code);
            Assert.AreEqual("Input System rejected the synthesized event: fixture failure", other.message);
        }

        static void CheckHint(Type owner, string methodName, object target, string topic)
        {
            ClearHints(owner);
            var method = owner.GetMethod(methodName, PrivateStatic);
            var first = (SuccessResponse)method.Invoke(null, new[] { target, new SuccessResponse("OK") });
            var repeat = (SuccessResponse)method.Invoke(null, new[] { target, new SuccessResponse("OK") });
            StringAssert.Contains(topic, first.agent_hint);
            StringAssert.Contains(topic, repeat.agent_hint);
            Assert.Less(repeat.agent_hint.Length, first.agent_hint.Length / 2);
        }

        static void ClearHints(Type owner)
        {
            var set = owner.GetField("s_ShownHints", PrivateStatic)?.GetValue(null);
            set?.GetType().GetMethod("Clear").Invoke(set, null);
        }
    }
}
