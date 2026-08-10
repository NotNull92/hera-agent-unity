using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HeraAgent.Tools
{
    public static partial class EditorScreenshot
    {
        private const int DefaultMaxAnnotations = 32;
        private const int MaxUiAnnotations = 100;
        private const int AnnotationRaycastResults = 8;

        internal sealed class ScreenshotUiAnnotationCollection
        {
            public readonly List<ScreenshotUiElementAnnotation> elements =
                new List<ScreenshotUiElementAnnotation>();
            public int total;
            public int skipped;
            public bool truncated;
            public int max_annotations;
            public int game_view_width;
            public int game_view_height;
        }

        internal sealed class ScreenshotUiElementAnnotation
        {
            public int instance_id;
            public string hierarchy_path;
            public string name;
            public string type;
            public bool interactable;
            public string not_interactable_reason;
            public object blocked_by;
            public bool target_hit;
            public bool target_top_hit;
            public float[] input_point;
            public float[] image_point;
            public float[] input_bounds;
            public float[] image_bounds;
        }

        private static (int value, ErrorResponse error) ParseMaxAnnotations(ToolParams parameters)
        {
            var raw = parameters.GetRaw("max_annotations");
            if (raw == null || raw.Type == JTokenType.Null)
                return (DefaultMaxAnnotations, null);
            if (!int.TryParse(raw.ToString(), out var value)
                || value < 1 || value > MaxUiAnnotations)
            {
                return (0, new ErrorResponse(
                    "SCREENSHOT_INVALID_MAX_ANNOTATIONS",
                    $"[Hera] I need max_annotations from 1 through {MaxUiAnnotations}."));
            }
            return (value, null);
        }

        internal static (
            ScreenshotUiAnnotationCollection annotations,
            ErrorResponse error) CollectUiAnnotations(int maxAnnotations)
        {
            var (_, eventSystemError) = InputQaResolver.ResolveEventSystem();
            if (eventSystemError != null)
            {
                return (null, new ErrorResponse(
                    "SCREENSHOT_UI_EVENT_SYSTEM_UNAVAILABLE",
                    "[Hera] I need an active EventSystem to produce UI annotations.",
                    new { cause_code = eventSystemError.code }));
            }

            Canvas.ForceUpdateCanvases();
            var candidates = Selectable.allSelectablesArray
                .Where(selectable => selectable != null
                    && selectable.gameObject.activeInHierarchy
                    && selectable.GetComponent<RectTransform>() != null)
                .Select(selectable => new
                {
                    selectable,
                    path = HierarchyPath.Build(selectable.transform),
                })
                .OrderBy(candidate => candidate.path, StringComparer.Ordinal)
                .ThenBy(candidate => EntityIdCompat.IdOf(candidate.selectable), Comparer<int>.Default)
                .ToList();

            var gameViewWidth = Mathf.Max(1, Screen.width);
            var gameViewHeight = Mathf.Max(1, Screen.height);
            var result = new ScreenshotUiAnnotationCollection
            {
                total = candidates.Count,
                truncated = candidates.Count > maxAnnotations,
                max_annotations = maxAnnotations,
                game_view_width = gameViewWidth,
                game_view_height = gameViewHeight,
            };

            foreach (var candidate in candidates.Take(maxAnnotations))
            {
                var options = new InputQaOptions
                {
                    Action = "inspect",
                    Backend = "eventsystem",
                    Target = candidate.selectable.gameObject,
                    Button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                    ClickCount = 1,
                    MaxResults = AnnotationRaycastResults,
                    Strict = false,
                };
                var (inspection, inspectionError) = InputQaEventSystem.BuildInspection(options);
                var (bounds, boundsError) = InputQaResolver.ResolveScreenBounds(options.Target);
                if (inspectionError != null || boundsError != null)
                {
                    result.skipped++;
                    continue;
                }

                result.elements.Add(new ScreenshotUiElementAnnotation
                {
                    instance_id = inspection.TargetId,
                    hierarchy_path = inspection.TargetPath,
                    name = inspection.TargetName,
                    type = candidate.selectable.GetType().Name,
                    interactable = inspection.Interactable,
                    not_interactable_reason = inspection.NotInteractableReason,
                    blocked_by = inspection.BlockedBy == null
                        ? null
                        : new
                        {
                            instance_id = EntityIdCompat.IdOf(inspection.BlockedBy),
                            hierarchy_path = inspection.BlockedByPath,
                        },
                    target_hit = inspection.TargetHit,
                    target_top_hit = inspection.TargetTopHit,
                    input_point = Point(inspection.Point),
                    image_point = ImagePoint(inspection.Point, gameViewHeight),
                    input_bounds = Bounds(bounds),
                    image_bounds = ImageBounds(bounds, gameViewHeight),
                });
            }
            return (result, null);
        }

        private static object AttachUiAnnotations(
            object response,
            ScreenshotUiAnnotationCollection annotations,
            int capturedWidth,
            int capturedHeight)
        {
            if (!(response is SuccessResponse success)) return response;
            var data = success.data == null
                ? new JObject()
                : JObject.FromObject(success.data);
            var pixelsRequested = capturedWidth > 0 && capturedHeight > 0;
            data["pixels_requested"] = pixelsRequested;
            data["ui_annotations"] = JArray.FromObject(annotations.elements);
            data["ui_annotation_count"] = annotations.elements.Count;
            data["ui_annotations_total"] = annotations.total;
            data["ui_annotations_skipped"] = annotations.skipped;
            data["ui_annotations_truncated"] = annotations.truncated;
            data["ui_annotations_limit"] = annotations.max_annotations;
            data["coordinate_spaces"] = CreateCoordinateSpaces(
                annotations.game_view_width,
                annotations.game_view_height,
                capturedWidth,
                capturedHeight);
            success.data = data;
            return success;
        }

        private static float[] Point(Vector2 point) => new[] { point.x, point.y };

        private static float[] ImagePoint(Vector2 point, int height) =>
            new[] { point.x, height - point.y };

        private static float[] Bounds(Rect bounds) => new[]
        {
            bounds.xMin,
            bounds.yMin,
            bounds.xMax,
            bounds.yMax,
        };

        private static float[] ImageBounds(Rect bounds, int height) => new[]
        {
            bounds.xMin,
            height - bounds.yMax,
            bounds.xMax,
            height - bounds.yMin,
        };
    }
}
