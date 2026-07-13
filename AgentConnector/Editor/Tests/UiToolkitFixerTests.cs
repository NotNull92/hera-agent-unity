using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            allPassed &= ExpectScreenSpaceDocumentOnLegacyPanelSettings();
            allPassed &= ExpectWorldSpaceDocumentUsesPanelSettingsRenderMode();
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

        private static bool ExpectScreenSpaceDocumentOnLegacyPanelSettings()
        {
            var stem = CreateFixtureStem("HeraUiToolkitScreenSpaceRegression");
            var activeScene = SceneManager.GetActiveScene();
            var isolatedScene = new Scene();
            var previousSelection = Selection.activeObject;
            GameObject parent = null;
            UiToolkitDocument.ApplyResult result = null;
            try
            {
                isolatedScene = OpenIsolatedScene();
                parent = new GameObject(stem + "Parent");
                var document = new JObject
                {
                    ["backend"] = "uitk",
                    ["name"] = stem,
                    ["panel"] = new JObject { ["render_mode"] = "screen-space" },
                    ["root"] = new JObject
                    {
                        ["name"] = "Root",
                        ["element"] = "Button",
                        ["attributes"] = new JObject { ["text"] = "Apply" },
                    },
                };
                result = UiToolkitDocument.Apply(document, parent.transform, upsert: false);
                if (result.Errors.Count == 0 && !result.WorldSpace)
                {
                    Debug.Log("[PASS] screen-space document works without PanelSettings.renderMode");
                    return true;
                }
                Debug.LogError($"[FAIL] screen-space document should emit without PanelSettings.renderMode ({result.Errors.Count} errors)");
                return false;
            }
            finally
            {
                if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
                CloseIsolatedScene(activeScene, isolatedScene);
                Selection.activeObject = previousSelection;
                CleanupFixtureAssets(stem, result);
            }
        }

        private static bool ExpectWorldSpaceDocumentUsesPanelSettingsRenderMode()
        {
            if (!UiToolkitFixer.SupportsWorldSpaceRuntime(Application.unityVersion))
            {
                Debug.Log("[PASS] world-space document emission skipped below Unity 6000.2");
                return true;
            }

            var stem = CreateFixtureStem("HeraUiToolkitWorldSpaceRegression");
            var activeScene = SceneManager.GetActiveScene();
            var isolatedScene = new Scene();
            var previousSelection = Selection.activeObject;
            GameObject parent = null;
            UiToolkitDocument.ApplyResult result = null;
            try
            {
                isolatedScene = OpenIsolatedScene();
                parent = new GameObject(stem + "Parent");
                var document = new JObject
                {
                    ["backend"] = "uitk",
                    ["name"] = stem,
                    ["panel"] = new JObject { ["render_mode"] = "world-space" },
                    ["root"] = new JObject
                    {
                        ["name"] = "Root",
                        ["element"] = "Button",
                        ["attributes"] = new JObject { ["text"] = "Apply" },
                    },
                };
                result = UiToolkitDocument.Apply(document, parent.transform, upsert: false);
                var panel = string.IsNullOrEmpty(result?.PanelSettingsAsset)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<ScriptableObject>(result.PanelSettingsAsset);
                var renderMode = panel?.GetType().GetProperty("renderMode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var mode = renderMode?.GetValue(panel, null)?.ToString();
                if (result.Errors.Count == 0 && result.WorldSpace && string.Equals(mode, "WorldSpace", System.StringComparison.Ordinal))
                {
                    Debug.Log("[PASS] world-space document writes PanelSettings.renderMode");
                    return true;
                }
                Debug.LogError($"[FAIL] world-space document should write PanelSettings.renderMode ({result.Errors.Count} errors, mode={mode ?? "missing"})");
                return false;
            }
            finally
            {
                if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
                CloseIsolatedScene(activeScene, isolatedScene);
                Selection.activeObject = previousSelection;
                CleanupFixtureAssets(stem, result);
            }
        }

        private static Scene OpenIsolatedScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            if (SceneManager.GetActiveScene().handle == scene.handle) return scene;
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            throw new System.InvalidOperationException("could not activate UI Toolkit regression scene");
        }

        private static void CloseIsolatedScene(Scene activeScene, Scene isolatedScene)
        {
            if (activeScene.IsValid()) SceneManager.SetActiveScene(activeScene);
            if (isolatedScene.IsValid()) EditorSceneManager.CloseScene(isolatedScene, true);
        }

        private static string CreateFixtureStem(string prefix)
        {
            return prefix + "_" + System.Guid.NewGuid().ToString("N");
        }

        private static void CleanupFixtureAssets(string stem, UiToolkitDocument.ApplyResult result)
        {
            var paths = new HashSet<string>();
            if (result != null)
            {
                if (!string.IsNullOrEmpty(result.UxmlAsset)) paths.Add(result.UxmlAsset);
                if (!string.IsNullOrEmpty(result.UssAsset)) paths.Add(result.UssAsset);
                if (!string.IsNullOrEmpty(result.PanelSettingsAsset)) paths.Add(result.PanelSettingsAsset);
            }
            if (paths.Count == 0)
            {
                var directory = UiToolkitDocument.DefaultDirectory + "/" + stem;
                paths.Add(directory + ".uxml");
                paths.Add(directory + ".uss");
                paths.Add(directory + "PanelSettings.asset");
            }
            foreach (var path in paths) AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
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
