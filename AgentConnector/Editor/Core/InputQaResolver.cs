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
        internal const int MaxHoldMs = 5000;
        internal const int MaxSettleFrames = 120;
        internal const int MaxSteps = 120;
        internal const int MaxClickCount = 3;
        internal const int MaxResults = 100;

        public static (InputQaOptions options, ErrorResponse err) Parse(JObject raw)
        {
            var p = new ToolParams(raw);
            string action = p.Get("action") ?? Arg(raw, 0);
            if (string.IsNullOrEmpty(action))
                return (null, new ErrorResponse("INPUT_MISSING_ACTION", "Input action required: state, inspect, click, submit, scroll, drag, pointer_down, or pointer_up."));

            var (clickCount, clickCountErr) = ParseBoundedInt(p, "click_count", 1, 1, MaxClickCount);
            if (clickCountErr != null) return (null, clickCountErr);
            var (holdMs, holdMsErr) = ParseBoundedInt(p, "hold_ms", 50, 0, MaxHoldMs);
            if (holdMsErr != null) return (null, holdMsErr);
            var (settleFrames, settleFramesErr) = ParseBoundedInt(p, "settle_frames", 1, 0, MaxSettleFrames);
            if (settleFramesErr != null) return (null, settleFramesErr);
            var (steps, stepsErr) = ParseBoundedInt(p, "steps", 8, 1, MaxSteps);
            if (stepsErr != null) return (null, stepsErr);
            var (maxResults, maxResultsErr) = ParseBoundedInt(p, "max_results", 50, 1, MaxResults);
            if (maxResultsErr != null) return (null, maxResultsErr);

            var options = new InputQaOptions
            {
                Action = action.ToLowerInvariant(),
                Backend = (p.Get("backend", "eventsystem") ?? "eventsystem").ToLowerInvariant(),
                ClickCount = clickCount,
                HoldMs = holdMs,
                SettleFrames = settleFrames,
                Steps = steps,
                MaxResults = maxResults,
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
                eventSystem = Object.FindFirstObjectByType<EventSystem>();

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

        public static object State(int maxResults)
        {
            var (eventSystem, _) = ResolveEventSystem();
            var raycasters = new List<object>();
            var allRaycasters = Object.FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None);
            foreach (var raycaster in allRaycasters)
            {
                if (raycaster == null) continue;
                if (raycasters.Count >= maxResults) continue;
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
                raycasters_total = allRaycasters.Length,
                raycasters_truncated = allRaycasters.Length > raycasters.Count,
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

        private static (int value, ErrorResponse err) ParseBoundedInt(ToolParams parameters, string name, int fallback, int min, int max)
        {
            var token = parameters.GetRaw(name);
            if (token == null || token.Type == JTokenType.Null) return (fallback, null);

            if (!int.TryParse(token.ToString(), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var value) || value < min || value > max)
                return (0, new ErrorResponse("INPUT_INVALID_PARAM", $"'{name}' must be an integer from {min} to {max}."));

            return (value, null);
        }

        private static string Arg(JObject raw, int index)
        {
            var args = raw?["args"] as JArray;
            return args != null && args.Count > index ? args[index]?.ToString() : null;
        }
    }
}
