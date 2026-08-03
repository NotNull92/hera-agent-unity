using System;
using System.Collections.Generic;
using System.Linq;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeraAgent.Tests
{
    public static class UiDocApplyTests
    {
        const string RootName = "HeraUiDocApplyTestCanvas";
        const string TextName = "HeraUiDocApplyTestText";

        [MenuItem("HeraAgent/Tests/UiDocApply")]
        public static void RunTests()
        {
            var allPassed = true;
            allPassed &= TestRootCanvasCreatesEventSystem();
            allPassed &= TestSharedEventSystemGetsCompatibleModule();
            allPassed &= TestRootCanvasUpsertReusesSceneRoot();
            allPassed &= TestAutoTextHasRenderableFont();

            if (allPassed)
                Debug.Log("[UiDocApplyTests] ALL PASSED");
            else
                Debug.LogError("[UiDocApplyTests] SOME TESTS FAILED");
        }

        static bool TestRootCanvasCreatesEventSystem()
        {
            var eventSystemType = ComponentTypeResolver.Resolve("EventSystem");
            if (eventSystemType == null)
                return Expect(nameof(TestRootCanvasCreatesEventSystem), false);

            var existing = SceneObjectsWith(eventSystemType);
            var activeStates = existing.ToDictionary(go => go, go => go.activeSelf);
            foreach (var go in existing) go.SetActive(false);

            try
            {
                ApplyRootCanvas("create");
                var created = SceneObjectsWith(eventSystemType)
                    .FirstOrDefault(go => !existing.Contains(go) && go.activeInHierarchy);
                if (created == null)
                    return Expect(nameof(TestRootCanvasCreatesEventSystem), false);

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                var expectedModule = ComponentTypeResolver.Resolve("InputSystemUIInputModule");
#else
                var expectedModule = ComponentTypeResolver.Resolve("StandaloneInputModule");
#endif
                return Expect(
                    nameof(TestRootCanvasCreatesEventSystem),
                    expectedModule != null && created.GetComponent(expectedModule) != null);
            }
            finally
            {
                DestroyNamed(RootName);
                foreach (var go in SceneObjectsWith(eventSystemType))
                    if (!existing.Contains(go)) Object.DestroyImmediate(go);
                foreach (var entry in activeStates)
                    if (entry.Key != null) entry.Key.SetActive(entry.Value);
            }
        }

        static bool TestRootCanvasUpsertReusesSceneRoot()
        {
            var eventSystemType = ComponentTypeResolver.Resolve("EventSystem");
            var existing = eventSystemType == null
                ? new List<GameObject>()
                : SceneObjectsWith(eventSystemType);
            var activeStates = existing.ToDictionary(go => go, go => go.activeSelf);
            foreach (var go in existing) go.SetActive(false);

            try
            {
                ApplyRootCanvas("upsert");
                ApplyRootCanvas("upsert");
                return Expect(
                    nameof(TestRootCanvasUpsertReusesSceneRoot),
                    SceneObjectsNamed(RootName).Count == 1);
            }
            finally
            {
                DestroyNamed(RootName);
                if (eventSystemType != null)
                {
                    foreach (var go in SceneObjectsWith(eventSystemType))
                        if (!existing.Contains(go)) Object.DestroyImmediate(go);
                }
                foreach (var entry in activeStates)
                    if (entry.Key != null) entry.Key.SetActive(entry.Value);
            }
        }

        static bool TestSharedEventSystemGetsCompatibleModule()
        {
            var eventSystemType = ComponentTypeResolver.Resolve("EventSystem");
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var expectedModule = ComponentTypeResolver.Resolve("InputSystemUIInputModule");
            var incompatibleModule = ComponentTypeResolver.Resolve("StandaloneInputModule");
#elif ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            var expectedModule = ComponentTypeResolver.Resolve("StandaloneInputModule");
            var incompatibleModule = ComponentTypeResolver.Resolve("InputSystemUIInputModule");
#else
            var expectedModule = ComponentTypeResolver.Resolve("StandaloneInputModule");
            var incompatibleModule = (Type)null;
#endif
            if (eventSystemType == null || expectedModule == null)
                return Expect(nameof(TestSharedEventSystemGetsCompatibleModule), false);

            var existing = SceneObjectsWith(eventSystemType);
            var activeStates = existing.ToDictionary(go => go, go => go.activeSelf);
            foreach (var go in existing) go.SetActive(false);
            var eventSystem = new GameObject("HeraUiDocApplyExistingEventSystem");
            eventSystem.AddComponent(eventSystemType);
            var incompatible = incompatibleModule == null
                ? null
                : eventSystem.AddComponent(incompatibleModule) as Behaviour;

            try
            {
                var resolved = UiEventSystem.Ensure().GameObject;
                return Expect(
                    nameof(TestSharedEventSystemGetsCompatibleModule),
                    resolved == eventSystem
                    && resolved.GetComponent(expectedModule) != null
                    && (incompatible == null || !incompatible.enabled));
            }
            finally
            {
                Object.DestroyImmediate(eventSystem);
                foreach (var entry in activeStates)
                    if (entry.Key != null) entry.Key.SetActive(entry.Value);
            }
        }

        static bool TestAutoTextHasRenderableFont()
        {
            GameObject created = null;
            try
            {
                var stats = new UiDocSchema.ApplyStats();
                created = UiDocSchema.ApplyNode(
                    new JObject
                    {
                        ["name"] = TextName,
                        ["element"] = "text",
                        ["text"] = new JObject
                        {
                            ["value"] = "Visible",
                            ["engine"] = "auto",
                        },
                    },
                    null,
                    stats,
                    false);
                var text = ComponentTypeResolver.Resolve("TextMeshProUGUI") is Type tmpType
                    ? created.GetComponent(tmpType)
                    : null;
                text = text ?? (ComponentTypeResolver.Resolve("Text") is Type legacyType
                    ? created.GetComponent(legacyType)
                    : null);
                var font = text?.GetType().GetProperty("font")?.GetValue(text) as Object;
                return Expect(nameof(TestAutoTextHasRenderableFont), text != null && font != null);
            }
            finally
            {
                if (created != null) Object.DestroyImmediate(created);
            }
        }

        static void ApplyRootCanvas(string mode)
        {
            UiDoc.HandleCommand(new JObject
            {
                ["action"] = "apply",
                ["mode"] = mode,
                ["doc"] = new JObject
                {
                    ["schema"] = UiDocSchema.SchemaId,
                    ["backend"] = "ugui",
                    ["root"] = new JObject
                    {
                        ["name"] = RootName,
                        ["element"] = "canvas",
                    },
                },
            });
        }

        static List<GameObject> SceneObjectsNamed(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.scene.IsValid() && go.name == name)
                .ToList();

        static List<GameObject> SceneObjectsWith(Type componentType) =>
            Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.scene.IsValid() && go.GetComponent(componentType) != null)
                .ToList();

        static void DestroyNamed(string name)
        {
            foreach (var go in SceneObjectsNamed(name)) Object.DestroyImmediate(go);
        }

        static bool Expect(string name, bool condition)
        {
            if (condition) Debug.Log($"[PASS] {name}");
            else Debug.LogError($"[FAIL] {name}");
            return condition;
        }
    }
}
