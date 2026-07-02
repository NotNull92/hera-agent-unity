using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeraAgent.Tools
{
    [HeraTool(Name = "screenshot", Description = "Capture a screenshot of the Unity editor. Views: scene, game, or isolated target.")]
    public static partial class EditorScreenshot
    {
        private const int DefaultWidth = 1920;
        private const int DefaultHeight = 1080;

        public class Parameters
        {
            [ToolParameter("View to capture: scene (default), game", Required = false)]
            public string View { get; set; }

            [ToolParameter("Override width (default 1920)", Required = false)]
            public int Width { get; set; }

            [ToolParameter("Override height (default 1080)", Required = false)]
            public int Height { get; set; }

            [ToolParameter("Output file path, absolute or relative to project root (default: Screenshots/screenshot.png)", Required = false)]
            public string OutputPath { get; set; }

            [ToolParameter("Capture only one GameObject by --target, --path, or --instance_id.", Required = false)]
            public bool Isolated { get; set; }

            [ToolParameter("Hierarchy path for isolated capture (same as --path, e.g. /Player).", Required = false)]
            public string Target { get; set; }

            [ToolParameter("Hierarchy path for isolated capture (e.g. /Player).", Required = false)]
            public string Path { get; set; }

            [ToolParameter("InstanceID for isolated capture.", Required = false)]
            public int InstanceId { get; set; }

            [ToolParameter("Isolated capture angles: iso, front, back, left, right, top, bottom; comma-separated.", Required = false)]
            public string Angles { get; set; }

            [ToolParameter("Isolated background color: #RRGGBB, #RRGGBBAA, or transparent.", Required = false)]
            public string Background { get; set; }

            [ToolParameter("Isolated camera padding fraction (default 0.15).", Required = false)]
            public float Padding { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                @params = new JObject();

            var p = new ToolParams(@params);
            var view = p.Get("view", "scene").ToLowerInvariant();
            var width = p.GetInt("width", DefaultWidth).Value;
            var height = p.GetInt("height", DefaultHeight).Value;
            var outputPath = ResolveOutputPath(p.Get("output_path"));
            var wantsIsolated = p.GetBool("isolated")
                || p.GetRaw("target") != null
                || p.GetRaw("path") != null
                || p.GetRaw("instance_id") != null;

            try
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                if (wantsIsolated)
                    return CaptureIsolated(p, width, height, outputPath);

                switch (view)
                {
                    case "scene":
                        return CaptureSceneView(width, height, outputPath);
                    case "game":
                        return CaptureGameView(width, height, outputPath);
                    default:
                        return new ErrorResponse("INVALID_PARAM", $"Unknown view '{view}'. Valid: scene, game.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse("SCREENSHOT_FAILED", $"Screenshot failed: {e.Message}");
            }
        }

        private static string ResolveOutputPath(string userPath)
        {
            if (string.IsNullOrEmpty(userPath))
                userPath = "Screenshots/screenshot.png";

            if (Path.IsPathRooted(userPath))
                return Path.GetFullPath(userPath);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, userPath));
        }

        private static object CaptureSceneView(int width, int height, string outputPath)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (!sceneView)
                return new ErrorResponse("SCENEVIEW_NOT_FOUND", "No active SceneView found.");

            var sceneCapture = CaptureSceneViewWindow(sceneView, width, height, outputPath);
            if (sceneCapture != null)
                return sceneCapture;

            var camera = sceneView.camera;
            if (!camera)
                return new ErrorResponse("SCENEVIEW_CAMERA_NULL", "SceneView camera is null.");

            if (!CanUseDirectCameraRender())
                return DirectCameraRenderUnavailable("SceneView");

            return CaptureCamera(camera, width, height, outputPath);
        }

        private static object CaptureGameView(int width, int height, string outputPath)
        {
            var gameCapture = CaptureGameViewWindow(width, height, outputPath);
            if (gameCapture != null)
                return gameCapture;

            var camera = Camera.main;
            if (!camera)
            {
#if UNITY_2023_1_OR_NEWER
                camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
                camera = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
                if (!camera)
                    return new ErrorResponse("CAMERA_NOT_FOUND", "No camera found in scene.");
            }

            if (!CanUseDirectCameraRender())
                return DirectCameraRenderUnavailable("GameView");

            return CaptureCamera(camera, width, height, outputPath);
        }

        private static object CaptureSceneViewWindow(SceneView sceneView, int width, int height, string outputPath)
        {
            sceneView.Focus();
            sceneView.Repaint();

            var sceneCapture = CaptureEditorRenderTexture(
                width,
                height,
                outputPath,
                rt => TryInvokeInternalEditorCapture(
                    "CaptureSceneView",
                    new[] { typeof(SceneView), typeof(RenderTexture) },
                    sceneView,
                    rt));
            if (sceneCapture != null)
                return sceneCapture;

            return CaptureEditorRenderTexture(
                width,
                height,
                outputPath,
                rt => TryInvokeInternalEditorCapture(
                    "CaptureEditorWindow",
                    new[] { typeof(EditorWindow), typeof(RenderTexture) },
                    sceneView,
                    rt));
        }

        private static object CaptureGameViewWindow(int width, int height, string outputPath)
        {
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return null;

            var gameView = EditorWindow.GetWindow(gameViewType);
            if (!gameView) return null;
            gameView.Focus();
            gameView.Repaint();

            return CaptureEditorRenderTexture(
                width,
                height,
                outputPath,
                rt => TryInvokeInternalEditorCapture(
                    "CaptureEditorWindow",
                    new[] { typeof(EditorWindow), typeof(RenderTexture) },
                    gameView,
                    rt));
        }

        private static bool TryInvokeInternalEditorCapture(
            string methodName,
            Type[] parameterTypes,
            UnityEngine.Object target,
            RenderTexture rt)
        {
            var method = typeof(InternalEditorUtility).GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                parameterTypes,
                null);
            if (method == null) return false;

            return method.Invoke(null, new object[] { target, rt }) is bool ok && ok;
        }

        private static object CaptureEditorRenderTexture(
            int width,
            int height,
            string outputPath,
            Func<RenderTexture, bool> capture)
        {
            var previousRT = RenderTexture.active;
            RenderTexture rt = null;
            Texture2D tex = null;

            try
            {
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                if (!capture(rt))
                    return null;

                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                File.WriteAllBytes(outputPath, tex.EncodeToPNG());

                return new SuccessResponse($"Screenshot saved to {outputPath}",
                    new { path = outputPath, width, height });
            }
            finally
            {
                RenderTexture.active = previousRT;
                if (rt) UnityEngine.Object.DestroyImmediate(rt);
                if (tex) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static bool CanUseDirectCameraRender()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (!pipeline)
                return true;

            var pipelineType = pipeline.GetType().FullName ?? string.Empty;
            return pipelineType.IndexOf("UniversalRenderPipeline", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static ErrorResponse DirectCameraRenderUnavailable(string view)
        {
            return new ErrorResponse(
                "SCREENSHOT_FAILED",
                $"[Hera] I couldn't capture {view} through the editor window, and direct Camera.Render fallback is disabled for URP because it can trigger Unity 6 RenderGraph errors.");
        }

        private static object CaptureCamera(Camera camera, int width, int height, string outputPath)
        {
            var previousRT = camera.targetTexture;
            RenderTexture rt = null;
            Texture2D tex = null;

            try
            {
                rt = new RenderTexture(width, height, 24);
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                File.WriteAllBytes(outputPath, tex.EncodeToPNG());

                return new SuccessResponse($"Screenshot saved to {outputPath}",
                    new { path = outputPath, width, height });
            }
            finally
            {
                camera.targetTexture = previousRT;
                RenderTexture.active = null;
                if (rt) UnityEngine.Object.DestroyImmediate(rt);
                if (tex) UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
