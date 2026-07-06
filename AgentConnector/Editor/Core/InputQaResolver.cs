using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeraAgent
{
    internal static class InputQaResolver
    {
        public static (InputQaOptions options, ErrorResponse err) Parse(JObject raw)
        {
            var p = new ToolParams(raw);
            string action = p.Get("action") ?? Arg(raw, 0);
            if (string.IsNullOrEmpty(action))
                return (null, new ErrorResponse("INPUT_MISSING_ACTION", "Input action required: state, inspect, click, submit, scroll, drag, pointer_down, or pointer_up."));

            var options = new InputQaOptions
            {
                Action = action.ToLowerInvariant(),
                Backend = (p.Get("backend", "eventsystem") ?? "eventsystem").ToLowerInvariant(),
                ClickCount = p.GetInt("click_count", 1) ?? 1,
                HoldMs = p.GetInt("hold_ms", 50) ?? 50,
                SettleFrames = p.GetInt("settle_frames", 1) ?? 1,
                Steps = p.GetInt("steps", 8) ?? 8,
                Strict = p.GetBool("strict", true),
                Details = p.GetBool("details", false),
                Button = ParseButton(p.Get("button", "left")),
            };

            var (position, posErr) = ParseVector(p.Get("position"), "position");
            if (posErr != null) return (null, posErr);
            var (normalized, normErr) = ParseVector(p.Get("normalized"), "normalized");
            if (normErr != null) return (null, normErr);
            var (offset, offsetErr) = ParseVector(p.Get("offset"), "offset");
            if (offsetErr != null) return (null, offsetErr);
            var (scrollDelta, scrollErr) = ParseVector(p.Get("scroll_delta") ?? p.Get("delta"), "scroll_delta");
            if (scrollErr != null) return (null, scrollErr);
            var (toPosition, toPosErr) = ParseVector(p.Get("to_position") ?? p.Get("to"), "to_position");
            if (toPosErr != null) return (null, toPosErr);
            var (toNormalized, toNormErr) = ParseVector(p.Get("to_normalized"), "to_normalized");
            if (toNormErr != null) return (null, toNormErr);
            options.Position = position;
            options.Normalized = normalized;
            options.Offset = offset;
            options.ScrollDelta = scrollDelta;
            options.ToPosition = toPosition;
            options.ToNormalized = toNormalized;

            if (options.Action != "state")
            {
                var (target, targetErr) = ResolveTarget(raw);
                if (targetErr != null) return (null, targetErr);
                options.Target = target;
            }

            return (options, null);
        }

        public static (EventSystem eventSystem, ErrorResponse err) ResolveEventSystem()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
#if UNITY_6000_5_OR_NEWER
                eventSystem = Object.FindAnyObjectByType<EventSystem>();
#elif UNITY_2023_1_OR_NEWER
                eventSystem = Object.FindFirstObjectByType<EventSystem>();
#else
                eventSystem = Object.FindObjectOfType<EventSystem>();
#endif
            }

            if (eventSystem == null)
                return (null, new ErrorResponse("INPUT_NO_EVENT_SYSTEM", "No active EventSystem found."));
            return (eventSystem, null);
        }

        public static (Vector2 point, ErrorResponse err) ResolvePoint(InputQaOptions options)
        {
            if (options.Position.HasValue) return (options.Position.Value, null);
            var rect = options.Target.GetComponent<RectTransform>();
            if (rect == null)
                return (default, new ErrorResponse("INPUT_TARGET_NOT_UI", "EventSystem input requires a RectTransform target."));

            var canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            Vector3 worldPoint;
            if (options.Normalized.HasValue)
            {
                var n = options.Normalized.Value;
                var local = new Vector2(
                    Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, n.x),
                    Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, n.y));
                worldPoint = rect.TransformPoint(local);
            }
            else
            {
                var local = rect.rect.center + (options.Offset ?? Vector2.zero);
                worldPoint = rect.TransformPoint(local);
            }

            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                canvas.transform is RectTransform canvasRect &&
                canvasRect.rect.width > 0f && canvasRect.rect.height > 0f)
            {
                var local = (Vector2)canvasRect.InverseTransformPoint(worldPoint);
                var canvasLocal = canvasRect.rect;
                var pixelRect = canvas.pixelRect;
                var x = pixelRect.x + ((local.x - canvasLocal.xMin) / canvasLocal.width) * pixelRect.width;
                var y = pixelRect.y + ((local.y - canvasLocal.yMin) / canvasLocal.height) * pixelRect.height;
                return (new Vector2(x, y), null);
            }

            return (RectTransformUtility.WorldToScreenPoint(camera, worldPoint), null);
        }

        public static (Vector2 point, ErrorResponse err) ResolveDragEndPoint(InputQaOptions options)
        {
            if (options.ToPosition.HasValue) return (options.ToPosition.Value, null);
            if (!options.ToNormalized.HasValue) return (options.Position ?? default, null);

            var original = options.Normalized;
            options.Normalized = options.ToNormalized;
            var result = ResolvePoint(options);
            options.Normalized = original;
            return result;
        }

        public static object State()
        {
            var (eventSystem, _) = ResolveEventSystem();
            var raycasters = new List<object>();
#if UNITY_6000_5_OR_NEWER
            var allRaycasters = Object.FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None);
#elif UNITY_2023_1_OR_NEWER
            var allRaycasters = Object.FindObjectsOfType<BaseRaycaster>();
#else
            var allRaycasters = Object.FindObjectsOfType<BaseRaycaster>();
#endif
            foreach (var raycaster in allRaycasters)
            {
                if (raycaster == null) continue;
                raycasters.Add(new
                {
                    instance_id = EntityIdCompat.IdOf(raycaster),
                    path = HierarchyPath.Build(raycaster.transform),
                    type = raycaster.GetType().Name,
                    active = raycaster.isActiveAndEnabled
                });
            }

            return new
            {
                backend = "eventsystem",
                evidence_level = "eventsystem",
                event_system = EventSystemShape(eventSystem),
                raycasters,
                inputsystem = new { available = System.Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem") != null },
                native_win32 = new { available = Application.platform == RuntimePlatform.WindowsEditor, implemented = false }
            };
        }

        public static object TargetShape(GameObject go)
        {
            if (go == null) return null;
            return new
            {
                instance_id = EntityIdCompat.IdOf(go),
                name = go.name,
                path = HierarchyPath.Build(go.transform),
                active = go.activeInHierarchy
            };
        }

        public static object EventSystemShape(EventSystem eventSystem)
        {
            if (eventSystem == null) return new { present = false };
            return new
            {
                present = true,
                instance_id = EntityIdCompat.IdOf(eventSystem),
                path = HierarchyPath.Build(eventSystem.transform),
                input_module = eventSystem.currentInputModule == null ? null : eventSystem.currentInputModule.GetType().Name
            };
        }

        private static (GameObject target, ErrorResponse err) ResolveTarget(JObject raw)
        {
            var shaped = new JObject(raw ?? new JObject());
            var target = shaped["target"]?.ToString();
            if (!string.IsNullOrEmpty(target) && shaped["instance_id"] == null && shaped["path"] == null)
            {
                if (int.TryParse(target, out var id)) shaped["instance_id"] = id;
                else shaped["path"] = target;
            }

            var (go, err) = TargetResolver.ResolveGameObject(new ToolParams(shaped), "target");
            if (err == null) return (go, null);
            switch (err.code)
            {
                case "MISSING_TARGET":
                    return (null, new ErrorResponse("INPUT_MISSING_TARGET", err.message));
                case "TARGET_NOT_FOUND":
                case "OBJECT_NOT_FOUND":
                    return (null, new ErrorResponse("INPUT_TARGET_NOT_FOUND", err.message));
                default:
                    return (null, err);
            }
        }

        private static PointerEventData.InputButton ParseButton(string value)
        {
            switch ((value ?? "left").ToLowerInvariant())
            {
                case "right": return PointerEventData.InputButton.Right;
                case "middle": return PointerEventData.InputButton.Middle;
                default: return PointerEventData.InputButton.Left;
            }
        }

        private static (Vector2? value, ErrorResponse err) ParseVector(string raw, string name)
        {
            if (string.IsNullOrEmpty(raw)) return (null, null);
            var parts = raw.Split(',');
            if (parts.Length != 2 ||
                !float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
                return (null, new ErrorResponse("INPUT_INVALID_VECTOR", $"Invalid '{name}' vector. Expected 'x,y'."));
            return (new Vector2(x, y), null);
        }

        private static string Arg(JObject raw, int index)
        {
            var args = raw?["args"] as JArray;
            return args != null && args.Count > index ? args[index]?.ToString() : null;
        }
    }
}
