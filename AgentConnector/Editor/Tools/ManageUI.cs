using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
// `using System;` + `using UnityEngine;` leave `Object` ambiguous; alias to the
// engine type so bare Object here is UnityEngine.Object (CS0104 trap — CLAUDE.md).
using Object = UnityEngine.Object;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "manage_ui",
        Description = "uGUI authoring: create (UI-aware element creation with auto Canvas/EventSystem scaffolding), get_rect, set_anchor (named preset grid or raw anchors — keeps the rect visually fixed by default), set_rect (anchored_position/size_delta/pivot/offsets). RectTransform anchor/pivot math is the value-add over raw m_ paths. Element property edits (Image color, Button colors, Text font) stay in manage_components. UI/TMP types are resolved via TypeCache so the connector still compiles in projects without com.unity.ugui.",
        Examples = new[]
        {
            "manage_ui create --element button --name PlayBtn --content Play",
            "manage_ui create --element text --name Title --content Hello --text legacy",
            "manage_ui get_rect --path /Canvas/PlayBtn",
            "manage_ui set_anchor --path /Canvas/Title --preset top-center",
            "manage_ui set_anchor --path /Canvas/Bg --preset stretch --snap true",
            "manage_ui set_rect --path /Canvas/Title --anchored_position 0,-40 --size_delta 300,60",
        },
        ExampleDescriptions = new[]
        {
            "Create a Button (auto Canvas + EventSystem if missing) with a TMP/legacy label",
            "Create a Text element, forcing the legacy UnityEngine.UI.Text engine",
            "Read the full RectTransform of a UI element",
            "Re-anchor to the top-center preset, preserving the element's visual position",
            "Stretch-fill the parent and snap offsets/pivot (Alt+Shift behaviour)",
            "Set anchoredPosition + sizeDelta directly",
        })]
    public static class ManageUI
    {
        public class Parameters
        {
            [ToolParameter("Action: create, get_rect, set_anchor, set_rect", Required = true)]
            public string Action { get; set; }

            [ToolParameter("create: element kind — canvas, panel, image, button, text, empty.")]
            public string Element { get; set; }

            [ToolParameter("create: name for the new element (default = element kind capitalised).")]
            public string Name { get; set; }

            [ToolParameter("create: text/label string for text & button elements.")]
            public string Content { get; set; }

            [ToolParameter("create: text engine override — 'tmp' or 'legacy'. Default: TMP when the package is present, else legacy Text.")]
            public string Text { get; set; }

            [ToolParameter("Parent by hierarchy path or InstanceID. create: defaults to an existing/auto-created Canvas. Target (get_rect/set_*) uses instance_id or path instead.")]
            public string Parent { get; set; }

            [ToolParameter("Target by InstanceID (get_rect/set_anchor/set_rect).")]
            public int? InstanceId { get; set; }

            [ToolParameter("Target by hierarchy path '/Canvas/Child' (get_rect/set_anchor/set_rect).")]
            public string Path { get; set; }

            [ToolParameter("set_anchor: named preset — top-left, top-center, top-right, middle-left, middle-center, middle-right, bottom-left, bottom-center, bottom-right, top-stretch, middle-stretch, bottom-stretch, stretch-left, stretch-center, stretch-right, stretch (full). Alternative to anchor_min/anchor_max.")]
            public string Preset { get; set; }

            [ToolParameter("set_anchor: raw anchorMin 'x,y' (alternative to preset).")]
            public string AnchorMin { get; set; }

            [ToolParameter("set_anchor: raw anchorMax 'x,y' (alternative to preset).")]
            public string AnchorMax { get; set; }

            [ToolParameter("set_anchor: snap to the preset (reset offsets to 0 / fill, move pivot to match — Unity's Alt+Shift click). Default false = keep the rect visually fixed.")]
            public bool? Snap { get; set; }

            [ToolParameter("set_rect: anchoredPosition 'x,y'.")]
            public string AnchoredPosition { get; set; }

            [ToolParameter("set_rect: sizeDelta 'x,y'.")]
            public string SizeDelta { get; set; }

            [ToolParameter("set_anchor/set_rect: pivot 'x,y'.")]
            public string Pivot { get; set; }

            [ToolParameter("set_rect: offsetMin 'x,y' (left/bottom).")]
            public string OffsetMin { get; set; }

            [ToolParameter("set_rect: offsetMax 'x,y' (right/top).")]
            public string OffsetMax { get; set; }
        }

        // ---- create ----

        public static object Create(JObject raw)
        {
            var p = new ToolParams(raw);
            string element = (p.Get("element")
                ?? ((p.GetRaw("args") as JArray)?.Count >= 2 ? ((JArray)p.GetRaw("args"))[1].ToString() : null))
                ?.ToLowerInvariant();
            if (string.IsNullOrEmpty(element))
                return new ErrorResponse("'element' required for create: canvas, panel, image, button, text, empty.");

            var created = new List<string>();

            // canvas is its own root; everything else needs a Canvas ancestor.
            if (element == "canvas")
            {
                var (canvas, cErr) = CreateCanvas(p.Get("name"), created);
                if (cErr != null) return new ErrorResponse(cErr);
                Finalize(canvas, created);
                return new SuccessResponse($"Created Canvas: {canvas.name}", BuildCreateShape(canvas, created));
            }

            // Resolve parent: explicit --parent, else an existing/auto Canvas.
            Transform parent;
            var parentToken = p.GetRaw("parent");
            if (parentToken != null && parentToken.Type != JTokenType.Null && !string.IsNullOrEmpty(parentToken.ToString()))
            {
                var (pt, pErr) = ResolveTransform(parentToken.ToString());
                if (pErr != null) return new ErrorResponse(pErr);
                parent = pt;
            }
            else
            {
                var (canvasGo, cErr) = EnsureCanvas(created);
                if (cErr != null) return new ErrorResponse(cErr);
                parent = canvasGo.transform;
            }

            string name = p.Get("name");
            string textEngine = p.Get("text");
            string content = p.Get("content");

            GameObject go;
            string buildErr;
            switch (element)
            {
                case "empty": (go, buildErr) = BuildEmpty(name); break;
                case "image": (go, buildErr) = BuildImage(name); break;
                case "panel": (go, buildErr) = BuildPanel(name); break;
                case "text": (go, buildErr) = BuildText(name, content, textEngine); break;
                case "button": (go, buildErr) = BuildButton(name, content, textEngine, created); break;
                default:
                    return new ErrorResponse(
                        $"Unknown element: '{element}'. Use canvas, panel, image, button, text, empty.");
            }
            if (buildErr != null)
            {
                if (go != null) Object.DestroyImmediate(go);
                return new ErrorResponse(buildErr);
            }

            // Interactive elements need an EventSystem to receive input
            // (best-effort — a missing one doesn't fail the create).
            if (element == "button")
                EnsureEventSystem(created);

            // worldPositionStays:false keeps the builder's local layout (fresh
            // objects sit at local 0, panels keep their full-stretch fill).
            go.transform.SetParent(parent, worldPositionStays: false);

            Finalize(go, created);
            return new SuccessResponse($"Created {element}: {go.name}", BuildCreateShape(go, created));
        }

        private static (GameObject go, string err) CreateCanvas(string name, List<string> created)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Canvas" : name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (AddByName(go, "CanvasScaler") == null)
            {
                Object.DestroyImmediate(go);
                return (null, "Could not add CanvasScaler — is com.unity.ugui installed?");
            }
            if (AddByName(go, "GraphicRaycaster") == null)
            {
                Object.DestroyImmediate(go);
                return (null, "Could not add GraphicRaycaster — is com.unity.ugui installed?");
            }
            created.Add("Canvas");
            EnsureEventSystem(created);
            return (go, null);
        }

        // Returns an existing Canvas or creates one (recording it in `created`).
        private static (GameObject go, string err) EnsureCanvas(List<string> created)
        {
            var existing = Object.FindFirstObjectByType<Canvas>();
            if (existing != null) return (existing.gameObject, null);
            return CreateCanvas("Canvas", created);
        }

        private static string EnsureEventSystem(List<string> created)
        {
            var esType = ComponentTypeResolver.Resolve("EventSystem");
            if (esType == null) return "EventSystem type not found (com.unity.ugui missing).";
            var existing = Object.FindFirstObjectByType(esType);
            if (existing != null) return null;

            var go = new GameObject("EventSystem");
            if (go.AddComponent(esType) == null)
            {
                Object.DestroyImmediate(go);
                return "Could not add EventSystem.";
            }
            // Pick the input module that matches the project's *active* input
            // handling, not just which type happens to be loadable. Unity sets
            // ENABLE_INPUT_SYSTEM / ENABLE_LEGACY_INPUT_MANAGER from Player
            // Settings, so this gates exactly like Unity's own EventSystem menu.
            // New-only: StandaloneInputModule would still add (the type ships
            // with com.unity.ugui) but throws at runtime — so prefer the new
            // module there and only fall back if it's somehow unavailable.
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (AddByName(go, "InputSystemUIInputModule") == null)
                AddByName(go, "StandaloneInputModule");
#else
            if (AddByName(go, "StandaloneInputModule") == null)
                AddByName(go, "InputSystemUIInputModule");
#endif
            Undo.RegisterCreatedObjectUndo(go, "Hera Create EventSystem");
            created.Add("EventSystem");
            return null;
        }

        private static (GameObject go, string err) BuildEmpty(string name)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "UIElement" : name, typeof(RectTransform));
            SizeTo(go, 100, 100);
            return (go, null);
        }

        private static (GameObject go, string err) BuildImage(string name)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Image" : name, typeof(RectTransform));
            if (AddByName(go, "Image") == null)
                return (go, "Could not add Image — is com.unity.ugui installed?");
            SizeTo(go, 100, 100);
            return (go, null);
        }

        private static (GameObject go, string err) BuildPanel(string name)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Panel" : name, typeof(RectTransform));
            var img = AddByName(go, "Image");
            if (img == null) return (go, "Could not add Image — is com.unity.ugui installed?");
            // Panels stretch to fill their parent with a faint translucent fill,
            // matching Unity's GameObject > UI > Panel default.
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            TrySetProp(img, "color", new Color(1f, 1f, 1f, 0.39f));
            return (go, null);
        }

        private static (GameObject go, string err) BuildText(string name, string content, string engine)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Text" : name, typeof(RectTransform));
            var (text, err) = AddTextComponent(go, engine);
            if (err != null) return (go, err);
            TrySetProp(text, "text", string.IsNullOrEmpty(content) ? "New Text" : content);
            SizeTo(go, 200, 50);
            return (go, null);
        }

        private static (GameObject go, string err) BuildButton(string name, string content, string engine, List<string> created)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Button" : name, typeof(RectTransform));
            var img = AddByName(go, "Image");
            if (img == null) return (go, "Could not add Image — is com.unity.ugui installed?");
            var btn = AddByName(go, "Button");
            if (btn == null) return (go, "Could not add Button — is com.unity.ugui installed?");
            TrySetProp(btn, "targetGraphic", img);
            SizeTo(go, 160, 30);

            // Child label, centred and stretched over the button.
            var labelGo = new GameObject("Text", typeof(RectTransform));
            var (text, err) = AddTextComponent(labelGo, engine);
            if (err != null) { Object.DestroyImmediate(labelGo); return (go, err); }
            TrySetProp(text, "text", string.IsNullOrEmpty(content) ? "Button" : content);
            labelGo.transform.SetParent(go.transform, worldPositionStays: false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            return (go, null);
        }

        // Adds a TMP text component when requested/available, else legacy Text.
        // Engine: "tmp"/"textmeshpro" forces TMP, "legacy"/"text" forces legacy,
        // null auto-detects (TMP when TextMeshProUGUI resolves).
        private static (Component comp, string err) AddTextComponent(GameObject go, string engine)
        {
            string e = engine?.ToLowerInvariant();
            bool wantTmp;
            if (e == "tmp" || e == "textmeshpro" || e == "textmeshprougui") wantTmp = true;
            else if (e == "legacy" || e == "text") wantTmp = false;
            else wantTmp = ComponentTypeResolver.Resolve("TextMeshProUGUI") != null;

            if (wantTmp)
            {
                var tmp = AddByName(go, "TextMeshProUGUI");
                if (tmp != null) return (tmp, null);
                // Forced TMP but unavailable.
                if (e == "tmp" || e == "textmeshpro" || e == "textmeshprougui")
                    return (null, "TextMeshProUGUI not found — TextMeshPro package is not installed.");
            }

            var legacy = AddByName(go, "Text");
            if (legacy == null)
                return (null, "Could not add a Text component — is com.unity.ugui installed?");
            // Legacy Text ships with no font; assign Unity's built-in so it
            // renders. Fonts live in the builtin resources (not the "extra"
            // set), matching UnityEngine.UI's own DefaultControls.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) TrySetProp(legacy, "font", font);
            return (legacy, null);
        }

        // ---- get_rect ----

        public static object GetRect(JObject raw)
        {
            var p = new ToolParams(raw);
            var (rt, err) = ResolveRectTransform(p);
            if (err != null) return new ErrorResponse(err);
            return new SuccessResponse($"OK", BuildRectShape(rt));
        }

        // ---- set_anchor ----

        public static object SetAnchor(JObject raw)
        {
            var p = new ToolParams(raw);
            var (rt, err) = ResolveRectTransform(p);
            if (err != null) return new ErrorResponse(err);

            Vector2 newMin, newMax, presetPivot;
            bool havePivot = false;

            string preset = p.Get("preset");
            if (!string.IsNullOrEmpty(preset))
            {
                if (!ParsePreset(preset, out newMin, out newMax, out presetPivot, out var pErr))
                    return new ErrorResponse("INVALID_PRESET", pErr);
                havePivot = true;
            }
            else
            {
                var minToken = p.GetRaw("anchor_min");
                var maxToken = p.GetRaw("anchor_max");
                if (minToken == null || maxToken == null)
                    return new ErrorResponse("set_anchor needs either 'preset' or both 'anchor_min' and 'anchor_max'.");
                if (!TryParseVector2(minToken, out newMin, out var e1)) return new ErrorResponse($"Invalid 'anchor_min': {e1}");
                if (!TryParseVector2(maxToken, out newMax, out var e2)) return new ErrorResponse($"Invalid 'anchor_max': {e2}");
                presetPivot = rt.pivot;
            }

            bool snap = p.GetBool("snap", false);

            // Preserve the element's corners in parent-local space unless snapping.
            var parentSize = ParentSize(rt);
            var oldCornerMin = new Vector2(rt.anchorMin.x * parentSize.x, rt.anchorMin.y * parentSize.y) + rt.offsetMin;
            var oldCornerMax = new Vector2(rt.anchorMax.x * parentSize.x, rt.anchorMax.y * parentSize.y) + rt.offsetMax;

            Undo.RecordObject(rt, "Hera Set Anchor");
            rt.anchorMin = newMin;
            rt.anchorMax = newMax;

            // Explicit pivot override wins; otherwise the preset's pivot applies
            // only when snapping.
            var pivotToken = p.GetRaw("pivot");
            if (pivotToken != null && pivotToken.Type != JTokenType.Null)
            {
                if (!TryParseVector2(pivotToken, out var pv, out var pe)) return new ErrorResponse($"Invalid 'pivot': {pe}");
                rt.pivot = pv;
            }
            else if (snap && havePivot)
            {
                rt.pivot = presetPivot;
            }

            if (snap)
            {
                // Collapse onto the anchors: zero offsets so the rect fills /
                // sits exactly on the preset (Unity Alt+Shift click).
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                // Recompute offsets so the visual rect is unchanged.
                rt.offsetMin = oldCornerMin - new Vector2(rt.anchorMin.x * parentSize.x, rt.anchorMin.y * parentSize.y);
                rt.offsetMax = oldCornerMax - new Vector2(rt.anchorMax.x * parentSize.x, rt.anchorMax.y * parentSize.y);
            }

            MarkDirty(rt);
            return new SuccessResponse($"Set anchor on {rt.name}.", BuildRectShape(rt));
        }

        // ---- set_rect ----

        public static object SetRect(JObject raw)
        {
            var p = new ToolParams(raw);
            var (rt, err) = ResolveRectTransform(p);
            if (err != null) return new ErrorResponse(err);

            Undo.RecordObject(rt, "Hera Set Rect");
            bool any = false;

            var pivotToken = p.GetRaw("pivot");
            if (pivotToken != null && pivotToken.Type != JTokenType.Null)
            {
                if (!TryParseVector2(pivotToken, out var pv, out var pe)) return new ErrorResponse($"Invalid 'pivot': {pe}");
                rt.pivot = pv; any = true;
            }

            var apToken = p.GetRaw("anchored_position");
            if (apToken != null && apToken.Type != JTokenType.Null)
            {
                if (!TryParseVector2(apToken, out var ap, out var ae)) return new ErrorResponse($"Invalid 'anchored_position': {ae}");
                rt.anchoredPosition = ap; any = true;
            }

            var sdToken = p.GetRaw("size_delta");
            if (sdToken != null && sdToken.Type != JTokenType.Null)
            {
                if (!TryParseVector2(sdToken, out var sd, out var se)) return new ErrorResponse($"Invalid 'size_delta': {se}");
                rt.sizeDelta = sd; any = true;
            }

            var omToken = p.GetRaw("offset_min");
            if (omToken != null && omToken.Type != JTokenType.Null)
            {
                if (!TryParseVector2(omToken, out var om, out var oe)) return new ErrorResponse($"Invalid 'offset_min': {oe}");
                rt.offsetMin = om; any = true;
            }

            var oMaxToken = p.GetRaw("offset_max");
            if (oMaxToken != null && oMaxToken.Type != JTokenType.Null)
            {
                if (!TryParseVector2(oMaxToken, out var oMax, out var oe2)) return new ErrorResponse($"Invalid 'offset_max': {oe2}");
                rt.offsetMax = oMax; any = true;
            }

            if (!any)
                return new ErrorResponse("set_rect needs at least one of: anchored_position, size_delta, pivot, offset_min, offset_max.");

            MarkDirty(rt);
            return new SuccessResponse($"Set rect on {rt.name}.", BuildRectShape(rt));
        }

        // ---- helpers: targets ----

        private static (RectTransform rt, string err) ResolveRectTransform(ToolParams p)
        {
            var idToken = p.GetRaw("instance_id");
            if (idToken != null && idToken.Type != JTokenType.Null)
            {
                int? id = p.GetInt("instance_id");
                if (id == null) return (null, $"Invalid 'instance_id': '{idToken}'.");
                var obj = EditorUtility.InstanceIDToObject(id.Value);
                if (obj == null) return (null, $"No object for instance_id={id.Value}.");
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go == null) return (null, $"instance_id={id.Value} is not a GameObject (type={obj.GetType().Name}).");
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) return (null, $"'{go.name}' has no RectTransform (not a UI element).");
                return (rt, null);
            }

            string path = p.Get("path");
            if (!string.IsNullOrEmpty(path))
            {
                var go = HierarchyPath.Find(path);
                if (go == null) return (null, $"No GameObject at path: '{path}'.");
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) return (null, $"'{go.name}' has no RectTransform (not a UI element).");
                return (rt, null);
            }

            return (null, "Target required: pass 'instance_id' or 'path'.");
        }

        private static (Transform t, string err) ResolveTransform(string s)
        {
            if (string.IsNullOrEmpty(s)) return (null, null);
            if (int.TryParse(s, out var id))
            {
                var obj = EditorUtility.InstanceIDToObject(id);
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go == null) return (null, $"No GameObject for instance_id={id}.");
                return (go.transform, null);
            }
            var found = HierarchyPath.Find(s);
            if (found == null) return (null, $"No GameObject at path: '{s}'.");
            return (found.transform, null);
        }

        // ---- helpers: components ----

        // AddComponent by TypeCache-resolved name so the connector compiles
        // without a compile-time reference to com.unity.ugui / TextMeshPro.
        private static Component AddByName(GameObject go, string typeName)
        {
            var type = ComponentTypeResolver.Resolve(typeName);
            if (type == null) return null;
            try { return go.AddComponent(type); }
            catch { return null; }
        }

        private static void TrySetProp(Component comp, string prop, object value)
        {
            if (comp == null || value == null) return;
            var pi = comp.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            if (pi == null || !pi.CanWrite) return;
            if (!pi.PropertyType.IsInstanceOfType(value)) return;
            try { pi.SetValue(comp, value); } catch { /* best-effort default */ }
        }

        private static void SizeTo(GameObject go, float w, float h)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(w, h);
        }

        private static void Finalize(GameObject go, List<string> created)
        {
            Undo.RegisterCreatedObjectUndo(go, $"Hera Create {go.name}");
            Selection.activeGameObject = go;
            if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
        }

        private static void MarkDirty(RectTransform rt)
        {
            EditorUtility.SetDirty(rt);
            if (rt.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(rt.gameObject.scene);
        }

        private static Vector2 ParentSize(RectTransform rt)
        {
            var parent = rt.parent as RectTransform;
            return parent != null ? parent.rect.size : Vector2.zero;
        }

        // ---- helpers: anchor presets ----

        // Parses "<vertical>-<horizontal>" presets plus the full-stretch aliases.
        // horizontal ∈ {left, center, right, stretch}; vertical ∈ {top, middle,
        // bottom, stretch}. Emits anchorMin/anchorMax and the matching pivot.
        private static bool ParsePreset(string preset, out Vector2 min, out Vector2 max, out Vector2 pivot, out string err)
        {
            min = max = pivot = Vector2.zero;
            err = null;
            string s = preset.Trim().ToLowerInvariant();

            string vert, horiz;
            switch (s)
            {
                case "stretch":
                case "stretch-stretch":
                case "full":
                case "fill":
                    vert = "stretch"; horiz = "stretch"; break;
                case "center":
                case "middle":
                case "middle-center":
                    vert = "middle"; horiz = "center"; break;
                default:
                    var parts = s.Split('-');
                    if (parts.Length != 2)
                    {
                        err = $"Unknown preset '{preset}'. Use '<vertical>-<horizontal>' (e.g. top-center, middle-left, stretch-right) or 'stretch'.";
                        return false;
                    }
                    vert = parts[0]; horiz = parts[1];
                    break;
            }

            if (!AxisAnchor(horiz, true, out float minX, out float maxX, out float pivotX))
            {
                err = $"Unknown horizontal token '{horiz}' in preset '{preset}'. Use left, center, right, or stretch.";
                return false;
            }
            if (!AxisAnchor(vert, false, out float minY, out float maxY, out float pivotY))
            {
                err = $"Unknown vertical token '{vert}' in preset '{preset}'. Use top, middle, bottom, or stretch.";
                return false;
            }

            min = new Vector2(minX, minY);
            max = new Vector2(maxX, maxY);
            pivot = new Vector2(pivotX, pivotY);
            return true;
        }

        private static bool AxisAnchor(string token, bool horizontal, out float lo, out float hi, out float pivot)
        {
            lo = hi = pivot = 0f;
            switch (token)
            {
                case "left" when horizontal: lo = 0f; hi = 0f; pivot = 0f; return true;
                case "center" when horizontal: lo = 0.5f; hi = 0.5f; pivot = 0.5f; return true;
                case "right" when horizontal: lo = 1f; hi = 1f; pivot = 1f; return true;
                case "bottom" when !horizontal: lo = 0f; hi = 0f; pivot = 0f; return true;
                case "middle" when !horizontal: lo = 0.5f; hi = 0.5f; pivot = 0.5f; return true;
                case "top" when !horizontal: lo = 1f; hi = 1f; pivot = 1f; return true;
                case "stretch": lo = 0f; hi = 1f; pivot = 0.5f; return true;
                default: return false;
            }
        }

        // ---- helpers: shapes ----

        private static object BuildRectShape(RectTransform rt)
        {
            return new
            {
                instance_id = rt.gameObject.GetInstanceID(),
                name = rt.name,
                path = HierarchyPath.Build(rt),
                rect = new
                {
                    anchor_min = V2(rt.anchorMin),
                    anchor_max = V2(rt.anchorMax),
                    anchored_position = V2(rt.anchoredPosition),
                    size_delta = V2(rt.sizeDelta),
                    pivot = V2(rt.pivot),
                    offset_min = V2(rt.offsetMin),
                    offset_max = V2(rt.offsetMax),
                    size = new { width = rt.rect.width, height = rt.rect.height },
                },
                preset = DetectPreset(rt.anchorMin, rt.anchorMax),
            };
        }

        private static object BuildCreateShape(GameObject go, List<string> created)
        {
            var rt = go.GetComponent<RectTransform>();
            return new
            {
                instance_id = go.GetInstanceID(),
                name = go.name,
                path = HierarchyPath.Build(go.transform),
                scene = go.scene.name,
                components = ComponentNames(go),
                created = created.ToArray(),
                rect = rt != null ? BuildRectShape(rt) : null,
            };
        }

        private static string[] ComponentNames(GameObject go)
        {
            var comps = go.GetComponents<Component>();
            var names = new List<string>(comps.Length);
            foreach (var c in comps)
                if (c != null) names.Add(c.GetType().Name);
            return names.ToArray();
        }

        private static object V2(Vector2 v) => new { x = v.x, y = v.y };

        // Reverse-maps anchorMin/Max to a preset name, or "custom".
        private static string DetectPreset(Vector2 min, Vector2 max)
        {
            string horiz = AxisName(min.x, max.x, true);
            string vert = AxisName(min.y, max.y, false);
            if (horiz == null || vert == null) return "custom";
            if (horiz == "stretch" && vert == "stretch") return "stretch";
            return $"{vert}-{horiz}";
        }

        private static string AxisName(float lo, float hi, bool horizontal)
        {
            const float e = 0.0001f;
            if (Approximately(lo, 0f, e) && Approximately(hi, 1f, e)) return "stretch";
            if (!Approximately(lo, hi, e)) return null;
            if (horizontal)
            {
                if (Approximately(lo, 0f, e)) return "left";
                if (Approximately(lo, 0.5f, e)) return "center";
                if (Approximately(lo, 1f, e)) return "right";
            }
            else
            {
                if (Approximately(lo, 0f, e)) return "bottom";
                if (Approximately(lo, 0.5f, e)) return "middle";
                if (Approximately(lo, 1f, e)) return "top";
            }
            return null;
        }

        private static bool Approximately(float a, float b, float epsilon) => Mathf.Abs(a - b) <= epsilon;

        private static bool TryParseVector2(JToken token, out Vector2 v, out string err)
        {
            v = Vector2.zero;
            err = null;
            if (token == null || token.Type == JTokenType.Null) { err = "null"; return false; }

            if (SerializedPropertyValue.TryParseFloats(token, 2, out var f, out var fErr))
            {
                v = new Vector2(f[0], f[1]);
                return true;
            }
            err = fErr;
            return false;
        }
    }
}
