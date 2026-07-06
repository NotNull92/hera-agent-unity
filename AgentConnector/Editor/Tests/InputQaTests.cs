using System;
using System.Threading.Tasks;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeraAgent.Tests
{
    public static class InputQaTests
    {
        [MenuItem("HeraAgent/Tests/InputQa")]
        public static async void RunTests()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[InputQaTests] SKIPPED: run in Play Mode so uGUI raycasters process live input.");
                return;
            }

            bool allPassed = true;
            var root = new GameObject("HeraInputQaRoot");
            try
            {
                if (EventSystem.current == null)
                {
                    var eventSystemGo = new GameObject("EventSystem");
                    eventSystemGo.transform.SetParent(root.transform, false);
                    eventSystemGo.AddComponent<EventSystem>();
                }

                var canvasGo = new GameObject("Canvas", typeof(RectTransform));
                canvasGo.transform.SetParent(root.transform, false);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<GraphicRaycaster>();
                var canvasRect = (RectTransform)canvasGo.transform;
                canvasRect.sizeDelta = new Vector2(800, 600);

                var buttonGo = new GameObject("TargetButton", typeof(RectTransform));
                buttonGo.transform.SetParent(canvasGo.transform, false);
                var buttonRect = (RectTransform)buttonGo.transform;
                buttonRect.sizeDelta = new Vector2(200, 80);
                buttonRect.anchoredPosition = Vector2.zero;
                buttonGo.AddComponent<Image>().color = Color.white;
                buttonGo.AddComponent<Button>();
                var probe = buttonGo.AddComponent<InputQaProbe>();

                Canvas.ForceUpdateCanvases();
                await NextEditorUpdates(3);

                var clickResponse = await InputTool.HandleCommand(new JObject
                {
                    ["action"] = "click",
                    ["path"] = HierarchyPath.Build(buttonGo.transform),
                    ["settle_frames"] = 0,
                    ["strict"] = true
                }) as SuccessResponse;
                allPassed &= ExpectTrue("click returns success", clickResponse != null);
                allPassed &= ExpectEqual("pointer down count", 1, probe.PointerDownCount);
                allPassed &= ExpectEqual("pointer up count", 1, probe.PointerUpCount);
                allPassed &= ExpectEqual("pointer click count", 1, probe.PointerClickCount);

                var submitResponse = await InputTool.HandleCommand(new JObject
                {
                    ["action"] = "submit",
                    ["path"] = HierarchyPath.Build(buttonGo.transform),
                    ["settle_frames"] = 0,
                    ["strict"] = true
                }) as SuccessResponse;
                allPassed &= ExpectTrue("submit returns success", submitResponse != null);
                allPassed &= ExpectEqual("submit count", 1, probe.SubmitCount);

                var scrollResponse = await InputTool.HandleCommand(new JObject
                {
                    ["action"] = "scroll",
                    ["path"] = HierarchyPath.Build(buttonGo.transform),
                    ["scroll_delta"] = "0,-3",
                    ["settle_frames"] = 0,
                    ["strict"] = true
                }) as SuccessResponse;
                allPassed &= ExpectTrue("scroll returns success", scrollResponse != null);
                allPassed &= ExpectEqual("scroll count", 1, probe.ScrollCount);

                var dragResponse = await InputTool.HandleCommand(new JObject
                {
                    ["action"] = "drag",
                    ["path"] = HierarchyPath.Build(buttonGo.transform),
                    ["to_normalized"] = "0.75,0.5",
                    ["steps"] = 2,
                    ["settle_frames"] = 0,
                    ["strict"] = true
                }) as SuccessResponse;
                allPassed &= ExpectTrue("drag returns success", dragResponse != null);
                allPassed &= ExpectEqual("begin drag count", 1, probe.BeginDragCount);
                allPassed &= ExpectEqual("drag count", 2, probe.DragCount);
                allPassed &= ExpectEqual("end drag count", 1, probe.EndDragCount);

                var blockerGo = new GameObject("Blocker", typeof(RectTransform));
                blockerGo.transform.SetParent(canvasGo.transform, false);
                var blockerRect = (RectTransform)blockerGo.transform;
                blockerRect.sizeDelta = new Vector2(240, 120);
                blockerRect.anchoredPosition = Vector2.zero;
                blockerGo.AddComponent<Image>().color = Color.black;
                blockerGo.transform.SetAsLastSibling();

                Canvas.ForceUpdateCanvases();
                await NextEditorUpdates(3);

                var blockedResponse = await InputTool.HandleCommand(new JObject
                {
                    ["action"] = "click",
                    ["path"] = HierarchyPath.Build(buttonGo.transform),
                    ["settle_frames"] = 0,
                    ["strict"] = true
                }) as ErrorResponse;
                allPassed &= ExpectTrue("blocked click returns error", blockedResponse != null);
                allPassed &= ExpectEqual("blocked click code", "INPUT_TARGET_BLOCKED", blockedResponse?.code);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                allPassed = false;
            }
            finally
            {
                Object.Destroy(root);
            }

            if (allPassed)
                Debug.Log("[InputQaTests] ALL PASSED");
            else
                Debug.LogError("[InputQaTests] SOME TESTS FAILED");
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

        private static bool ExpectEqual<T>(string label, T expected, T actual)
        {
            if (Equals(expected, actual))
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError($"[FAIL] {label}: expected '{expected}', got '{actual}'");
            return false;
        }

        private static async Task NextEditorUpdates(int count)
        {
            while (count-- > 0)
                await NextEditorUpdate();
        }

        private static Task NextEditorUpdate()
        {
            var source = new TaskCompletionSource<bool>();
            void Tick()
            {
                EditorApplication.update -= Tick;
                source.TrySetResult(true);
            }
            EditorApplication.update += Tick;
            return source.Task;
        }
    }

    public sealed class InputQaProbe : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        ISubmitHandler, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int PointerDownCount { get; private set; }
        public int PointerUpCount { get; private set; }
        public int PointerClickCount { get; private set; }
        public int SubmitCount { get; private set; }
        public int ScrollCount { get; private set; }
        public int BeginDragCount { get; private set; }
        public int DragCount { get; private set; }
        public int EndDragCount { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            PointerDownCount++;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            PointerUpCount++;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PointerClickCount++;
        }

        public void OnSubmit(BaseEventData eventData)
        {
            SubmitCount++;
        }

        public void OnScroll(PointerEventData eventData)
        {
            ScrollCount++;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            BeginDragCount++;
        }

        public void OnDrag(PointerEventData eventData)
        {
            DragCount++;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EndDragCount++;
        }
    }
}
