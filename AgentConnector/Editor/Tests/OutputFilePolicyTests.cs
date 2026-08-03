using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class OutputFilePolicyTests
    {
        [MenuItem("HeraAgent/Tests/Output File Policy")]
        public static void RunTests()
        {
            var directory = Path.Combine(Path.GetTempPath(), "hera-output-policy-" + Guid.NewGuid().ToString("N"));
            var existing = Path.Combine(directory, "capture.png");
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllBytes(existing, new byte[] { 1 });
                Assert(!OutputFilePolicy.TryResolvePng(existing, null, false, out _, out var existsCode, out _)
                    && existsCode == "OUTPUT_PATH_EXISTS", "existing output must require overwrite=true");
                Assert(OutputFilePolicy.TryResolvePng(existing, null, true, out _, out _, out _),
                    "temp output may be explicitly overwritten");
                Assert(!OutputFilePolicy.TryResolvePng("capture.txt", null, false, out _, out var extensionCode, out _)
                    && extensionCode == "INVALID_OUTPUT_PATH", "non-PNG output must be rejected");

                var trustedRoot = Path.Combine(directory, "project");
                var sibling = trustedRoot + "-other";
                Assert(!OutputFilePolicy.IsUnder(Path.Combine(sibling, "capture.png"), trustedRoot),
                    "prefix siblings must not pass the trusted-root boundary");
                Assert(OutputFilePolicy.IsUnder(Path.Combine(trustedRoot, "captures", "capture.png"), trustedRoot),
                    "descendants must pass the trusted-root boundary");

                Debug.Log("[OutputFilePolicyTests] ALL PASSED");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[OutputFilePolicyTests] " + message);
        }
    }
}
