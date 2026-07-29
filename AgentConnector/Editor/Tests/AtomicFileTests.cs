using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class AtomicFileTests
    {
        [MenuItem("HeraAgent/Tests/AtomicFile")]
        public static void RunTests()
        {
            var directory = Path.Combine(Path.GetTempPath(), "hera-atomic-file-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "heartbeat.json");
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(path, "old");
                var replaceAttempted = false;

                AtomicFile.WriteAllTextCore(path, "new", (source, destination) =>
                {
                    replaceAttempted = true;
                    throw new IOException("simulated destination replacement lock");
                });

                var passed = replaceAttempted &&
                    File.ReadAllText(path) == "new" &&
                    Directory.GetFiles(directory, "*.tmp").Length == 0;
                if (!passed)
                    throw new InvalidOperationException("[AtomicFileTests] SOME TESTS FAILED");
                Debug.Log("[AtomicFileTests] ALL PASSED");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
