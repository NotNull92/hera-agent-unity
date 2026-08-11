using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "screenshot",
        Description = "Capture a Scene/Game view or isolated target, with optional bounded uGUI or 3D physics identity and input-coordinate evidence.",
        Profiles = new[] { "core", "scene", "ui", "diagnostics", "testing" },
        RiskClass = HeraRiskClass.Write,
        Reversible = true,
        ContractMode = ToolContractMode.Strict)]
    [HeraArgumentGroup(ToolArgumentGroupMode.AtMostOne, "target", "path", "instance_id")]
    [HeraArgumentGroup(
        ToolArgumentGroupMode.RequiredWhen,
        "isolated=true",
        "target",
        "path",
        "instance_id",
        Path = "/isolated",
        Expected = "target, path, or instance_id when isolated is true")]
    [HeraSafetyRule(
        "screenshot.overwrite",
        "overwrite",
        "true",
        RiskClass = HeraRiskClass.Write,
        RequiresConfirmation = true)]
    public static partial class EditorScreenshot
    {
        private const int DefaultWidth = 1920;
        private const int DefaultHeight = 1080;

        public class Parameters
        {
            [ToolParameter(
                "View to capture.",
                Required = false,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"scene\",\"game\"]}")]
            public string View { get; set; }

            [ToolParameter(
                "Render active ScreenSpaceOverlay canvases to a PNG instead of a Scene or Game view.",
                Required = false)]
            public bool Overlay { get; set; }

            [ToolParameter(
                "Override width (default 1920).",
                Required = false,
                SchemaJson = "{\"type\":\"integer\",\"minimum\":1}")]
            public int Width { get; set; }

            [ToolParameter(
                "Override height (default 1080).",
                Required = false,
                SchemaJson = "{\"type\":\"integer\",\"minimum\":1}")]
            public int Height { get; set; }

            [ToolParameter("Output file path, absolute or relative to project root (default: unique PNG under Screenshots/)", Required = false)]
            public string OutputPath { get; set; }

            [ToolParameter("Allow replacing an existing PNG under the project or system temp directory.", Required = false)]
            public bool Overwrite { get; set; }

            [ToolParameter("Capture only one GameObject by --target, --path, or --instance_id.", Required = false)]
            public bool Isolated { get; set; }

            [ToolParameter("Hierarchy path for isolated capture (same as --path, e.g. /Player).", Required = false)]
            public string Target { get; set; }

            [ToolParameter("Hierarchy path for isolated capture (e.g. /Player).", Required = false)]
            public string Path { get; set; }

            [ToolParameter("InstanceID for isolated capture.", Required = false)]
            public int InstanceId { get; set; }

            [ToolParameter(
                "Isolated capture angles: iso, front, back, left, right, top, bottom; comma-separated.",
                Required = false,
                SchemaJson = "{\"type\":\"string\",\"pattern\":\"^\\\\s*(?:iso|front|back|left|right|top|bottom)(?:\\\\s*,\\\\s*(?:iso|front|back|left|right|top|bottom))*\\\\s*$\"}")]
            public string Angles { get; set; }

            [ToolParameter(
                "Isolated background color: #RRGGBB, #RRGGBBAA, or transparent.",
                Required = false,
                SchemaJson = "{\"type\":\"string\",\"pattern\":\"^(?:transparent|#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?)$\"}")]
            public string Background { get; set; }

            [ToolParameter(
                "Isolated camera padding fraction (default 0.15).",
                Required = false,
                SchemaJson = "{\"type\":\"number\",\"minimum\":0,\"maximum\":2}")]
            public float Padding { get; set; }

            [ToolParameter(
                "Attach bounded active uGUI Selectable identity, reachability, and coordinate metadata. Game view only.",
                Required = false)]
            public bool AnnotateUi { get; set; }

            [ToolParameter(
                "Return uGUI annotations without resolving an output path, rendering pixels, encoding PNG, or writing a file. Implies annotate_ui and defaults view to game.",
                Required = false)]
            public bool AnnotationsOnly { get; set; }

            [ToolParameter(
                "Maximum uGUI annotations returned (default 32, maximum 100).",
                Required = false,
                SchemaJson = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":100}")]
            public int MaxAnnotations { get; set; }

            [ToolParameter(
                "Attach bounded 3D collider identity and input-coordinate evidence sampled through Camera.main. Game view only.",
                Required = false)]
            public bool AnnotatePhysics { get; set; }

            [ToolParameter(
                "Return 3D physics evidence without resolving an output path, rendering pixels, encoding PNG, or writing a file. Implies annotate_physics and defaults view to game.",
                Required = false)]
            public bool PhysicsOnly { get; set; }

            [ToolParameter(
                "Square 3D raycast grid density (default 9, maximum 16; at most 256 rays).",
                Required = false,
                SchemaJson = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":16}")]
            public int PhysicsGridSize { get; set; }

            [ToolParameter(
                "Maximum clustered 3D collider results returned (default 32, maximum 100).",
                Required = false,
                SchemaJson = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":100}")]
            public int MaxPhysicsHits { get; set; }

            [ToolParameter(
                "Optional 3D physics layer mask, intersected with Camera.main.cullingMask (default: camera culling mask).",
                Required = false,
                SchemaJson = "{\"type\":\"integer\",\"minimum\":-2147483648,\"maximum\":2147483647}")]
            public int PhysicsLayerMask { get; set; }

            [ToolParameter(
                "Maximum 3D raycast distance in world units (default: Camera.main far clip plane).",
                Required = false,
                SchemaJson = "{\"type\":\"number\",\"minimum\":0.0001,\"maximum\":100000}")]
            public float PhysicsMaxDistance { get; set; }

            [ToolParameter(
                "3D trigger handling for physics evidence.",
                Required = false,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"use_global\",\"ignore\",\"collide\"]}")]
            public string PhysicsQueryTriggers { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                @params = new JObject();

            var p = new ToolParams(@params);
            var overlay = p.GetBool("overlay");
            var annotationsOnly = p.GetBool("annotations_only");
            var annotateUi = p.GetBool("annotate_ui") || annotationsOnly;
            var physicsOnly = p.GetBool("physics_only");
            var annotatePhysics = p.GetBool("annotate_physics") || physicsOnly;
            var wantsEvidence = annotateUi || annotatePhysics;
            var view = p.Get("view", wantsEvidence ? "game" : "scene").ToLowerInvariant();
            var width = p.GetInt("width", DefaultWidth).Value;
            var height = p.GetInt("height", DefaultHeight).Value;
            var wantsIsolated = p.GetBool("isolated")
                || p.GetRaw("target") != null
                || p.GetRaw("path") != null
                || p.GetRaw("instance_id") != null;

            try
            {
                if (overlay && wantsEvidence)
                    return new ErrorResponse(
                        "SCREENSHOT_OVERLAY_EVIDENCE_CONFLICT",
                        "[Hera] I can't combine overlay rendering with uGUI or physics annotations.");
                if (overlay && wantsIsolated)
                    return new ErrorResponse(
                        "SCREENSHOT_OVERLAY_ISOLATED_CONFLICT",
                        "[Hera] I can't combine overlay rendering with isolated GameObject rendering.");
                if (annotateUi && view != "game")
                    return new ErrorResponse(
                        "SCREENSHOT_UI_ANNOTATION_REQUIRES_GAME_VIEW",
                        "[Hera] I can annotate uGUI only in the game view coordinate space.");
                if (annotateUi && wantsIsolated)
                    return new ErrorResponse(
                        "SCREENSHOT_UI_ANNOTATION_ISOLATED_CONFLICT",
                        "[Hera] I can't combine uGUI annotations with isolated GameObject rendering.");
                if (annotatePhysics && view != "game")
                    return new ErrorResponse(
                        "SCREENSHOT_PHYSICS_EVIDENCE_REQUIRES_GAME_VIEW",
                        "[Hera] I can collect 3D physics evidence only in the game view coordinate space.");
                if (annotatePhysics && wantsIsolated)
                    return new ErrorResponse(
                        "SCREENSHOT_PHYSICS_EVIDENCE_ISOLATED_CONFLICT",
                        "[Hera] I can't combine 3D physics evidence with isolated GameObject rendering.");
                if (annotationsOnly &&
                    (p.GetRaw("output_path") != null || p.GetBool("overwrite")))
                {
                    return new ErrorResponse(
                        "SCREENSHOT_ANNOTATIONS_ONLY_OUTPUT_CONFLICT",
                        "[Hera] I don't accept output_path or overwrite when annotations_only skips all PNG work.");
                }
                if (physicsOnly &&
                    (p.GetRaw("output_path") != null || p.GetBool("overwrite")))
                {
                    return new ErrorResponse(
                        "SCREENSHOT_PHYSICS_ONLY_OUTPUT_CONFLICT",
                        "[Hera] I don't accept output_path or overwrite when physics_only skips all PNG work.");
                }

                var (maxAnnotations, maxError) = ParseMaxAnnotations(p);
                if (maxError != null) return maxError;
                ScreenshotUiAnnotationCollection annotations = null;
                if (annotateUi)
                {
                    var collected = CollectUiAnnotations(maxAnnotations);
                    if (collected.error != null) return collected.error;
                    annotations = collected.annotations;
                }
                ScreenshotPhysicsAnnotationCollection physicsAnnotations = null;
                if (annotatePhysics)
                {
                    var collected = CollectPhysicsAnnotations(p);
                    if (collected.error != null) return collected.error;
                    physicsAnnotations = collected.annotations;
                }
                if (annotationsOnly || physicsOnly)
                {
                    object evidenceResponse = new SuccessResponse(
                        "Screenshot evidence collected without capturing pixels",
                        new { pixels_requested = false });
                    if (annotations != null)
                        evidenceResponse = AttachUiAnnotations(evidenceResponse, annotations, 0, 0);
                    if (physicsAnnotations != null)
                        evidenceResponse = AttachPhysicsAnnotations(evidenceResponse, physicsAnnotations, 0, 0);
                    return evidenceResponse;
                }

                var overwrite = p.GetBool("overwrite");
                if (!OutputFilePolicy.TryResolvePng(
                    p.Get("output_path"),
                    "Screenshots/screenshot-" + Guid.NewGuid().ToString("N") + ".png",
                    overwrite,
                    out var outputPath,
                    out var pathErrorCode,
                    out var pathError))
                    return new ErrorResponse(pathErrorCode, pathError);

                if (overlay)
                {
                    var overlayWidth = p.GetInt("width", 0) ?? 0;
                    var overlayHeight = p.GetInt("height", 0) ?? 0;
                    return CaptureOverlayCanvases(
                        overlayWidth,
                        overlayHeight,
                        p.Get("background"),
                        outputPath,
                        overwrite);
                }

                if (wantsIsolated)
                    return CaptureIsolated(p, width, height, outputPath, overwrite);

                object response;
                switch (view)
                {
                    case "scene":
                        response = CaptureSceneView(width, height, outputPath, overwrite);
                        break;
                    case "game":
                        response = CaptureGameView(width, height, outputPath, overwrite);
                        break;
                    default:
                        return new ErrorResponse("INVALID_PARAM", $"Unknown view '{view}'. Valid: scene, game.");
                }
                if (annotations != null)
                    response = AttachUiAnnotations(response, annotations, width, height);
                if (physicsAnnotations != null)
                    response = AttachPhysicsAnnotations(response, physicsAnnotations, width, height);
                return response;
            }
            catch (Exception e)
            {
                return new ErrorResponse("SCREENSHOT_FAILED", $"Screenshot failed: {e.Message}");
            }
        }

        private static object CaptureSceneView(int width, int height, string outputPath, bool overwrite)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (!sceneView)
                return new ErrorResponse("SCENEVIEW_NOT_FOUND", "No active SceneView found.");

            var sceneCapture = CaptureSceneViewWindow(sceneView, width, height, outputPath, overwrite);
            if (sceneCapture != null)
                return sceneCapture;

            var camera = sceneView.camera;
            if (!camera)
                return new ErrorResponse("SCENEVIEW_CAMERA_NULL", "SceneView camera is null.");

            if (!CanUseDirectCameraRender())
                return DirectCameraRenderUnavailable("SceneView");

            return CaptureCamera(camera, width, height, outputPath, overwrite);
        }

        private static object CaptureGameView(int width, int height, string outputPath, bool overwrite)
        {
            var gameCapture = CaptureGameViewWindow(width, height, outputPath, overwrite);
            if (gameCapture != null)
                return gameCapture;

            var camera = Camera.main;
            if (!camera)
            {
#if UNITY_6000_5_OR_NEWER
                camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
#elif UNITY_2023_1_OR_NEWER
                camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
                camera = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
                if (!camera)
                    return new ErrorResponse("CAMERA_NOT_FOUND", "No camera found in scene.");
            }

            if (!CanUseDirectCameraRender())
                return DirectCameraRenderUnavailable("GameView");

            return CaptureCamera(camera, width, height, outputPath, overwrite);
        }

        private static object CaptureOverlayCanvases(
            int width,
            int height,
            string backgroundValue,
            string outputPath,
            bool overwrite)
        {
#if UNITY_6000_5_OR_NEWER
            var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
#else
            var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#endif
            var targets = new List<Canvas>();
            foreach (var canvas in allCanvases)
            {
                if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    targets.Add(canvas);
            }

            if (targets.Count > 0)
            {
                var pixelRect = targets[0].pixelRect;
                if (width <= 0) width = Mathf.RoundToInt(pixelRect.width);
                if (height <= 0) height = Mathf.RoundToInt(pixelRect.height);
            }
            if (width <= 0) width = DefaultWidth;
            if (height <= 0) height = DefaultHeight;

            var background = new Color(0.10f, 0.10f, 0.12f, 1f);
            if (string.Equals(backgroundValue, "transparent", StringComparison.OrdinalIgnoreCase))
                background = Color.clear;
            else if (!string.IsNullOrEmpty(backgroundValue)
                && SerializedPropertyValue.TryParseColor(new JValue(backgroundValue), out var parsed, out _))
                background = parsed;

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
                for (var index = 0; index < targets.Count; index++)
                {
                    var canvas = targets[index];
                    saved[index] = (canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance);
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
                return new SuccessResponse($"Captured {targets.Count} overlay canvas(es) -> {outputPath}", new
                {
                    path = outputPath,
                    width,
                    height,
                    bytes = bytes.Length,
                    canvases = targets.Count,
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse("SCREENSHOT_OVERLAY_FAILED", $"[Hera] I couldn't capture overlay UI: {exception.Message}");
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
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static object CaptureSceneViewWindow(SceneView sceneView, int width, int height, string outputPath, bool overwrite)
        {
            sceneView.Focus();
            sceneView.Repaint();

            var sceneCapture = CaptureEditorRenderTexture(
                width,
                height,
                outputPath,
                overwrite,
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
                overwrite,
                rt => TryInvokeInternalEditorCapture(
                    "CaptureEditorWindow",
                    new[] { typeof(EditorWindow), typeof(RenderTexture) },
                    sceneView,
                    rt));
        }

        private static object CaptureGameViewWindow(int width, int height, string outputPath, bool overwrite)
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
                overwrite,
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
            bool overwrite,
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

                OutputFilePolicy.WriteAllBytes(outputPath, tex.EncodeToPNG(), overwrite);

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

        private static object CaptureCamera(Camera camera, int width, int height, string outputPath, bool overwrite)
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

                OutputFilePolicy.WriteAllBytes(outputPath, tex.EncodeToPNG(), overwrite);

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
