using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class UiToolkitFixerTests
    {
        [MenuItem("HeraAgent/Tests/UiToolkitFixer")]
        public static void RunTests()
        {
            var allPassed = true;
            allPassed &= ExpectWorldSpace("2023.2.20f1", false);
            allPassed &= ExpectWorldSpace("6000.1.9f1", false);
            allPassed &= ExpectWorldSpace("6000.2.0f1", true);
            allPassed &= ExpectWorldSpace("6000.5.0f1", true);
            allPassed &= ExpectValidDocument();
            allPassed &= ExpectRejectedAttribute();
            allPassed &= ExpectRejectedStyleInjection();

            if (allPassed)
                Debug.Log("[UiToolkitFixerTests] ALL PASSED");
            else
                Debug.LogError("[UiToolkitFixerTests] SOME TESTS FAILED");
        }

        private static bool ExpectWorldSpace(string unityVersion, bool expected)
        {
            var actual = UiToolkitFixer.SupportsWorldSpaceRuntime(unityVersion);
            if (actual == expected)
            {
                Debug.Log($"[PASS] world-space {unityVersion} -> {actual}");
                return true;
            }
            Debug.LogError($"[FAIL] world-space {unityVersion}: expected {expected}, got {actual}");
            return false;
        }

        private static bool ExpectValidDocument()
        {
            var document = new JObject
            {
                ["backend"] = "uitk",
                ["root"] = new JObject
                {
                    ["name"] = "Root",
                    ["element"] = "Button",
                    ["attributes"] = new JObject { ["text"] = "Apply" },
                    ["style"] = new JObject { ["flex-direction"] = "column" },
                },
            };
            var diagnostics = new List<UiToolkitFixer.Report>();
            UiToolkitFixer.ValidateDocument(document, new List<UiToolkitFixer.Report>(), diagnostics);
            if (!UiToolkitFixer.HasErrors(diagnostics))
            {
                Debug.Log("[PASS] reflected Button document accepted");
                return true;
            }
            Debug.LogError("[FAIL] reflected Button document was rejected");
            return false;
        }

        private static bool ExpectRejectedAttribute()
        {
            var document = new JObject
            {
                ["backend"] = "uitk",
                ["root"] = new JObject
                {
                    ["element"] = "Button",
                    ["attributes"] = new JObject { ["not-a-real-attribute"] = "x" },
                    ["style"] = new JObject { ["not-a-real-uss"] = "1px" },
                },
            };
            var diagnostics = new List<UiToolkitFixer.Report>();
            UiToolkitFixer.ValidateDocument(document, new List<UiToolkitFixer.Report>(), diagnostics);
            var sawAttributeError = false;
            var sawUssWarning = false;
            foreach (var diagnostic in diagnostics)
            {
                sawAttributeError |= diagnostic.rule == "uitk.attribute.unsupported" && diagnostic.severity == "error";
                sawUssWarning |= diagnostic.rule == "uitk.uss.unsupported" && diagnostic.severity == "warning";
            }
            if (sawAttributeError && sawUssWarning)
            {
                Debug.Log("[PASS] invalid UITK attribute rejected and USS property downgraded");
                return true;
            }
            Debug.LogError("[FAIL] expected UITK validation diagnostics were missing");
            return false;
        }

        private static bool ExpectRejectedStyleInjection()
        {
            var document = new JObject
            {
                ["backend"] = "uitk",
                ["root"] = new JObject
                {
                    ["element"] = "Button",
                    ["style"] = new JObject { ["color"] = "red; not-a-real-uss: value" },
                },
            };
            var diagnostics = new List<UiToolkitFixer.Report>();
            UiToolkitFixer.ValidateDocument(document, new List<UiToolkitFixer.Report>(), diagnostics);
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.rule == "uitk.uss.value" && diagnostic.severity == "error")
                {
                    Debug.Log("[PASS] unsafe USS declaration rejected");
                    return true;
                }
            }
            Debug.LogError("[FAIL] expected unsafe USS declaration to be rejected");
            return false;
        }
    }
}
