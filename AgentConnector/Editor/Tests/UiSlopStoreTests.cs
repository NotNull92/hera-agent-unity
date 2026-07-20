using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class UiSlopStoreTests
    {
        [MenuItem("HeraAgent/Tests/UiSlopStore")]
        public static void RunTests()
        {
            var allPassed = true;

            allPassed &= ExpectTrue("ui-slop index loaded", UiSlopStore.Count >= 45);
            allPassed &= ExpectTrue("layout tell present", UiSlopStore.Lookup("box-in-box") != null);
            allPassed &= ExpectTrue("spacing tell present", UiSlopStore.Lookup("unscaled-spacing-ladder") != null);
            allPassed &= ExpectTrue("hangul tell present", UiSlopStore.Lookup("hangul-font-fallback-jump") != null);
            allPassed &= ExpectTrue("unity-native tell present", UiSlopStore.Lookup("missing-canvasscaler") != null);

            // box-in-box carries the game-UI exception gate (functional surfaces
            // like inventory slots are not flattened).
            allPassed &= ExpectTrue(
                "box-in-box keeps the game-UI exception",
                (UiSlopStore.Lookup("box-in-box")?.exception ?? "").Contains("슬롯"));

            // Replacement tells carry a borrow (quantitative snap target);
            // deletion tells do not.
            allPassed &= ExpectTrue("replacement tell has borrow", UiSlopStore.Lookup("low-contrast-text")?.borrow != null);
            allPassed &= ExpectTrue("deletion tell has no borrow", UiSlopStore.Lookup("box-in-box")?.borrow == null);

            // ui_system slice returns the version-appropriate predicate.
            var ugui = UiSlopStore.CheckFor("tmp-italic", "ugui");
            var uitk = UiSlopStore.CheckFor("tmp-italic", "uitk");
            allPassed &= ExpectTrue("ugui check present", !string.IsNullOrEmpty(ugui) && ugui.Contains("m_fontStyle"));
            allPassed &= ExpectTrue("uitk check present", !string.IsNullOrEmpty(uitk) && uitk.Contains("-unity-font-style"));

            allPassed &= ExpectTrue("suggestions available", UiSlopStore.SuggestSimilar("boxinbox").Count > 0);
            allPassed &= ExpectTrue("index has five areas", UiSlopStore.BuildIndex().Count == 5);

            if (allPassed)
                Debug.Log("[UiSlopStoreTests] ALL PASSED");
            else
                Debug.LogError("[UiSlopStoreTests] SOME TESTS FAILED");
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
