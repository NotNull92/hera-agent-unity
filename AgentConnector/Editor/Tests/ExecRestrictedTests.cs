using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class ExecRestrictedTests
    {
        [MenuItem("HeraAgent/Tests/ExecRestricted")]
        public static void RunTests()
        {
            var allPassed = true;

            var contract = ToolContractRegistry.Get("exec");
            var restrictedContract = ToolContractValidator.Validate(
                contract,
                new JObject { ["code"] = "return 1;", ["security_mode"] = "restricted" });
            var invalidContract = ToolContractValidator.Validate(
                contract,
                new JObject { ["code"] = "return 1;", ["security_mode"] = "unknown" });
            allPassed &= ExpectTrue("restricted strict contract",
                restrictedContract.IsValid && invalidContract.Error?.code == "INVALID_ARGUMENT");
            allPassed &= ExpectSuccess("full default unchanged", ExecuteCsharp.HandleCommand(
                new JObject { ["code"] = "return typeof(System.IO.File).FullName;", ["no_cache"] = true }));
            allPassed &= ExpectSuccess("restricted benign", ExecuteCsharp.HandleCommand(
                Restricted("return UnityEngine.Application.unityVersion;")));
            allPassed &= ExpectCode("restricted source", ExecuteCsharp.HandleCommand(
                Restricted("return File.Exists(\"x\");")), "EXEC_RESTRICTED_SOURCE_DENIED");
            allPassed &= ExpectCode("restricted metadata", ExecuteCsharp.HandleCommand(
                Restricted("return Newtonsoft.Json.Linq.JObject.Parse(\"{}\").Count;")), "EXEC_RESTRICTED_METADATA_DENIED");
            allPassed &= ExpectCode("restricted IL", ExecuteCsharp.HandleCommand(
                Restricted("Console.WriteLine(\"blocked\"); return null;")), "EXEC_RESTRICTED_IL_DENIED");

            if (allPassed)
                Debug.Log("[ExecRestrictedTests] ALL PASSED");
            else
                Debug.LogError("[ExecRestrictedTests] SOME TESTS FAILED");
        }

        private static JObject Restricted(string code)
        {
            return new JObject
            {
                ["code"] = code,
                ["security_mode"] = "restricted",
                ["no_cache"] = true,
            };
        }

        private static bool ExpectSuccess(string label, object response)
        {
            var passed = response is SuccessResponse;
            Log(label, passed, response);
            return passed;
        }

        private static bool ExpectCode(string label, object response, string expected)
        {
            var passed = response is ErrorResponse error && error.code == expected;
            Log(label, passed, response);
            return passed;
        }

        private static void Log(string label, bool passed, object response)
        {
            if (passed)
                Debug.Log("[PASS] " + label);
            else
                Debug.LogError($"[FAIL] {label}: {JsonUtility.ToJson(response)}");
        }

        private static bool ExpectTrue(string label, bool passed)
        {
            Log(label, passed, null);
            return passed;
        }
    }
}
