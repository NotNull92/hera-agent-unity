using System;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class PackageJobStateTests
    {
        [MenuItem("HeraAgent/Tests/PackageJobState")]
        public static void RunTests()
        {
            const long started = 1_000_000;
            var recovered = 0;
            PackageJobState.RecoverPendingFiles(
                new[] { "broken", "healthy" },
                file =>
                {
                    if (file == "broken")
                        throw new InvalidOperationException("simulated corrupt pending file");
                    recovered++;
                });

            if (PackageJobState.HasTimedOut(started, started + 10 * 60 * 1000)
                || !PackageJobState.HasTimedOut(started, started + 10 * 60 * 1000 + 1)
                || recovered != 1)
            {
                throw new InvalidOperationException("[PackageJobStateTests] SOME TESTS FAILED");
            }

            Debug.Log("[PackageJobStateTests] ALL PASSED");
        }
    }
}
