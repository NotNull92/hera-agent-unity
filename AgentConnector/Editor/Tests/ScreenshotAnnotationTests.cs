using System;
using System.Collections.Generic;
using System.Linq;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeraAgent.Tests
{
    public static class ScreenshotAnnotationTests
    {
        private const string CanvasName = "!HeraScreenshotAnnotationCanvas";

        [MenuItem("HeraAgent/Tests/ScreenshotAnnotations")]
        public static void RunTests()
        {
            var allPassed = true;
            allPassed &= TestAnnotationsOnlyReturnsIdentityBlockingAndCoordinates();
            allPassed &= TestAnnotationsOnlyRejectsPngArgumentsBeforeCapture();
            allPassed &= TestAnnotationBoundsAreStrict();

            if (allPassed)
                Debug.Log("[ScreenshotAnnotationTests] ALL PASSED");
            else
                Debug.LogError("[ScreenshotAnnotationTests] SOME TESTS FAILED");
        }

        private static bool TestAnnotationsOnlyReturnsIdentityBlockingAndCoordinates()
        {
            var eventSystemType = ComponentTypeResolver.Resolve("EventSystem");
            if (eventSystemType == null)
                return Expect(nameof(TestAnnotationsOnlyReturnsIdentityBlockingAndCoordinates), false);

            var existingEventSystems = SceneObjectsWith(eventSystemType);
            var activeStates = existingEventSystems.ToDictionary(go => go, go => go.activeSelf);
            foreach (var existing in existingEventSystems) existing.SetActive(false);

            GameObject eventSystem = null;
            GameObject canvasObject = null;
            try
            {
                eventSystem = new GameObject("HeraScreenshotAnnotationEventSystem");
                eventSystem.AddComponent<EventSystem>();

                canvasObject = new GameObject(CanvasName);
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;
                canvasObject.AddComponent<GraphicRaycaster>();

                var target = CreateRect("TargetButton", canvasObject.transform);
                target.AddComponent<Image>();
                target.AddComponent<Button>();

                var blocker = CreateRect("BlockingGraphic", canvasObject.transform);
                blocker.AddComponent<Image>();

                Canvas.ForceUpdateCanvases();
                var response = EditorScreenshot.HandleCommand(new JObject
                {
                    ["annotations_only"] = true,
                    ["max_annotations"] = 100,
                }) as SuccessResponse;
                var data = response == null ? null : JObject.FromObject(response.data);
                var annotations = data?["ui_annotations"] as JArray;
                var targetId = EntityIdCompat.IdOf(target);
                var blockerId = EntityIdCompat.IdOf(blocker);
                var annotation = annotations?
                    .OfType<JObject>()
                    .FirstOrDefault(item => item.Value<int>("instance_id") == targetId);
                var blockedBy = annotation?["blocked_by"] as JObject;
                var coordinateSpaces = data?["coordinate_spaces"] as JObject;
                var inputSpace = coordinateSpaces?["input"] as JObject;
                var imageSpace = coordinateSpaces?["image"] as JObject;

                var passed = true;
                passed &= Expect("AnnotationsOnlySkipsPixels",
                    response?.success == true
                    && data?.Value<bool>("pixels_requested") == false
                    && data.Value<int>("ui_annotations_limit") == 100
                    && data["path"] == null);
                passed &= Expect("AnnotationIdentity",
                    annotation?.Value<string>("hierarchy_path")
                        == CanvasName.Insert(0, "/") + "/TargetButton");
                passed &= Expect("AnnotationInteractable",
                    annotation?.Value<bool>("interactable") == true);
                if (Application.isPlaying)
                {
                    passed &= Expect("AnnotationRaycastTargetHit",
                        annotation?.Value<bool>("target_hit") == true);
                    passed &= Expect("AnnotationBlocker",
                        blockedBy?.Value<int>("instance_id") == blockerId);
                }
                else
                {
                    passed &= Expect("AnnotationEditModeRaycastShape",
                        annotation?.Property("target_hit")?.Value.Type == JTokenType.Boolean
                        && annotation.Property("target_top_hit")?.Value.Type == JTokenType.Boolean
                        && annotation.Property("blocked_by") != null);
                }
                passed &= Expect("AnnotationPointsAndBounds",
                    annotation?["input_point"] is JArray inputPoint
                    && inputPoint.Count == 2
                    && annotation["image_point"] is JArray imagePoint
                    && imagePoint.Count == 2
                    && annotation["input_bounds"] is JArray inputBounds
                    && inputBounds.Count == 4
                    && annotation["image_bounds"] is JArray imageBounds
                    && imageBounds.Count == 4);
                passed &= Expect("AnnotationCoordinateSpaces",
                    inputSpace?.Value<string>("name")
                        == "unity_screen_bottom_left_pixels"
                    && imageSpace?.Value<string>("name")
                        == "game_view_top_left_pixels");
                return passed;
            }
            finally
            {
                if (canvasObject != null) Object.DestroyImmediate(canvasObject);
                if (eventSystem != null) Object.DestroyImmediate(eventSystem);
                foreach (var entry in activeStates)
                    if (entry.Key != null) entry.Key.SetActive(entry.Value);
            }
        }

        private static bool TestAnnotationsOnlyRejectsPngArgumentsBeforeCapture()
        {
            var response = EditorScreenshot.HandleCommand(new JObject
            {
                ["annotations_only"] = true,
                ["output_path"] = "Screenshots/should-not-exist.png",
            }) as ErrorResponse;
            return Expect(
                nameof(TestAnnotationsOnlyRejectsPngArgumentsBeforeCapture),
                response?.code == "SCREENSHOT_ANNOTATIONS_ONLY_OUTPUT_CONFLICT");
        }

        private static bool TestAnnotationBoundsAreStrict()
        {
            var contract = ToolContractRegistry.Get("screenshot");
            var schema = contract?.InputSchema["properties"]?["max_annotations"];
            var invalid = ToolContractValidator.Validate(
                contract,
                new JObject
                {
                    ["annotations_only"] = true,
                    ["max_annotations"] = 0,
                });
            var runtime = EditorScreenshot.HandleCommand(new JObject
            {
                ["annotations_only"] = true,
                ["max_annotations"] = 101,
            }) as ErrorResponse;
            return Expect(
                nameof(TestAnnotationBoundsAreStrict),
                schema?.Value<int>("minimum") == 1
                && schema.Value<int>("maximum") == 100
                && invalid.Error?.code == "INVALID_ARGUMENT"
                && runtime?.code == "SCREENSHOT_INVALID_MAX_ANNOTATIONS");
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, 80f);
            rect.anchoredPosition = Vector2.zero;
            return gameObject;
        }

        private static List<GameObject> SceneObjectsWith(Type componentType) =>
            Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.scene.IsValid() && go.GetComponent(componentType) != null)
                .ToList();

        private static bool Expect(string name, bool condition)
        {
            if (condition) Debug.Log("[PASS] " + name);
            else Debug.LogError("[FAIL] " + name);
            return condition;
        }
    }
}
