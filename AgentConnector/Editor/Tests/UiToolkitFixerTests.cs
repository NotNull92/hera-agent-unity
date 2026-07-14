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
            allPassed &= ExpectInvalidResolutionDoesNotCreateAssets();
            allPassed &= ExpectCreateRollbackAfterInjectedFailure();
            allPassed &= ExpectUpsertFailureKeepsExistingArtifacts();
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

        private static bool ExpectInvalidResolutionDoesNotCreateAssets()
        {
            var stem = CreateFixtureStem("HeraUiToolkitPreflightRegression");
            var document = CreateDocument(stem);
            document["panel"] = new JObject { ["reference_resolution"] = new JArray("wide", 1080) };
            var result = UiToolkitDocument.Apply(document, null, upsert: false);
            var assetsAbsent = AssetsAbsent(stem);
            if (result.Errors.Count > 0 && assetsAbsent)
            {
                Debug.Log("[PASS] invalid UI Toolkit panel resolution creates no assets");
                return true;
            }
            Debug.LogError("[FAIL] invalid UI Toolkit panel resolution should fail before asset creation");
            CleanupFixtureAssets(stem, result);
            return false;
        }

        private static bool ExpectCreateRollbackAfterInjectedFailure()
        {
            var stem = CreateFixtureStem("HeraUiToolkitCreateRollbackRegression");
            var activeScene = SceneManager.GetActiveScene();
            var isolatedScene = new Scene();
            GameObject parent = null;
            UiToolkitDocument.ApplyResult result = null;
            try
            {
                isolatedScene = OpenIsolatedScene();
                parent = new GameObject(stem + "Parent");
                UiToolkitDocument.FailureInjectionForTests = stage =>
                    stage == UiToolkitDocument.FailureStageAfterAssets
                        ? new System.InvalidOperationException("injected create failure")
                        : null;
                result = UiToolkitDocument.Apply(CreateDocument(stem), parent.transform, upsert: false);
                var rolledBack = result.Errors.Count > 0
                    && result.RollbackAttempted
                    && result.RollbackErrors.Count == 0
                    && result.RolledBackArtifacts.Count >= 2;
                if (rolledBack && AssetsAbsent(stem) && FindSceneObject("HeraUITK_" + stem) == null)
                {
                    Debug.Log("[PASS] failed create removes only its generated UI Toolkit artifacts");
                    return true;
                }
                Debug.LogError("[FAIL] failed create should roll back its generated assets and UIDocument GameObject");
                return false;
            }
            finally
            {
                UiToolkitDocument.FailureInjectionForTests = null;
                if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
                CloseIsolatedScene(activeScene, isolatedScene);
                CleanupFixtureAssets(stem, result);
            }
        }

        private static bool ExpectUpsertFailureKeepsExistingArtifacts()
        {
            var stem = CreateFixtureStem("HeraUiToolkitUpsertRollbackRegression");
            var activeScene = SceneManager.GetActiveScene();
            var isolatedScene = new Scene();
            GameObject parent = null;
            UiToolkitDocument.ApplyResult initial = null;
            UiToolkitDocument.ApplyResult failed = null;
            try
            {
                isolatedScene = OpenIsolatedScene();
                parent = new GameObject(stem + "Parent");
                initial = UiToolkitDocument.Apply(CreateDocument(stem), parent.transform, upsert: false);
                if (initial.Errors.Count > 0) return Fail("could not create upsert fixture");

                UiToolkitDocument.FailureInjectionForTests = stage =>
                    stage == UiToolkitDocument.FailureStageAfterAssets
                        ? new System.InvalidOperationException("injected upsert failure")
                        : null;
                failed = UiToolkitDocument.Apply(CreateDocument(stem), parent.transform, upsert: true);
                var retained = AssetDatabase.LoadMainAssetAtPath(initial.UxmlAsset) != null
                    && AssetDatabase.LoadMainAssetAtPath(initial.UssAsset) != null
                    && AssetDatabase.LoadMainAssetAtPath(initial.PanelSettingsAsset) != null
                    && FindSceneObject("HeraUITK_" + stem) != null;
                if (failed.Errors.Count > 0 && failed.UpsertMayBePartial && !failed.RollbackAttempted && retained)
                {
                    Debug.Log("[PASS] failed upsert retains existing artifacts and reports partial-update risk");
                    return true;
                }
                Debug.LogError("[FAIL] failed upsert should retain existing artifacts and report partial-update risk");
                return false;
            }
            finally
            {
                UiToolkitDocument.FailureInjectionForTests = null;
                if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
                CloseIsolatedScene(activeScene, isolatedScene);
                CleanupFixtureAssets(stem, initial);
            }
        }

        private static JObject CreateDocument(string stem)
        {
            return new JObject
            {
                ["backend"] = "uitk",
                ["name"] = stem,
                ["root"] = new JObject
                {
                    ["name"] = "Root",
                    ["element"] = "Button",
                    ["attributes"] = new JObject { ["text"] = "Apply" },
                },
            };
        }

        private static bool AssetsAbsent(string stem)
        {
            return AssetDatabase.LoadMainAssetAtPath(UiToolkitDocument.DefaultDirectory + "/" + stem + ".uxml") == null
                && AssetDatabase.LoadMainAssetAtPath(UiToolkitDocument.DefaultDirectory + "/" + stem + ".uss") == null
                && AssetDatabase.LoadMainAssetAtPath(UiToolkitDocument.DefaultDirectory + "/" + stem + "PanelSettings.asset") == null;
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                if (candidate.scene.IsValid() && candidate.name == name) return candidate;
            return null;
        }

        private static bool Fail(string message)
        {
            Debug.LogError("[FAIL] " + message);
            return false;
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
