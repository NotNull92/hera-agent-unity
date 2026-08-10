using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HeraAgent.Tools
{
    public static partial class EditorScreenshot
    {
        private const int DefaultPhysicsGridSize = 9;
        private const int MaxPhysicsGridSize = 16;
        private const int DefaultMaxPhysicsHits = 32;
        private const int MaxPhysicsHits = 100;
        private const float MaxPhysicsDistance = 100000f;

        internal sealed class ScreenshotPhysicsAnnotationCollection
        {
            public readonly List<ScreenshotPhysicsAnnotation> elements =
                new List<ScreenshotPhysicsAnnotation>();
            public int total;
            public bool truncated;
            public int max_results;
            public int grid_size;
            public int rays_cast;
            public int rays_hit;
            public float max_distance;
            public string query_triggers;
            public int requested_layer_mask;
            public int camera_culling_mask;
            public int effective_layer_mask;
            public int game_view_width;
            public int game_view_height;
            public int camera_instance_id;
            public string camera_hierarchy_path;
        }

        internal sealed class ScreenshotPhysicsAnnotation
        {
            public int instance_id;
            public string hierarchy_path;
            public string name;
            public int collider_instance_id;
            public string collider_type;
            public int layer;
            public string layer_name;
            public int sample_count;
            public float distance;
            public float[] world_point;
            public float[] world_normal;
            public float[] input_point;
            public float[] image_point;
            public float[] input_bounds;
            public float[] image_bounds;
        }

        private sealed class PhysicsSample
        {
            public RaycastHit hit;
            public Vector2 inputPoint;
        }

        private sealed class PhysicsCluster
        {
            public Collider collider;
            public readonly List<PhysicsSample> samples = new List<PhysicsSample>();
        }

        internal static (
            ScreenshotPhysicsAnnotationCollection annotations,
            ErrorResponse error) CollectPhysicsAnnotations(ToolParams parameters)
        {
            var camera = Camera.main;
            if (!camera || !camera.enabled || !camera.gameObject.activeInHierarchy)
            {
                return (null, new ErrorResponse(
                    "SCREENSHOT_PHYSICS_CAMERA_UNAVAILABLE",
                    "[Hera] I need an active Camera tagged MainCamera to collect 3D physics evidence."));
            }

            var (gridSize, gridError) = ParseBoundedInt(
                parameters,
                "physics_grid_size",
                DefaultPhysicsGridSize,
                MaxPhysicsGridSize,
                "SCREENSHOT_INVALID_PHYSICS_GRID_SIZE");
            if (gridError != null) return (null, gridError);
            var (maxResults, maxResultsError) = ParseBoundedInt(
                parameters,
                "max_physics_hits",
                DefaultMaxPhysicsHits,
                MaxPhysicsHits,
                "SCREENSHOT_INVALID_MAX_PHYSICS_HITS");
            if (maxResultsError != null) return (null, maxResultsError);

            var (maxDistance, distanceError) = ParsePhysicsMaxDistance(parameters, camera);
            if (distanceError != null) return (null, distanceError);
            var (requestedMask, maskError) = ParsePhysicsLayerMask(parameters, camera.cullingMask);
            if (maskError != null) return (null, maskError);
            var effectiveMask = requestedMask & camera.cullingMask;
            if (effectiveMask == 0)
            {
                return (null, new ErrorResponse(
                    "SCREENSHOT_PHYSICS_LAYER_MASK_EMPTY",
                    "[Hera] The requested physics_layer_mask has no layers visible through Camera.main.cullingMask."));
            }
            var (queryTriggers, queryName, queryError) = ParseQueryTriggers(parameters);
            if (queryError != null) return (null, queryError);

            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            var clusters = new Dictionary<int, PhysicsCluster>();
            var raysHit = 0;
            Physics.SyncTransforms();

            for (var row = 0; row < gridSize; row++)
            {
                for (var column = 0; column < gridSize; column++)
                {
                    var inputPoint = new Vector2(
                        (column + 0.5f) * width / gridSize,
                        (row + 0.5f) * height / gridSize);
                    var ray = camera.ScreenPointToRay(inputPoint);
                    if (!Physics.Raycast(
                        ray,
                        out var hit,
                        maxDistance,
                        effectiveMask,
                        queryTriggers))
                    {
                        continue;
                    }

                    raysHit++;
                    var colliderId = EntityIdCompat.IdOf(hit.collider);
                    if (!clusters.TryGetValue(colliderId, out var cluster))
                    {
                        cluster = new PhysicsCluster { collider = hit.collider };
                        clusters.Add(colliderId, cluster);
                    }
                    cluster.samples.Add(new PhysicsSample
                    {
                        hit = hit,
                        inputPoint = inputPoint,
                    });
                }
            }

            var ordered = clusters.Values
                .OrderByDescending(cluster => cluster.samples.Count)
                .ThenBy(cluster => HierarchyPath.Build(cluster.collider.transform), StringComparer.Ordinal)
                .ThenBy(cluster => EntityIdCompat.IdOf(cluster.collider), Comparer<int>.Default)
                .ToList();
            var result = new ScreenshotPhysicsAnnotationCollection
            {
                total = ordered.Count,
                truncated = ordered.Count > maxResults,
                max_results = maxResults,
                grid_size = gridSize,
                rays_cast = gridSize * gridSize,
                rays_hit = raysHit,
                max_distance = maxDistance,
                query_triggers = queryName,
                requested_layer_mask = requestedMask,
                camera_culling_mask = camera.cullingMask,
                effective_layer_mask = effectiveMask,
                game_view_width = width,
                game_view_height = height,
                camera_instance_id = EntityIdCompat.IdOf(camera.gameObject),
                camera_hierarchy_path = HierarchyPath.Build(camera.transform),
            };

            foreach (var cluster in ordered.Take(maxResults))
                result.elements.Add(CreatePhysicsAnnotation(cluster, gridSize, width, height));
            return (result, null);
        }

        private static ScreenshotPhysicsAnnotation CreatePhysicsAnnotation(
            PhysicsCluster cluster,
            int gridSize,
            int width,
            int height)
        {
            var centroid = new Vector2(
                cluster.samples.Average(sample => sample.inputPoint.x),
                cluster.samples.Average(sample => sample.inputPoint.y));
            var representative = cluster.samples
                .OrderBy(sample => (sample.inputPoint - centroid).sqrMagnitude)
                .ThenBy(sample => sample.hit.distance)
                .First();
            var halfCellWidth = width / (gridSize * 2f);
            var halfCellHeight = height / (gridSize * 2f);
            var minX = Mathf.Clamp(cluster.samples.Min(sample => sample.inputPoint.x) - halfCellWidth, 0f, width);
            var minY = Mathf.Clamp(cluster.samples.Min(sample => sample.inputPoint.y) - halfCellHeight, 0f, height);
            var maxX = Mathf.Clamp(cluster.samples.Max(sample => sample.inputPoint.x) + halfCellWidth, 0f, width);
            var maxY = Mathf.Clamp(cluster.samples.Max(sample => sample.inputPoint.y) + halfCellHeight, 0f, height);
            var gameObject = cluster.collider.gameObject;

            return new ScreenshotPhysicsAnnotation
            {
                instance_id = EntityIdCompat.IdOf(gameObject),
                hierarchy_path = HierarchyPath.Build(gameObject.transform),
                name = gameObject.name,
                collider_instance_id = EntityIdCompat.IdOf(cluster.collider),
                collider_type = cluster.collider.GetType().Name,
                layer = gameObject.layer,
                layer_name = LayerMask.LayerToName(gameObject.layer),
                sample_count = cluster.samples.Count,
                distance = representative.hit.distance,
                world_point = Vector3Values(representative.hit.point),
                world_normal = Vector3Values(representative.hit.normal),
                input_point = Point(representative.inputPoint),
                image_point = ImagePoint(representative.inputPoint, height),
                input_bounds = new[] { minX, minY, maxX, maxY },
                image_bounds = new[] { minX, height - maxY, maxX, height - minY },
            };
        }

        private static object AttachPhysicsAnnotations(
            object response,
            ScreenshotPhysicsAnnotationCollection annotations,
            int capturedWidth,
            int capturedHeight)
        {
            if (!(response is SuccessResponse success)) return response;
            var data = success.data == null
                ? new JObject()
                : JObject.FromObject(success.data);
            var pixelsRequested = capturedWidth > 0 && capturedHeight > 0;
            data["pixels_requested"] = pixelsRequested;
            data["physics_annotations"] = JArray.FromObject(annotations.elements);
            data["physics_annotation_count"] = annotations.elements.Count;
            data["physics_annotations_total"] = annotations.total;
            data["physics_annotations_truncated"] = annotations.truncated;
            data["physics_annotations_limit"] = annotations.max_results;
            data["physics_raycast"] = JObject.FromObject(new
            {
                camera = new
                {
                    instance_id = annotations.camera_instance_id,
                    hierarchy_path = annotations.camera_hierarchy_path,
                    culling_mask = annotations.camera_culling_mask,
                },
                grid_size = annotations.grid_size,
                rays_cast = annotations.rays_cast,
                rays_hit = annotations.rays_hit,
                max_distance = annotations.max_distance,
                query_triggers = annotations.query_triggers,
                requested_layer_mask = annotations.requested_layer_mask,
                effective_layer_mask = annotations.effective_layer_mask,
            });
            data["coordinate_spaces"] = CreateCoordinateSpaces(
                annotations.game_view_width,
                annotations.game_view_height,
                capturedWidth,
                capturedHeight);
            success.data = data;
            return success;
        }

        private static (int value, ErrorResponse error) ParseBoundedInt(
            ToolParams parameters,
            string key,
            int defaultValue,
            int maximum,
            string errorCode)
        {
            var raw = parameters.GetRaw(key);
            if (raw == null || raw.Type == JTokenType.Null) return (defaultValue, null);
            if (!int.TryParse(raw.ToString(), out var value) || value < 1 || value > maximum)
            {
                return (0, new ErrorResponse(
                    errorCode,
                    $"[Hera] I need {key} from 1 through {maximum}."));
            }
            return (value, null);
        }

        private static (float value, ErrorResponse error) ParsePhysicsMaxDistance(
            ToolParams parameters,
            Camera camera)
        {
            var raw = parameters.GetRaw("physics_max_distance");
            if (raw == null || raw.Type == JTokenType.Null)
                return (Mathf.Min(camera.farClipPlane, MaxPhysicsDistance), null);
            if (!float.TryParse(
                    raw.ToString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value)
                || float.IsNaN(value)
                || float.IsInfinity(value)
                || value <= 0f
                || value > MaxPhysicsDistance)
            {
                return (0f, new ErrorResponse(
                    "SCREENSHOT_INVALID_PHYSICS_MAX_DISTANCE",
                    $"[Hera] I need physics_max_distance greater than 0 and no more than {MaxPhysicsDistance}."));
            }
            return (value, null);
        }

        private static (int value, ErrorResponse error) ParsePhysicsLayerMask(
            ToolParams parameters,
            int defaultValue)
        {
            var raw = parameters.GetRaw("physics_layer_mask");
            if (raw == null || raw.Type == JTokenType.Null) return (defaultValue, null);
            if (!int.TryParse(raw.ToString(), out var value))
            {
                return (0, new ErrorResponse(
                    "SCREENSHOT_INVALID_PHYSICS_LAYER_MASK",
                    "[Hera] I need physics_layer_mask as a signed 32-bit integer."));
            }
            return (value, null);
        }

        private static (
            QueryTriggerInteraction value,
            string name,
            ErrorResponse error) ParseQueryTriggers(ToolParams parameters)
        {
            var name = parameters.Get("physics_query_triggers", "use_global").ToLowerInvariant();
            switch (name)
            {
                case "use_global":
                    return (QueryTriggerInteraction.UseGlobal, name, null);
                case "ignore":
                    return (QueryTriggerInteraction.Ignore, name, null);
                case "collide":
                    return (QueryTriggerInteraction.Collide, name, null);
                default:
                    return (QueryTriggerInteraction.UseGlobal, name, new ErrorResponse(
                        "SCREENSHOT_INVALID_PHYSICS_QUERY_TRIGGERS",
                        "[Hera] I need physics_query_triggers as use_global, ignore, or collide."));
            }
        }

        private static JObject CreateCoordinateSpaces(
            int gameViewWidth,
            int gameViewHeight,
            int capturedWidth,
            int capturedHeight)
        {
            var pixelsRequested = capturedWidth > 0 && capturedHeight > 0;
            return JObject.FromObject(new
            {
                input = new
                {
                    name = "unity_screen_bottom_left_pixels",
                    origin = "bottom_left",
                    y_axis = "up",
                    width = gameViewWidth,
                    height = gameViewHeight,
                },
                image = new
                {
                    name = "game_view_top_left_pixels",
                    origin = "top_left",
                    y_axis = "down",
                    width = gameViewWidth,
                    height = gameViewHeight,
                },
                captured_png = pixelsRequested
                    ? new
                    {
                        width = capturedWidth,
                        height = capturedHeight,
                        annotation_coordinates = "game_view_top_left_pixels",
                        includes_editor_window_chrome = true,
                    }
                    : null,
            });
        }

        private static float[] Vector3Values(Vector3 value) =>
            new[] { value.x, value.y, value.z };
    }
}
