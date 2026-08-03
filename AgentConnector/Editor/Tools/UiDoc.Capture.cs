using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeraAgent.Tools
{
    public static partial class UiDoc
    {
        static object Capture(JObject raw)
        {
            var p = new ToolParams(raw);

#if UNITY_6000_5_OR_NEWER
            var all = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
#else
            var all = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#endif
            Canvas only = null;
            var canvasSelection = p.Get("canvas");
            if (!string.IsNullOrEmpty(canvasSelection))
            {
                var (target, error) = TargetResolver.ResolveTransform(canvasSelection);
                if (error != null) return error;
                var canvas = target.GetComponentInParent<Canvas>();
                only = canvas != null ? canvas.rootCanvas : null;
                if (only == null)
                    return new ErrorResponse("TARGET_NOT_FOUND", $"[Hera] I found '{canvasSelection}' but it isn't under a Canvas.");
            }

            var targets = new List<Canvas>();
            foreach (var canvas in all)
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                if (only != null && canvas != only) continue;
                targets.Add(canvas);
            }

            int width = p.GetInt("width", 0) ?? 0;
            int height = p.GetInt("height", 0) ?? 0;
            if (targets.Count > 0)
            {
                var pixelRect = targets[0].pixelRect;
                if (width <= 0) width = Mathf.RoundToInt(pixelRect.width);
                if (height <= 0) height = Mathf.RoundToInt(pixelRect.height);
            }
            if (width <= 0) width = 1920;
            if (height <= 0) height = 1080;

            var background = new Color(0.10f, 0.10f, 0.12f, 1f);
            var backgroundValue = p.Get("bg");
            if (!string.IsNullOrEmpty(backgroundValue)
                && SerializedPropertyValue.TryParseColor(new JValue(backgroundValue), out var parsed, out _))
                background = parsed;

            var overwrite = p.GetBool("overwrite");
            var defaultPath = Path.Combine(Path.GetTempPath(), "hera_ui_capture-" + System.Guid.NewGuid().ToString("N") + ".png");
            if (!OutputFilePolicy.TryResolvePng(
                p.Get("out"), defaultPath, overwrite, out var outputPath, out var pathErrorCode, out var pathError))
                return new ErrorResponse(pathErrorCode, pathError);

            var saved = new (Canvas canvas, RenderMode mode, Camera camera, float planeDistance)[targets.Count];
            GameObject cameraObject = null;
            RenderTexture renderTexture = null;
            Texture2D texture = null;
            try
            {
                cameraObject = new GameObject("HeraShotCam") { hideFlags = HideFlags.HideAndDontSave };
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
                camera.orthographic = true;
                cameraObject.transform.position = new Vector3(0, 0, -100);

                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = renderTexture;
                for (int i = 0; i < targets.Count; i++)
                {
                    var canvas = targets[i];
                    saved[i] = (canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance);
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 50f;
                }

                Canvas.ForceUpdateCanvases();
                camera.Render();

                var previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previousActive;

                var bytes = texture.EncodeToPNG();
                OutputFilePolicy.WriteAllBytes(outputPath, bytes, overwrite);
                return new SuccessResponse($"Captured {targets.Count} canvas(es) -> {outputPath}", new
                {
                    path = outputPath,
                    width,
                    height,
                    bytes = bytes.Length,
                    canvases = targets.Count,
                });
            }
            catch (System.Exception exception)
            {
                return new ErrorResponse("CAPTURE_FAILED", $"[Hera] I couldn't capture the UI: {exception.Message}");
            }
            finally
            {
                foreach (var state in saved)
                {
                    if (state.canvas == null) continue;
                    state.canvas.renderMode = state.mode;
                    state.canvas.worldCamera = state.camera;
                    state.canvas.planeDistance = state.planeDistance;
                }
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                if (renderTexture != null) { renderTexture.Release(); Object.DestroyImmediate(renderTexture); }
                if (texture != null) Object.DestroyImmediate(texture);
            }
        }
    }
}
