using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
// `using UnityEditor;` + `using UnityEngine;` leave `Object` ambiguous; alias to
// the engine type so bare Object here is UnityEngine.Object (CS0104 trap).
using Object = UnityEngine.Object;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "ui_doc",
        Description = "HTML→Unity UI pipeline (uGUI). export: serialize a UI subtree to the compact ui_doc IR (grounding for the agent). apply: realize a ui_doc IR under a parent — pass the doc via --file; --mode create (default, always-new) or upsert (match existing children by name and update rect/graphic/text in place). gen_sprite: Tier-1 procedural sprite (solid/rounded_rect/gradient/nine_slice) baked + imported, no external dependency. capture: render the live UI (overlay Canvases, which a normal camera render misses) to a PNG so the agent can verify what it built against a reference. (sample — read colors from a reference image — is handled CLI-side, no Unity round-trip.) Element property edits beyond the IR stay in manage_components; juice recipes ride apply's agent_hint when UI Juicy Mode is on.",
        Examples = new[]
        {
            "hera-agent-unity ui_doc export --path /Canvas/Panel",
            "hera-agent-unity ui_doc apply --file design.json",
            "hera-agent-unity ui_doc apply --file design.json --parent /Canvas",
            "hera-agent-unity ui_doc gen_sprite --spec '{\"kind\":\"rounded_rect\",\"size\":[240,64],\"color\":\"#1A1A2EFF\",\"radius\":12}' --out Assets/UI/btn_bg.png",
            "hera-agent-unity ui_doc capture --out /tmp/ui.png",
        },
        ExampleDescriptions = new[]
        {
            "Export the current state of a subtree as the ui_doc IR (compact, defaults omitted)",
            "Build a UI doc under an existing/auto Canvas",
            "Build a UI doc under an explicit parent",
            "Bake + import a rounded-rect sprite under Assets/",
            "Render the live overlay UI to a PNG for visual verification",
        })]
    public static class UiDoc
    {
        public class Parameters
        {
            [ToolParameter("Action: export, apply, gen_sprite, capture. (sample is CLI-side.)", Required = true)]
            public string Action { get; set; }

            [ToolParameter("export: target subtree by hierarchy path. Alternative to instance_id.")]
            public string Path { get; set; }

            [ToolParameter("export: target subtree by InstanceID.")]
            public int? InstanceId { get; set; }

            [ToolParameter("export: max child depth to walk (default 8).")]
            public int? Depth { get; set; }

            [ToolParameter("apply: the ui_doc/2 IR document (schema in docs/UI_DOC_IR.md). Nodes carry rect (anchor + pos/size or stretch offset_min/max), image (color/sprite + type/fill for progress bars + extras), text (value/color/align/font), and the layout system (layout group + layout_element + fit) for relative arrangement. The CLI injects this from --file so the doc never rides inline in the agent's context; inline JSON is also accepted.")]
            public string Doc { get; set; }

            [ToolParameter("apply: parent path/InstanceID to attach the doc root under. Default: an existing/auto-created Canvas.")]
            public string Parent { get; set; }

            [ToolParameter("apply: 'create' (default — always new objects) or 'upsert' (match existing children by name, update rect/graphic/text in place; no deletes).")]
            public string Mode { get; set; }

            [ToolParameter("gen_sprite: the sprite spec — kind (solid|rounded_rect|gradient|nine_slice), size [w,h], color/from/to, radius, border [l,b,r,t] (nine_slice), direction (gradient). Alternative to individual flags.")]
            public string Spec { get; set; }

            [ToolParameter("gen_sprite: output asset path under Assets/ (e.g. Assets/UI/bg.png). Default: auto under Assets/HeraGenerated/. capture: output PNG path (absolute or project-relative). Default: a temp file.")]
            public string Out { get; set; }

            [ToolParameter("capture: render width in px. Default: the canvas pixel size (current game view).")]
            public int? Width { get; set; }

            [ToolParameter("capture: render height in px. Default: the canvas pixel size (current game view).")]
            public int? Height { get; set; }

            [ToolParameter("capture: background color hex (#RRGGBBAA) or r,g,b[,a]. Default opaque dark; alpha 0 = transparent.")]
            public string Bg { get; set; }

            [ToolParameter("capture: restrict to one Canvas by path/InstanceID. Default: all root non-world canvases.")]
            public string Canvas { get; set; }
        }

        public static object HandleCommand(JObject raw)
        {
            var p = new ToolParams(raw);
            var action = (p.GetRaw("args") as JArray)?[0]?.ToString() ?? p.Get("action");
            if (string.IsNullOrWhiteSpace(action))
                return new ErrorResponse("MISSING_PARAM", "'action' required: export, apply, gen_sprite.");

            switch (action.ToLowerInvariant())
            {
                case "export": return Export(raw);
                case "apply": return Apply(raw);
                case "gen_sprite":
                case "gensprite": return GenSprite(raw);
                case "capture": return Capture(raw);
                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action '{action}'. Valid: export, apply, gen_sprite, capture.");
            }
        }

        static object Export(JObject raw)
        {
            var p = new ToolParams(raw);

            Transform target;
            var idTok = p.GetRaw("instance_id");
            if (idTok != null && idTok.Type != JTokenType.Null)
            {
                var (go, err) = TargetResolver.ResolveGameObject(p);
                if (go == null) return new ErrorResponse("TARGET_NOT_FOUND", err);
                target = go.transform;
            }
            else
            {
                string path = p.Get("path");
                if (string.IsNullOrEmpty(path))
                    return new ErrorResponse("MISSING_PARAM", "export needs 'path' or 'instance_id' (the UI subtree root).");
                var (t, err) = TargetResolver.ResolveTransform(path);
                if (t == null) return new ErrorResponse("TARGET_NOT_FOUND", err);
                target = t;
            }

            int depth = p.GetInt("depth", 8) ?? 8;
            var doc = new JObject
            {
                ["schema"] = UiDocSchema.SchemaId,
                ["backend"] = "ugui",
                ["root"] = UiDocSchema.ExportNode(target, depth),
            };
            return new SuccessResponse($"Exported {target.name}", doc);
        }

        static object Apply(JObject raw)
        {
            var p = new ToolParams(raw);
            var doc = AsObject(p.GetRaw("doc"));
            if (doc == null)
                return new ErrorResponse("MISSING_PARAM", "apply needs 'doc' (the ui_doc IR; pass --file design.json).");

            var rootNode = doc["root"] as JObject ?? doc; // allow a bare node too
            if (rootNode["name"] == null && rootNode["element"] == null)
                return new ErrorResponse("INVALID_DOC", "doc has no 'root' node (expected {schema, root:{...}} or a bare node).");

            Transform parent;
            var parentTok = p.GetRaw("parent");
            if (parentTok != null && parentTok.Type != JTokenType.Null && !string.IsNullOrEmpty(parentTok.ToString()))
            {
                var (t, err) = TargetResolver.ResolveTransform(parentTok.ToString());
                if (t == null) return new ErrorResponse("PARENT_NOT_FOUND", err);
                parent = t;
            }
            else
            {
                var canvas = EnsureCanvas();
                parent = canvas != null ? canvas.transform : null;
            }

            bool upsert = string.Equals(p.Get("mode"), "upsert", System.StringComparison.OrdinalIgnoreCase);

            var stats = new UiDocSchema.ApplyStats();
            var root = UiDocSchema.ApplyNode(rootNode, parent, stats, upsert);

            if (root != null)
            {
                Selection.activeGameObject = root;
                if (root.scene.IsValid()) EditorSceneManager.MarkSceneDirty(root.scene);
            }

            var counts = upsert
                ? $"{stats.Created} created, {stats.Updated} updated, {stats.Sprites} sprites"
                : $"{stats.Created} created, {stats.Sprites} sprites";
            var message = stats.Errors.Count > 0
                ? $"Applied ui_doc: {counts}, {stats.Errors.Count} errors"
                : $"Applied ui_doc: {counts}";

            var resp = new SuccessResponse(message, new
            {
                created = stats.Created,
                updated = stats.Updated,
                sprites = stats.Sprites,
                errors = stats.Errors,
                root_id = root != null ? EntityIdCompat.IdOf(root) : 0,
            });
            if (HeraSettings.JuicyMode)
                resp.agent_hint = UIJuiceGuide.ForElements(stats.ElementTypes, HeraSettings.DotweenPreferred);
            return resp;
        }

        static object GenSprite(JObject raw)
        {
            var p = new ToolParams(raw);
            var spec = AsObject(p.GetRaw("spec"));
            if (spec == null)
            {
                spec = new JObject();
                CopyIfPresent(p, spec, "kind");
                CopyIfPresent(p, spec, "size");
                CopyIfPresent(p, spec, "color");
                CopyIfPresent(p, spec, "radius");
                CopyIfPresent(p, spec, "border");
                CopyIfPresent(p, spec, "from");
                CopyIfPresent(p, spec, "to");
                CopyIfPresent(p, spec, "direction");
            }
            if (spec.Count == 0)
                return new ErrorResponse("MISSING_PARAM", "gen_sprite needs 'spec' (or --kind/--size/--color flags).");

            var (path, err) = ProceduralSprite.Generate(spec, p.Get("out"));
            if (err != null) return new ErrorResponse("SPRITE_GEN_FAILED", err);

            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return new SuccessResponse($"Generated sprite: {path}", new
            {
                asset = path,
                instance_id = spr != null ? EntityIdCompat.IdOf(spr) : 0,
            });
        }

        // Capture renders the live UI to a PNG. ScreenSpaceOverlay canvases are
        // composited after the camera, so a normal `screenshot` (camera render)
        // misses them; here we temporarily route every root non-world canvas
        // through a throwaway camera + RenderTexture, ReadPixels → PNG, then
        // restore each canvas. This institutionalizes the verify loop (build →
        // capture → compare to reference) the agent otherwise hand-rolled via exec.
        static object Capture(JObject raw)
        {
            var p = new ToolParams(raw);

            var all = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            Canvas only = null;
            var canvasSel = p.Get("canvas");
            if (!string.IsNullOrEmpty(canvasSel))
            {
                var (t, err) = TargetResolver.ResolveTransform(canvasSel);
                if (t == null) return new ErrorResponse("TARGET_NOT_FOUND", err);
                var c = t.GetComponentInParent<Canvas>();
                only = c != null ? c.rootCanvas : null;
                if (only == null) return new ErrorResponse("TARGET_NOT_FOUND", $"[Hera] I found '{canvasSel}' but it isn't under a Canvas.");
            }

            var targets = new List<Canvas>();
            foreach (var c in all)
            {
                if (!c.isRootCanvas) continue;
                if (c.renderMode == RenderMode.WorldSpace) continue; // world canvases render through the normal camera
                if (only != null && c != only) continue;
                targets.Add(c);
            }

            int w = p.GetInt("width", 0) ?? 0;
            int h = p.GetInt("height", 0) ?? 0;
            if (targets.Count > 0)
            {
                var pr = targets[0].pixelRect;
                if (w <= 0) w = Mathf.RoundToInt(pr.width);
                if (h <= 0) h = Mathf.RoundToInt(pr.height);
            }
            if (w <= 0) w = 1920;
            if (h <= 0) h = 1080;

            var bg = new Color(0.10f, 0.10f, 0.12f, 1f);
            var bgStr = p.Get("bg");
            if (!string.IsNullOrEmpty(bgStr) && SerializedPropertyValue.TryParseColor(new JValue(bgStr), out var parsed, out _))
                bg = parsed;

            var outPath = p.Get("out");
            if (string.IsNullOrEmpty(outPath))
                outPath = Path.Combine(Path.GetTempPath(), "hera_ui_capture.png");

            var saved = new (Canvas c, RenderMode mode, Camera cam, float pd)[targets.Count];
            GameObject camGO = null;
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                camGO = new GameObject("HeraShotCam") { hideFlags = HideFlags.HideAndDontSave };
                var cam = camGO.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = bg;
                cam.orthographic = true;
                camGO.transform.position = new Vector3(0, 0, -100);

                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;

                for (int i = 0; i < targets.Count; i++)
                {
                    var c = targets[i];
                    saved[i] = (c, c.renderMode, c.worldCamera, c.planeDistance);
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 50f;
                }

                Canvas.ForceUpdateCanvases();
                cam.Render();

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                var bytes = tex.EncodeToPNG();
                File.WriteAllBytes(outPath, bytes);

                return new SuccessResponse($"Captured {targets.Count} canvas(es) -> {outPath}", new
                {
                    path = outPath,
                    width = w,
                    height = h,
                    bytes = bytes.Length,
                    canvases = targets.Count,
                });
            }
            catch (System.Exception e)
            {
                return new ErrorResponse("CAPTURE_FAILED", $"[Hera] I couldn't capture the UI: {e.Message}");
            }
            finally
            {
                for (int i = 0; i < saved.Length; i++)
                {
                    var s = saved[i];
                    if (s.c == null) continue;
                    s.c.renderMode = s.mode;
                    s.c.worldCamera = s.cam;
                    s.c.planeDistance = s.pd;
                }
                if (camGO != null) Object.DestroyImmediate(camGO);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (tex != null) Object.DestroyImmediate(tex);
            }
        }

        static void CopyIfPresent(ToolParams p, JObject spec, string key)
        {
            var tok = p.GetRaw(key);
            if (tok != null && tok.Type != JTokenType.Null) spec[key] = tok;
        }

        static JObject AsObject(JToken tok)
        {
            if (tok == null || tok.Type == JTokenType.Null) return null;
            if (tok is JObject o) return o;
            try { return JObject.Parse(tok.ToString()); }
            catch { return null; }
        }

        static GameObject EnsureCanvas()
        {
            var existing = Object.FindFirstObjectByType<Canvas>();
            if (existing != null) return existing.gameObject;

            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = ComponentTypeResolver.Resolve("CanvasScaler");
            if (scaler != null) go.AddComponent(scaler);
            var ray = ComponentTypeResolver.Resolve("GraphicRaycaster");
            if (ray != null) go.AddComponent(ray);

            var esType = ComponentTypeResolver.Resolve("EventSystem");
            if (esType != null && Object.FindFirstObjectByType(esType) == null)
            {
                var esgo = new GameObject("EventSystem");
                esgo.AddComponent(esType);
                // Match the project's active input handling (see ManageUI rationale).
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                var mod = ComponentTypeResolver.Resolve("InputSystemUIInputModule") ?? ComponentTypeResolver.Resolve("StandaloneInputModule");
#else
                var mod = ComponentTypeResolver.Resolve("StandaloneInputModule") ?? ComponentTypeResolver.Resolve("InputSystemUIInputModule");
#endif
                if (mod != null) esgo.AddComponent(mod);
                Undo.RegisterCreatedObjectUndo(esgo, "Hera ui_doc EventSystem");
            }

            Undo.RegisterCreatedObjectUndo(go, "Hera ui_doc Canvas");
            return go;
        }
    }
}
