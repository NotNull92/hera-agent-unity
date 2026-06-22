using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class UnityVersionCompatTests
    {
        [MenuItem("HeraAgent/Tests/UnityVersionCompat")]
        public static void RunTests()
        {
            bool allPassed = true;
            allPassed &= Expect("2022.3.45f1", UnityVersionCompat.Docs2022_3);
            allPassed &= Expect("2023.2.20f1", UnityVersionCompat.Docs2023_2);
            allPassed &= Expect("6000.0.58f2", UnityVersionCompat.Docs6000_0);
            allPassed &= Expect("6000.2.9f1", UnityVersionCompat.Docs6000_0);
            allPassed &= Expect("6000.3.5f1", UnityVersionCompat.Docs6000_3);
            allPassed &= Expect("6000.4.1f1", UnityVersionCompat.Docs6000_3);
            allPassed &= Expect("6000.5.0b11", UnityVersionCompat.Docs6000_5);
            allPassed &= Expect("6000.6.0a1", UnityVersionCompat.Docs6000_5);
            allPassed &= Expect("", UnityVersionCompat.Docs6000_0);
            allPassed &= Expect("bad-version", UnityVersionCompat.Docs6000_0);
            allPassed &= ExpectAtLeast(UnityVersionCompat.Docs2023_2, UnityVersionCompat.Docs6000_0, false);
            allPassed &= ExpectAtLeast(UnityVersionCompat.Docs6000_3, UnityVersionCompat.Docs6000_0, true);
            allPassed &= ExpectAtLeast(UnityVersionCompat.Docs6000_5, UnityVersionCompat.Docs6000_3, true);

            if (allPassed)
                Debug.Log("[UnityVersionCompatTests] ALL PASSED");
            else
                Debug.LogError("[UnityVersionCompatTests] SOME TESTS FAILED");
        }

        private static bool Expect(string unityVersion, string expected)
        {
            var actual = UnityVersionCompat.DocsVersionFor(unityVersion);
            if (actual == expected)
            {
                Debug.Log($"[PASS] {unityVersion} -> {actual}");
                return true;
            }

            Debug.LogError($"[FAIL] {unityVersion}: expected {expected}, got {actual}");
            return false;
        }

        private static bool ExpectAtLeast(string docsVersion, string minimumDocsVersion, bool expected)
        {
            var actual = UnityVersionCompat.DocsVersionAtLeast(docsVersion, minimumDocsVersion);
            if (actual == expected)
            {
                Debug.Log($"[PASS] {docsVersion} >= {minimumDocsVersion}: {actual}");
                return true;
            }

            Debug.LogError($"[FAIL] {docsVersion} >= {minimumDocsVersion}: expected {expected}, got {actual}");
            return false;
        }
    }
}
