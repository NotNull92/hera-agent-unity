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
        Description = "Unity UI pipeline. With ui_system=ugui, export/apply build the existing compact ui_doc/2 GameObject + RectTransform IR. With ui_system=uitk, apply accepts backend=uitk and emits validated runtime UXML + shared USS + PanelSettings + UIDocument scaffolding under Assets/HeraGenerated/UI; every runtime element, UXML attribute, and USS property is checked against the connected Editor's bundled reflection schema. import/gen_sprite remain asset helpers; capture/export are uGUI-only. Element property edits stay in manage_components; juice recipes ride apply's agent_hint when Game Feel UI Mode (Beta) is on.",
        Examples = new[]
        {
            "hera-agent-unity ui_doc catalog --dir /Users/me/Downloads/UIKit",
            "hera-agent-unity ui_doc import --src /Users/me/Downloads/UIKit/btn.png --into Assets/UI --border 12,12,12,12",
            "hera-agent-unity ui_doc import --file imports.json",
            "hera-agent-unity ui_doc apply --file design.json --parent /Canvas",
            "hera-agent-unity ui_doc gen_sprite --spec '{\"kind\":\"rounded_rect\",\"size\":[240,64],\"color\":\"#1A1A2EFF\",\"radius\":12}' --out Assets/UI/btn_bg.png",
            "hera-agent-unity ui_doc capture --out /tmp/ui.png",
        },
        ExampleDescriptions = new[]
        {
            "Scan a folder of UI sprites into a manifest (CLI-side; the agent then reads the PNGs to classify them)",
            "Import one external sprite as a 9-slice Sprite asset under Assets/UI",
            "Import many sprites with per-sprite settings (into/items via a JSON file)",
            "Build a UI doc under an explicit parent (reference the imported sprites by Assets/ path)",
            "Bake + import a rounded-rect sprite under Assets/",
            "Render the live overlay UI to a PNG for visual verification",
        })]
    public static class UiDoc
    {
        public class Parameters
        {
            [ToolParameter("Action: export, apply, import, gen_sprite, capture. (sample and catalog are CLI-side.) UI Toolkit supports apply; export/capture remain uGUI-only.", Required = true)]
            public string Action { get; set; }

            [ToolParameter("export: target subtree by hierarchy path. Alternative to instance_id.")]
            public string Path { get; set; }

            [ToolParameter("export: target subtree by InstanceID.")]
            public int? InstanceId { get; set; }

            [ToolParameter("export: max child depth to walk (default 8).")]
            public int? Depth { get; set; }

            [ToolParameter("apply: a ui_doc/2 document (schema in docs/UI_DOC_IR.md). ui_system=ugui uses rect/image/text/layout nodes. ui_system=uitk requires backend=uitk and runtime element names with attributes plus style (USS) objects. The CLI injects this from --file so the doc never rides inline in the agent's context; inline JSON is also accepted.")]
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

            [ToolParameter("import: source image absolute path (single-sprite form). Use --file for many sprites / per-sprite settings.")]
            public string Src { get; set; }

            [ToolParameter("import: destination folder under Assets/ (default Assets/HeraImported). The --file doc may also carry 'into'.")]
            public string Into { get; set; }

            [ToolParameter("import: 9-slice sprite border [left,bottom,right,top] in px — sets the sprite to Sliced so corners stay fixed when scaled.")]
            public string Border { get; set; }

            [ToolParameter("import: pixels-per-unit for the imported sprite(s). Default 100.")]
            public float? Ppu { get; set; }

            [ToolParameter("import: filter mode — 'point' (pixel art) or 'bilinear' (default).")]
            public string Filter { get; set; }

            [ToolParameter("import: sprite pivot [x,y] in 0..1 (custom alignment). Default center.")]
            public string Pivot { get; set; }
        }

        public static object HandleCommand(JObject raw)
        {
            var p = new ToolParams(raw);
            var action = (p.GetRaw("args") as JArray)?[0]?.ToString() ?? p.Get("action");
            if (string.IsNullOrWhiteSpace(action))
                return new ErrorResponse("MISSING_PARAM", "'action' required: export, apply, gen_sprite.");

            switch (action.ToLowerInvariant())
            {
                case "export": return HeraSettings.UsesUiToolkit
                    ? new ErrorResponse("UITK_ACTION_UNSUPPORTED", "ui_doc export only serializes live uGUI GameObjects; UI Toolkit v1 emits UXML/USS from an input document.")
                    : Export(raw);
                case "apply": return Apply(raw);
                case "import": return Import(raw);
                case "gen_sprite":
                case "gensprite": return GenSprite(raw);
                case "capture": return HeraSettings.UsesUiToolkit
                    ? new ErrorResponse("UITK_ACTION_UNSUPPORTED", "ui_doc capture renders overlay Canvases and is unavailable for UI Toolkit output.")
                    : Capture(raw);
                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action '{action}'. Valid: export, apply, import, gen_sprite, capture.");
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
                if (err != null) return err;
                target = go.transform;
            }
            else
            {
                string path = p.Get("path");
                if (string.IsNullOrEmpty(path))
                    return new ErrorResponse("MISSING_PARAM", "export needs 'path' or 'instance_id' (the UI subtree root).");
                var (t, err) = TargetResolver.ResolveTransform(path);
                if (err != null) return err;
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

            if (HeraSettings.UsesUiToolkit)
                return ApplyUiToolkitDocument(doc, raw);

            var rootNode = doc["root"] as JObject ?? doc; // allow a bare node too
            if (rootNode["name"] == null && rootNode["element"] == null)
                return new ErrorResponse("INVALID_DOC", "doc has no 'root' node (expected {schema, root:{...}} or a bare node).");

            Transform parent;
            var parentTok = p.GetRaw("parent");
            bool explicitParent = parentTok != null && parentTok.Type != JTokenType.Null && !string.IsNullOrEmpty(parentTok.ToString());
            if (explicitParent)
            {
                var (t, err) = TargetResolver.ResolveTransform(parentTok.ToString());
                if (err != null) return err;
                parent = t;
            }
            else if (string.Equals(rootNode["element"]?.ToString(), "canvas", System.StringComparison.OrdinalIgnoreCase))
            {
                // A root canvas element stands alone at the scene root; the
                // top-level `canvas` config configures its CanvasScaler.
                parent = null;
            }
            else
            {
                var canvas = EnsureCanvas();
                parent = canvas != null ? canvas.transform : null;
            }

            bool upsert = string.Equals(p.Get("mode"), "upsert", System.StringComparison.OrdinalIgnoreCase);

            var stats = new UiDocSchema.ApplyStats();
            UiDocFixer.PreflightDocument(doc, stats.Diagnostics);
            var canvasConfig = doc["canvas"] as JObject;
            var root = UiDocSchema.ApplyNode(rootNode, parent, stats, upsert, canvasConfig);

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
                docs_version = stats.FixerProfile.docs_version,
                ugui_package = stats.FixerProfile.ugui_package,
                manual_url = stats.FixerProfile.manual_url,
                fixes = stats.Fixes,
                diagnostics = stats.Diagnostics,
                errors = stats.Errors,
                root_id = root != null ? EntityIdCompat.IdOf(root) : 0,
            });
            if (HeraSettings.GameFeelUiMode)
                resp.agent_hint = UIJuiceGuide.ForElements(stats.ElementTypes, HeraSettings.DotweenPreferred);
            return resp;
        }

        internal static object ApplyUiToolkitDocument(JObject doc, JObject raw)
        {
            if (!string.Equals(doc?["backend"]?.ToString(), "uitk", System.StringComparison.OrdinalIgnoreCase))
                return new ErrorResponse("UI_SYSTEM_MISMATCH", "ui_system is uitk, so ui_doc apply requires a document with backend: 'uitk'.");

            var p = new ToolParams(raw);
            Transform parent = null;
            var parentToken = p.GetRaw("parent");
            if (parentToken != null && parentToken.Type != JTokenType.Null && !string.IsNullOrEmpty(parentToken.ToString()))
            {
                var (target, error) = TargetResolver.ResolveTransform(parentToken.ToString());
                if (error != null) return error;
                parent = target;
            }

            var upsert = string.Equals(p.Get("mode"), "upsert", System.StringComparison.OrdinalIgnoreCase);
            var result = UiToolkitDocument.Apply(doc, parent, upsert);
            if (result.Errors.Count > 0)
            {
                var message = result.UpsertMayBePartial
                    ? "UI Toolkit document apply failed after existing output may have changed."
                    : "UI Toolkit document was not emitted.";
                return new ErrorResponse("UITK_VALIDATION_FAILED", message, new
                {
                    uitk_version = result.FixerProfile.uitk_version,
                    uxml_traits = result.FixerProfile.uxml_traits,
                    uxml_api = result.FixerProfile.uxml_api,
                    manual_url = result.FixerProfile.manual_url,
                    fixes = result.Fixes,
                    diagnostics = result.Diagnostics,
                    errors = result.Errors,
                    rollback_attempted = result.RollbackAttempted,
                    rolled_back_artifacts = result.RolledBackArtifacts,
                    rollback_errors = result.RollbackErrors,
                    upsert_may_be_partial = result.UpsertMayBePartial,
                });
            }

            var response = new SuccessResponse("Applied UI Toolkit document", new
            {
                created = result.Created,
                updated = result.Updated,
                elements = result.Elements,
                uitk_version = result.FixerProfile.uitk_version,
                uxml_traits = result.FixerProfile.uxml_traits,
                uxml_api = result.FixerProfile.uxml_api,
                manual_url = result.FixerProfile.manual_url,
                uxml_asset = result.UxmlAsset,
                uss_asset = result.UssAsset,
                panel_settings_asset = result.PanelSettingsAsset,
                world_space = result.WorldSpace,
                fixes = result.Fixes,
                diagnostics = result.Diagnostics,
                root_id = result.RootId,
            });
            if (HeraSettings.GameFeelUiMode)
                response.agent_hint = UIJuiceGuide.ForUiToolkitElements(result.ElementTypes);
            return response;
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

        // Default landing folder for imported external art (parallels
        // ProceduralSprite.DefaultDir for generated art).
        const string DefaultImportDir = "Assets/HeraImported";

        // Import copies external sprite files (absolute paths — a downloaded UI
        // kit, exported art) into the project and imports them as Sprite assets so
        // apply can reference them by Assets/ path. The agent picks files from a
        // `catalog` scan and decides each sprite's role/border by sight; this just
        // realizes that choice. GIFs are skipped (Unity has no GIF→Sprite import).
        static object Import(JObject raw)
        {
            var p = new ToolParams(raw);

            string into = p.Get("into");
            var doc = AsObject(p.GetRaw("doc"));
            if (string.IsNullOrWhiteSpace(into) && doc?["into"] != null)
                into = doc["into"].ToString();
            if (string.IsNullOrWhiteSpace(into)) into = DefaultImportDir;
            if (!AssetPathGuard.TryNormalizeAssetFolder(into, out into, out var pathErr))
                return new ErrorResponse("INVALID_DEST", $"[Hera] I can only import under Assets/: {pathErr}");

            // Rich form: --file doc = { into?, items: [ {src, name?, border?, ppu?, filter?, pivot?}, ... ] }.
            var items = new List<JObject>();
            if (doc?["items"] is JArray arr)
                foreach (var it in arr)
                    if (it is JObject io) items.Add(io);

            // Simple form: a single sprite from --src (or the first positional arg) + shared flags.
            if (items.Count == 0)
            {
                string src = p.Get("src");
                if (string.IsNullOrEmpty(src) && p.GetRaw("args") is JArray a && a.Count > 1)
                    src = a[1]?.ToString(); // args[0] is the action ("import")
                if (string.IsNullOrEmpty(src))
                    return new ErrorResponse("MISSING_PARAM", "import needs items (via --file) or --src <absolute path>.");
                var single = new JObject { ["src"] = src };
                CopyIfPresent(p, single, "name");
                CopyIfPresent(p, single, "border");
                CopyIfPresent(p, single, "ppu");
                CopyIfPresent(p, single, "filter");
                CopyIfPresent(p, single, "pivot");
                items.Add(single);
            }

            try { if (!Directory.Exists(into)) Directory.CreateDirectory(into); }
            catch (System.Exception e)
            {
                return new ErrorResponse("DEST_CREATE_FAILED", $"[Hera] I couldn't create '{into}': {e.Message}");
            }

            var imported = new List<object>();
            var skipped = new List<object>();
            var errors = new List<string>();

            foreach (var item in items)
            {
                var src = item["src"]?.ToString();
                if (string.IsNullOrEmpty(src)) { errors.Add("an item is missing 'src'."); continue; }
                if (!File.Exists(src)) { errors.Add($"source not found: {src}"); continue; }

                var ext = Path.GetExtension(src).ToLowerInvariant();
                if (ext == ".gif")
                {
                    skipped.Add(new { src, reason = "gif is reference-only — Unity has no GIF→Sprite import." });
                    continue;
                }
                if (!IsImportableImage(ext))
                {
                    skipped.Add(new { src, reason = $"unsupported image type '{ext}'." });
                    continue;
                }

                var name = item["name"]?.ToString();
                if (string.IsNullOrEmpty(name)) name = Path.GetFileNameWithoutExtension(src);
                var dest = AssetDatabase.GenerateUniqueAssetPath($"{into}/{SanitizeFileName(name)}{ext}");

                try { File.Copy(src, dest, overwrite: false); }
                catch (System.Exception e) { errors.Add($"copy '{src}': {e.Message}"); continue; }

                AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);

                Vector4? border = null;
                if (item["border"] != null && SerializedPropertyValue.TryParseFloats(item["border"], 4, out var bd, out _))
                    border = new Vector4(bd[0], bd[1], bd[2], bd[3]);
                Vector2? pivot = null;
                if (item["pivot"] != null && SerializedPropertyValue.TryParseFloats(item["pivot"], 2, out var pv, out _))
                    pivot = new Vector2(pv[0], pv[1]);
                float ppu = item["ppu"]?.Value<float>() ?? 100f;
                bool point = string.Equals(item["filter"]?.ToString(), "point", System.StringComparison.OrdinalIgnoreCase);

                ConfigureSpriteImporter(dest, border, ppu, point, pivot);

                var spr = AssetDatabase.LoadAssetAtPath<Sprite>(dest);
                imported.Add(new
                {
                    src,
                    asset = dest,
                    instance_id = spr != null ? EntityIdCompat.IdOf(spr) : 0,
                    sliced = border.HasValue,
                });
            }

            AssetDatabase.Refresh();

            var msg = $"Imported {imported.Count} sprite(s) into {into}";
            if (skipped.Count > 0) msg += $", {skipped.Count} skipped";
            if (errors.Count > 0) msg += $", {errors.Count} errors";
            return new SuccessResponse(msg, new { into, imported, skipped, errors, count = imported.Count });
        }

        // Configures a freshly-imported texture as a Sprite. Replicates
        // ProceduralSprite's importer settings block (this is the 2nd consumer;
        // extract to a Core helper at the 3rd per the repo's replicate-then-extract
        // convention) and adds ppu / point-filter / custom-pivot for real art.
        static void ConfigureSpriteImporter(string path, Vector4? border, float ppu, bool point, Vector2? pivot)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            if (ppu > 0) importer.spritePixelsPerUnit = ppu;

            if (border.HasValue || pivot.HasValue)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (border.HasValue)
                {
                    settings.spriteBorder = border.Value; // (left, bottom, right, top)
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                }
                if (pivot.HasValue)
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = pivot.Value;
                }
                importer.SetTextureSettings(settings);
            }
            importer.SaveAndReimport();
        }

        // Raster types Unity imports as textures (Go can't decode tga/psd/exr/etc,
        // but the editor can). GIF is handled separately (reference-only).
        static bool IsImportableImage(string ext)
        {
            switch (ext)
            {
                case ".png": case ".jpg": case ".jpeg": case ".tga":
                case ".psd": case ".bmp": case ".exr": case ".tif": case ".tiff":
                    return true;
                default: return false;
            }
        }

        static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
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

#if UNITY_6000_5_OR_NEWER
            var all = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
#else
            var all = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#endif
            Canvas only = null;
            var canvasSel = p.Get("canvas");
            if (!string.IsNullOrEmpty(canvasSel))
            {
                var (t, err) = TargetResolver.ResolveTransform(canvasSel);
                if (err != null) return err;
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
#if UNITY_6000_5_OR_NEWER
            var existing = Object.FindAnyObjectByType<Canvas>();
#elif UNITY_2023_1_OR_NEWER
            var existing = Object.FindFirstObjectByType<Canvas>();
#else
            var existing = Object.FindObjectOfType<Canvas>();
#endif
            if (existing != null) return existing.gameObject;

            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = ComponentTypeResolver.Resolve("CanvasScaler");
            if (scaler != null) go.AddComponent(scaler);
            var ray = ComponentTypeResolver.Resolve("GraphicRaycaster");
            if (ray != null) go.AddComponent(ray);

            var esType = ComponentTypeResolver.Resolve("EventSystem");
            if (esType != null && FindAnyObjectByType(esType) == null)
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

        static Object FindAnyObjectByType(System.Type type)
        {
#if UNITY_6000_5_OR_NEWER
            return Object.FindAnyObjectByType(type);
#elif UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType(type);
#else
            return Object.FindObjectOfType(type);
#endif
        }
    }
}
