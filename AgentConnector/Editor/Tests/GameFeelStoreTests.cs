using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class GameFeelStoreTests
    {
        [MenuItem("HeraAgent/Tests/GameFeelStore")]
        public static void RunTests()
        {
            var allPassed = true;

            allPassed &= ExpectTrue("game-feel index loaded", GameFeelStore.Count >= 45);
            allPassed &= ExpectTrue("technique topic present", GameFeelStore.Lookup("screen_shake") != null);
            allPassed &= ExpectTrue("ethics topic present", GameFeelStore.Lookup("ethics_checklist") != null);
            allPassed &= ExpectTrue("honest_juice present", GameFeelStore.Lookup("honest_juice") != null);
            allPassed &= ExpectTrue("ui topic present", GameFeelStore.Lookup("ui_bar") != null);
            allPassed &= ExpectTrue(
                "ui_choice_symmetry keeps the checklist",
                (GameFeelStore.Lookup("ui_choice_symmetry")?.body ?? "").Contains("- [ ]"));
            allPassed &= ExpectTrue(
                "screen_shake keeps the accessibility option",
                (GameFeelStore.Lookup("screen_shake")?.body ?? "").Contains("option"));
            allPassed &= ExpectTrue(
                "tweening keeps the one-line lerp",
                (GameFeelStore.Lookup("tweening_easing")?.body ?? "").Contains("x += (target - x) * 0.1f"));
            allPassed &= ExpectTrue("suggestions available", GameFeelStore.SuggestSimilar("screenshake").Count > 0);
            allPassed &= ExpectTrue("index has categories", GameFeelStore.BuildIndex().Count >= 5);

            UIJuiceGuide.ResetShownRecipesForTesting();
            var first = UIJuiceGuide.ForElement("button", false);
            var repeat = UIJuiceGuide.ForElement("Button", true);
            allPassed &= ExpectTrue("first create carries the full recipe", first != null && first.Contains("Hover:"));
            allPassed &= ExpectTrue("repeat create carries only the pointer",
                first != null && repeat != null && !repeat.Contains("Hover:") && repeat.Contains("game_feel <topic>") && repeat.Length < first.Length / 4);
            allPassed &= ExpectTrue("other elements still get their recipe", (UIJuiceGuide.ForElement("panel", false) ?? "").Contains("Golden rule"));
            UIJuiceGuide.ResetShownRecipesForTesting();

            if (allPassed)
                Debug.Log("[GameFeelStoreTests] ALL PASSED");
            else
                Debug.LogError("[GameFeelStoreTests] SOME TESTS FAILED");
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
