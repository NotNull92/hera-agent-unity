using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class UnityDocsStoreTests
    {
        [MenuItem("HeraAgent/Tests/UnityDocsStore")]
        public static void RunTests()
        {
            var allPassed = true;
            var loadedDocsVersion = UnityDocsStore.LoadedDocsVersion;
            var currentDocsVersion = UnityVersionCompat.CurrentDocsVersion();

            allPassed &= ExpectTrue("docs index loaded", UnityDocsStore.Count > 1000);
            allPassed &= ExpectTrue(
                "loaded docs version matches current bucket or fallback",
                loadedDocsVersion == currentDocsVersion || loadedDocsVersion == UnityVersionCompat.Docs6000_0);
            allPassed &= ExpectTrue("legacy GameObject page present", UnityDocsStore.Lookup("GameObject") != null);
            allPassed &= ExpectTrue("property page present", UnityDocsStore.Lookup("Rigidbody-mass") != null);
            allPassed &= ExpectTrue("method page present", UnityDocsStore.Lookup("GameObject.AddComponent") != null);
            allPassed &= ExpectTrue("editor method page present", UnityDocsStore.Lookup("AssetDatabase.Refresh") != null);
            allPassed &= ExpectTrue("suggestions available", UnityDocsStore.SuggestSimilar("GameObjekt").Count > 0);

            if (allPassed)
                Debug.Log("[UnityDocsStoreTests] ALL PASSED");
            else
                Debug.LogError("[UnityDocsStoreTests] SOME TESTS FAILED");
        }

        private static bool ExpectTrue(string label, bool actual)
        {
            if (actual)
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError("[FAIL] " + label + ": expected true");
            return false;
        }
    }
}
