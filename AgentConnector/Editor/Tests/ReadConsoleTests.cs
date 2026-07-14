using System;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class ReadConsoleTests
    {
        [MenuItem("HeraAgent/Tests/ReadConsole")]
        public static void RunTests()
        {
            bool allPassed = true;
            var start = ReadData(new JObject
            {
                ["type"] = "log",
                ["stacktrace"] = "none",
                ["lines"] = 0,
            }).Value<int>("last_cursor");

            var prefix = "[ReadConsoleTests] " + Guid.NewGuid().ToString("N") + " ";
            for (int i = 0; i < 21; i++)
                Debug.Log(prefix + i);

            var defaultPage = ReadData(new JObject
            {
                ["type"] = "log",
                ["stacktrace"] = "none",
                ["since"] = start,
            });
            allPassed &= Expect("default lines caps at 20", defaultPage.Value<int>("returned") == 20);
            allPassed &= Expect("default page is truncated", defaultPage.Value<bool>("truncated"));

            var unlimited = ReadData(new JObject
            {
                ["type"] = "log",
                ["stacktrace"] = "none",
                ["since"] = start,
                ["lines"] = 0,
            });
            allPassed &= Expect("lines=0 is unlimited", unlimited.Value<int>("returned") >= 21);
            allPassed &= Expect("unlimited page is not truncated", !unlimited.Value<bool>("truncated"));

            var one = ReadData(new JObject
            {
                ["type"] = "log",
                ["stacktrace"] = "none",
                ["since"] = start,
                ["lines"] = 1,
            });
            allPassed &= Expect("lines=1 returns one", one.Value<int>("returned") == 1);
            allPassed &= Expect("lines=1 is truncated", one.Value<bool>("truncated"));

            var two = ReadData(new JObject
            {
                ["type"] = "log",
                ["stacktrace"] = "none",
                ["since"] = one.Value<int>("last_cursor"),
                ["lines"] = 2,
            });
            allPassed &= Expect("cursor resumes after returned entry", two.Value<int>("returned") == 2);

            var staleCursor = ReadData(new JObject
            {
                ["type"] = "log",
                ["stacktrace"] = "none",
                ["since"] = int.MaxValue,
                ["lines"] = 1,
            });
            allPassed &= Expect("stale cursor restarts from zero", staleCursor.Value<int>("returned") == 1);

            allPassed &= ExpectError("negative lines rejected", new JObject { ["lines"] = -1 }, "INVALID_PARAM");
            allPassed &= ExpectError("negative since rejected", new JObject { ["since"] = -1 }, "INVALID_PARAM");

            if (allPassed)
                Debug.Log("[ReadConsoleTests] ALL PASSED");
            else
                Debug.LogError("[ReadConsoleTests] SOME TESTS FAILED");
        }

        private static JObject ReadData(JObject parameters)
        {
            var response = ReadConsole.HandleCommand(parameters) as SuccessResponse;
            if (response == null)
                throw new InvalidOperationException("Expected a successful console response.");
            return JObject.FromObject(response.data);
        }

        private static bool ExpectError(string label, JObject parameters, string expectedCode)
        {
            var response = ReadConsole.HandleCommand(parameters) as ErrorResponse;
            return Expect(label, response != null && response.code == expectedCode);
        }

        private static bool Expect(string label, bool actual)
        {
            if (actual)
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError("[FAIL] " + label);
            return false;
        }
    }
}
