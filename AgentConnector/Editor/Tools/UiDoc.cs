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
        Description = "HTML→Unity UI pipeline (uGUI). export: serialize a UI subtree to the compact ui_doc IR (grounding for the agent). apply: build a ui_doc IR (always-create) under a parent — pass the doc via --file. gen_sprite: Tier-1 procedural sprite (solid/rounded_rect/gradient) baked + imported, no external dependency. Element property edits beyond the IR stay in manage_components; juice recipes ride apply's agent_hint when UI Juicy Mode is on.",
        Examples = new[]
        {
            "hera-agent-unity ui_doc export --path /Canvas/Panel",
            "hera-agent-unity ui_doc apply --file design.json",
            "hera-agent-unity ui_doc apply --file design.json --parent /Canvas",
            "hera-agent-unity ui_doc gen_sprite --spec '{\"kind\":\"rounded_rect\",\"size\":[240,64],\"color\":\"#1A1A2EFF\",\"radius\":12}' --out Assets/UI/btn_bg.png",
        },
        ExampleDescriptions = new[]
        {
            "Export the current state of a subtree as the ui_doc IR (compact, defaults omitted)",
            "Build a UI doc under an existing/auto Canvas",
            "Build a UI doc under an explicit parent",
            "Bake + import a rounded-rect sprite under Assets/",
        })]
    public static class UiDoc
    {
        public class Parameters
        {
            [ToolParameter("Action: export, apply, gen_sprite.", Required = true)]
            public string Action { get; set; }

            [ToolParameter("export: target subtree by hierarchy path. Alternative to instance_id.")]
            public string Path { get; set; }

            [ToolParameter("export: target subtree by InstanceID.")]
            public int? InstanceId { get; set; }

            [ToolParameter("export: max child depth to walk (default 8).")]
            public int? Depth { get; set; }

            [ToolParameter("apply: the ui_doc IR document. The CLI injects this from --file so the doc never rides inline in the agent's context; inline JSON is also accepted.")]
            public string Doc { get; set; }

            [ToolParameter("apply: parent path/InstanceID to attach the doc root under. Default: an existing/auto-created Canvas.")]
            public string Parent { get; set; }

            [ToolParameter("gen_sprite: the sprite spec {kind,size,color,radius,from,to,direction}. Alternative to individual flags.")]
            public string Spec { get; set; }

            [ToolParameter("gen_sprite: output asset path under Assets/ (e.g. Assets/UI/bg.png). Default: auto under Assets/HeraGenerated/.")]
            public string Out { get; set; }
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
                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action '{action}'. Valid: export, apply, gen_sprite.");
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

            var stats = new UiDocSchema.ApplyStats();
            var root = UiDocSchema.ApplyNode(rootNode, parent, stats);

            if (root != null)
            {
                Selection.activeGameObject = root;
                if (root.scene.IsValid()) EditorSceneManager.MarkSceneDirty(root.scene);
            }

            var message = stats.Errors.Count > 0
                ? $"Applied ui_doc: {stats.Created} created, {stats.Sprites} sprites, {stats.Errors.Count} errors"
                : $"Applied ui_doc: {stats.Created} created, {stats.Sprites} sprites";

            var resp = new SuccessResponse(message, new
            {
                created = stats.Created,
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
