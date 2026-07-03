using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class UiDocFixerTests
    {
        [MenuItem("HeraAgent/Tests/UiDocFixer")]
        public static void RunTests()
        {
            bool allPassed = true;
            allPassed &= ExpectProfile(UnityVersionCompat.Docs2022_3, "com.unity.ugui@1.0");
            allPassed &= ExpectProfile(UnityVersionCompat.Docs2023_2, "com.unity.ugui@2.0");
            allPassed &= ExpectProfile(UnityVersionCompat.Docs6000_0, "com.unity.ugui@2.0");
            allPassed &= ExpectProfile(UnityVersionCompat.Docs6000_3, "com.unity.ugui@2.0");
            allPassed &= ExpectProfile(UnityVersionCompat.Docs6000_5, "com.unity.ugui@2.5");

            if (allPassed)
                UnityEngine.Debug.Log("[UiDocFixerTests] ALL PASSED");
            else
                UnityEngine.Debug.LogError("[UiDocFixerTests] SOME TESTS FAILED");
        }

        private static bool ExpectProfile(string docsVersion, string expectedPackage)
        {
            var profile = UiDocFixer.ProfileForDocsVersion(docsVersion);
            var expectedUrl = "https://docs.unity3d.com/Packages/" + expectedPackage + "/manual/index.html";
            if (profile.docs_version == docsVersion && profile.ugui_package == expectedPackage && profile.manual_url == expectedUrl)
            {
                UnityEngine.Debug.Log($"[PASS] {docsVersion} -> {profile.ugui_package}");
                return true;
            }

            UnityEngine.Debug.LogError($"[FAIL] {docsVersion}: expected {expectedPackage}, got {profile.ugui_package}");
            return false;
        }
    }
}
