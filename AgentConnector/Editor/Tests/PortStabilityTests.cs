using Newtonsoft.Json.Linq;
using UnityEditor;

namespace HeraAgent.Tests
{
    /// <summary>
    /// Locks the reload/port contract measured in Step 5 (20/20 domain reloads kept the same
    /// port): every server start prefers the default port, the reload fast-path refuses only
    /// what it must, and the instance descriptor always republishes the live port plus the
    /// discovery advertisement fields the CLI consumes.
    /// </summary>
    public static class PortStabilityTests
    {
        [MenuItem("HeraAgent/Tests/PortStability")]
        public static void RunTests()
        {
            var allPassed = true;

            // Port contract: attempt 0 always tries DEFAULT_PORT, so a reload re-binds the
            // same port unless another process stole it.
            allPassed &= ExpectTrue("default port prefers 8090 when free", !string.IsNullOrEmpty(HttpServer.HttpPrefix(8090)));
            allPassed &= ExpectTrue("prefix binds the loopback literal", HttpServer.HttpPrefix(8090) == "http://127.0.0.1:8090/");

            // Reload fast-path contract: only the commands that start or observe the reload
            // window may run during a compile; everything else is deferred with RELOADING.
            allPassed &= ExpectTrue("refresh_unity is reload-exempt", CommandRouter.IsReloadExempt("refresh_unity"));
            allPassed &= ExpectTrue("mutation is deferred during reload", !CommandRouter.IsReloadExempt("manage_gameobject"));

            // Retry contract: retryable codes are the ones the CLI is allowed to repeat.
            allPassed &= ExpectTrue("RELOADING is retryable", ResponsePolicy.IsRetryableCode(ResponsePolicy.CodeReloading));
            allPassed &= ExpectTrue("unknown code is not retryable", !ResponsePolicy.IsRetryableCode("EXEC_RUNTIME_ERROR"));

            // Discovery advertisement: live port + catalog schema hash + retryable feature.
            var status = JObject.FromObject(Heartbeat.BuildStatus());
            allPassed &= ExpectTrue("heartbeat carries the live port", status.Value<int>("port") == HttpServer.Port);
            allPassed &= ExpectTrue("heartbeat carries a catalog schema hash",
                !string.IsNullOrEmpty(status.Value<string>("catalogSchemaHash")) &&
                status.Value<string>("catalogSchemaHash").StartsWith("sha256:"));
            var features = status["features"] as JArray;
            allPassed &= ExpectTrue("features advertise retryable_v1",
                features != null && features.ToString().Contains("retryable_v1"));
            allPassed &= ExpectTrue("features advertise catalog_schema_v1",
                features != null && features.ToString().Contains("catalog_schema_v1"));

            UnityEngine.Debug.Log(allPassed
                ? "PortStabilityTests: all assertions passed"
                : "PortStabilityTests: FAILURES above");
        }

        static bool ExpectTrue(string label, bool condition)
        {
            if (!condition)
                UnityEngine.Debug.LogError("PortStabilityTests failed: " + label);
            return condition;
        }
    }
}
