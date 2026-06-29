using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HeraAgent.Tools
{
    public static partial class EditorScreenshot
    {
        private const int IsolatedLayer = 31;

        private struct AngleSpec
        {
            public string Name;
            public Vector3 Forward;
            public Vector3 Up;

            public AngleSpec(string name, Vector3 forward, Vector3 up)
            {
                Name = name;
                Forward = forward.normalized;
                Up = up.normalized;
            }
        }

        private static object CaptureIsolated(ToolParams p, int width, int height, string outputPath)
        {
            if (width <= 0 || height <= 0)
                return new ErrorResponse("INVALID_PARAM", "'width' and 'height' must be positive.");

            var (target, targetError) = TargetResolver.ResolveGameObject(p, "target");
            if (targetError != null)
                return targetError;

            if (!TryParseAngles(p.Get("angles", "iso"), out var angles, out var angleError))
                return new ErrorResponse("INVALID_PARAM", angleError);

            if (!TryParseBackground(p.Get("background", "#2B2B2BFF"), out var background, out var colorError))
                return new ErrorResponse("INVALID_PARAM", colorError);

            var padding = Mathf.Clamp(p.GetFloat("padding", 0.15f).Value, 0f, 2f);
            var previousActive = RenderTexture.active;
            var tiles = new List<Texture2D>();
            GameObject clone = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            Texture2D sheet = null;

            try
            {
                clone = UnityEngine.Object.Instantiate(target);
                clone.name = target.name + "_HeraScreenshotClone";
                clone.hideFlags = HideFlags.HideAndDontSave;
                clone.transform.position = Vector3.zero;
                SetActiveRecursively(clone, true);
                SetLayerRecursively(clone, IsolatedLayer);

                var bounds = CalculateBounds(clone);
                cameraObject = new GameObject("Hera Isolated Screenshot Camera");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
                camera.cullingMask = 1 << IsolatedLayer;
                camera.orthographic = true;

                lightObject = new GameObject("Hera Isolated Screenshot Light");
                lightObject.hideFlags = HideFlags.HideAndDontSave;
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;

                foreach (var angle in angles)
                    tiles.Add(RenderAngle(camera, lightObject.transform, bounds, angle, width, height, padding));

                sheet = BuildSheet(tiles, width, height, background, out var sheetWidth, out var sheetHeight);
                File.WriteAllBytes(outputPath, sheet.EncodeToPNG());

                return new SuccessResponse($"Isolated screenshot saved to {outputPath}", new
                {
                    path = outputPath,
                    target = target.name,
                    instance_id = EntityIdCompat.IdOf(target),
                    hierarchy_path = HierarchyPath.Build(target.transform),
                    angles = angles.ConvertAll(a => a.Name).ToArray(),
                    width = sheetWidth,
                    height = sheetHeight,
                    cell_width = width,
                    cell_height = height,
                });
            }
            finally
            {
                RenderTexture.active = previousActive;
                foreach (var tile in tiles)
                    if (tile) UnityEngine.Object.DestroyImmediate(tile);
                if (sheet) UnityEngine.Object.DestroyImmediate(sheet);
                if (lightObject) UnityEngine.Object.DestroyImmediate(lightObject);
                if (cameraObject) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (clone) UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        private static Texture2D RenderAngle(
            Camera camera,
            Transform lightTransform,
            Bounds bounds,
            AngleSpec angle,
            int width,
            int height,
            float padding)
        {
            RenderTexture rt = null;
            Texture2D texture = null;

            try
            {
                var radius = Mathf.Max(0.5f, bounds.extents.magnitude) * (1f + padding);
                var distance = radius * 3f + 1f;
                camera.transform.position = bounds.center - angle.Forward * distance;
                camera.transform.rotation = Quaternion.LookRotation(angle.Forward, angle.Up);
                camera.orthographicSize = radius;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = distance + radius * 4f;
                lightTransform.rotation = Quaternion.LookRotation(angle.Forward, angle.Up);

                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                if (rt) UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static Texture2D BuildSheet(
            List<Texture2D> tiles,
            int cellWidth,
            int cellHeight,
            Color background,
            out int sheetWidth,
            out int sheetHeight)
        {
            var columns = Mathf.CeilToInt(Mathf.Sqrt(tiles.Count));
            var rows = Mathf.CeilToInt(tiles.Count / (float)columns);
            sheetWidth = cellWidth * columns;
            sheetHeight = cellHeight * rows;

            var sheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGBA32, false);
            var fill = new Color[sheetWidth * sheetHeight];
            for (int i = 0; i < fill.Length; i++)
                fill[i] = background;
            sheet.SetPixels(fill);

            for (int i = 0; i < tiles.Count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var x = col * cellWidth;
                var y = (rows - 1 - row) * cellHeight;
                sheet.SetPixels(x, y, cellWidth, cellHeight, tiles[i].GetPixels());
            }

            sheet.Apply();
            return sheet;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var hasBounds = false;
            var bounds = new Bounds(root.transform.position, Vector3.one);

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled)
                    continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds)
                return bounds;

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled)
                    continue;
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return bounds;
        }

        private static bool TryParseAngles(string raw, out List<AngleSpec> angles, out string error)
        {
            angles = new List<AngleSpec>();
            error = null;
            foreach (var token in (raw ?? "iso").Split(','))
            {
                var name = token.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(name))
                    continue;

                switch (name)
                {
                    case "iso":
                        angles.Add(new AngleSpec("iso", new Vector3(1f, -0.75f, 1f), Vector3.up));
                        break;
                    case "front":
                        angles.Add(new AngleSpec("front", Vector3.forward, Vector3.up));
                        break;
                    case "back":
                        angles.Add(new AngleSpec("back", Vector3.back, Vector3.up));
                        break;
                    case "left":
                        angles.Add(new AngleSpec("left", Vector3.left, Vector3.up));
                        break;
                    case "right":
                        angles.Add(new AngleSpec("right", Vector3.right, Vector3.up));
                        break;
                    case "top":
                        angles.Add(new AngleSpec("top", Vector3.down, Vector3.forward));
                        break;
                    case "bottom":
                        angles.Add(new AngleSpec("bottom", Vector3.up, Vector3.back));
                        break;
                    default:
                        error = $"Unknown angle '{name}'. Valid: iso, front, back, left, right, top, bottom.";
                        return false;
                }
            }

            if (angles.Count > 0)
                return true;

            error = "'angles' must include at least one angle.";
            return false;
        }

        private static bool TryParseBackground(string raw, out Color color, out string error)
        {
            error = null;
            if (string.Equals(raw, "transparent", StringComparison.OrdinalIgnoreCase))
            {
                color = new Color(0f, 0f, 0f, 0f);
                return true;
            }

            if (ColorUtility.TryParseHtmlString(raw, out color))
                return true;

            error = $"'background' must be #RRGGBB, #RRGGBBAA, or transparent (got '{raw}').";
            return false;
        }

        private static void SetActiveRecursively(GameObject root, bool active)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                transform.gameObject.SetActive(active);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                transform.gameObject.layer = layer;
        }
    }
}
