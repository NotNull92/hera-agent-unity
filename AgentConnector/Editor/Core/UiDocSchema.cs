using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
// `using UnityEditor;` + `using UnityEngine;` leave `Object` ambiguous; alias to
// the engine type so bare Object here is UnityEngine.Object (CS0104 trap).
using Object = UnityEngine.Object;

namespace HeraAgent
{
    /// <summary>
    /// The ui_doc IR (schema "ui_doc/1"): a compact, defaults-omitted JSON tree
    /// that round-trips a uGUI subtree. <see cref="ExportNode"/> serializes the
    /// current state (export); <see cref="ApplyNode"/> builds it (always-create,
    /// MVP). Reuses Core utilities (ComponentTypeResolver, SerializedPropertyValue)
    /// so the connector stays compile-free of com.unity.ugui / TextMeshPro.
    ///
    /// The anchor-preset grid and element-build helpers are replicated minimally
    /// from ManageUI (ui_doc is the 2nd consumer); extract to Core at the 3rd per
    /// the project's replicate-then-extract convention.
    /// </summary>
    public static class UiDocSchema
    {
        public const string SchemaId = "ui_doc/1";

        /// <summary>Accumulates apply results for a compact, token-disciplined summary.</summary>
        public class ApplyStats
        {
            public int Created;
            public int Sprites;
            public readonly List<string> Errors = new List<string>();
            public readonly HashSet<string> ElementTypes = new HashSet<string>();
        }

        // =====================================================================
        // EXPORT  (Transform → IR node, defaults omitted)
        // =====================================================================

        public static JObject ExportNode(Transform t, int depth)
        {
            var go = t.gameObject;
            var node = new JObject
            {
                ["name"] = go.name,
                ["element"] = DetectElement(go),
            };

            if (t is RectTransform rt)
            {
                var rect = ExportRect(rt);
                if (rect.Count > 0) node["rect"] = rect;
            }

            var img = ExportImage(go);
            if (img != null) node["image"] = img;
            var text = ExportText(go);
            if (text != null) node["text"] = text;

            if (depth > 0 && t.childCount > 0)
            {
                var children = new JArray();
                for (int i = 0; i < t.childCount; i++)
                    children.Add(ExportNode(t.GetChild(i), depth - 1));
                node["children"] = children;
            }
            return node;
        }

        static JObject ExportRect(RectTransform rt)
        {
            var o = new JObject();
            string preset = AnchorPreset.Detect(rt.anchorMin, rt.anchorMax);
            if (preset != null) o["anchor"] = preset;
            else { o["anchor_min"] = Vec(rt.anchorMin); o["anchor_max"] = Vec(rt.anchorMax); }

            if (rt.anchoredPosition != Vector2.zero) o["pos"] = Vec(rt.anchoredPosition);
            if (rt.sizeDelta != Vector2.zero) o["size"] = Vec(rt.sizeDelta);
            if (rt.pivot != new Vector2(0.5f, 0.5f)) o["pivot"] = Vec(rt.pivot);
            return o;
        }

        static JObject ExportImage(GameObject go)
        {
            var img = GetComponentByName(go, "Image");
            if (img == null) return null;
            var o = new JObject();
            if (GetProp(img, "color") is Color c && c != Color.white)
                o["color"] = "#" + ColorUtility.ToHtmlStringRGBA(c);
            if (GetProp(img, "sprite") is Object spr && spr != null)
            {
                var path = AssetDatabase.GetAssetPath(spr);
                if (!string.IsNullOrEmpty(path)) o["sprite"] = new JObject { ["asset"] = path };
            }
            return o.Count > 0 ? o : null;
        }

        static JObject ExportText(GameObject go)
        {
            var txt = GetComponentByName(go, "TextMeshProUGUI") ?? GetComponentByName(go, "Text");
            if (txt == null) return null;
            if (!(GetProp(txt, "text") is string val) || string.IsNullOrEmpty(val)) return null;
            return new JObject
            {
                ["value"] = val,
                ["engine"] = txt.GetType().Name == "Text" ? "legacy" : "tmp",
            };
        }

        static string DetectElement(GameObject go)
        {
            if (go.GetComponent<Canvas>() != null) return "canvas";
            if (GetComponentByName(go, "Button") != null) return "button";
            if (GetComponentByName(go, "TextMeshProUGUI") != null || GetComponentByName(go, "Text") != null) return "text";
            if (GetComponentByName(go, "Image") != null) return "image";
            return "empty";
        }

        // =====================================================================
        // APPLY  (IR node → GameObject tree, always-create)
        // =====================================================================

        /// <summary>Builds the node (and its children) under <paramref name="parent"/>. Returns the created root GameObject.</summary>
        public static GameObject ApplyNode(JObject node, Transform parent, ApplyStats stats)
        {
            if (node == null) return null;
            string element = (node["element"]?.ToString() ?? "empty").ToLowerInvariant();
            string name = node["name"]?.ToString();

            var go = BuildElement(element, name, node, stats);
            go.transform.SetParent(parent, worldPositionStays: false);

            if (node["rect"] is JObject rect) ApplyRect(go, rect);
            ApplyImage(go, node["image"] as JObject, name, stats);
            ApplyText(go, node["text"] as JObject, element, stats);

            Undo.RegisterCreatedObjectUndo(go, "Hera ui_doc apply");
            stats.Created++;
            stats.ElementTypes.Add(element);

            if (node["children"] is JArray children)
                foreach (var child in children)
                    if (child is JObject co) ApplyNode(co, go.transform, stats);

            return go;
        }

        static GameObject BuildElement(string element, string name, JObject node, ApplyStats stats)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? Capitalize(element) : name, typeof(RectTransform));
            switch (element)
            {
                case "empty":
                    SizeTo(go, 100, 100);
                    break;
                case "canvas":
                    var canvas = go.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    AddByName(go, "CanvasScaler");
                    AddByName(go, "GraphicRaycaster");
                    break;
                case "image":
                    AddOrError(go, "Image", stats);
                    SizeTo(go, 100, 100);
                    break;
                case "panel":
                    var pimg = AddOrError(go, "Image", stats);
                    if (pimg != null) SetProp(pimg, "color", new Color(1f, 1f, 1f, 0.39f));
                    Stretch(go);
                    break;
                case "text":
                    AddTextComponent(go, node?["text"]?["engine"]?.ToString(), stats);
                    SizeTo(go, 200, 50);
                    break;
                case "button":
                    var bimg = AddOrError(go, "Image", stats);
                    var btn = AddOrError(go, "Button", stats);
                    if (btn != null && bimg != null) SetProp(btn, "targetGraphic", bimg);
                    SizeTo(go, 160, 40);
                    break;
                default:
                    stats.Errors.Add($"unknown element '{element}' for node '{name}' — created empty.");
                    SizeTo(go, 100, 100);
                    break;
            }
            return go;
        }

        static void ApplyRect(GameObject go, JObject rect)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) return;

            var anchor = rect["anchor"]?.ToString();
            if (!string.IsNullOrEmpty(anchor) && AnchorPreset.TryParse(anchor, out var amin, out var amax, out var piv))
            {
                rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = piv;
            }
            else
            {
                if (rect["anchor_min"] != null && SerializedPropertyValue.TryParseFloats(rect["anchor_min"], 2, out var mn, out _))
                    rt.anchorMin = new Vector2(mn[0], mn[1]);
                if (rect["anchor_max"] != null && SerializedPropertyValue.TryParseFloats(rect["anchor_max"], 2, out var mx, out _))
                    rt.anchorMax = new Vector2(mx[0], mx[1]);
            }

            if (rect["pivot"] != null && SerializedPropertyValue.TryParseFloats(rect["pivot"], 2, out var pv, out _))
                rt.pivot = new Vector2(pv[0], pv[1]);
            if (rect["size"] != null && SerializedPropertyValue.TryParseFloats(rect["size"], 2, out var sd, out _))
                rt.sizeDelta = new Vector2(sd[0], sd[1]);
            // anchoredPosition depends on anchors/pivot, so set it last.
            if (rect["pos"] != null && SerializedPropertyValue.TryParseFloats(rect["pos"], 2, out var ap, out _))
                rt.anchoredPosition = new Vector2(ap[0], ap[1]);
        }

        static void ApplyImage(GameObject go, JObject image, string name, ApplyStats stats)
        {
            if (image == null) return;
            var img = GetComponentByName(go, "Image");
            if (img == null) return;

            if (image["color"] is JToken colorTok && SerializedPropertyValue.TryParseColor(colorTok, out var c, out _))
                SetProp(img, "color", c);

            if (image["sprite"] is JObject sprite)
            {
                string assetPath = sprite["asset"]?.ToString();
                if (string.IsNullOrEmpty(assetPath) && sprite["gen"] is JObject gen)
                {
                    var (p, err) = ProceduralSprite.Generate(gen, null);
                    if (err != null) stats.Errors.Add($"sprite gen for '{name}': {err}");
                    else { assetPath = p; stats.Sprites++; }
                }
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var spr = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (spr != null) SetProp(img, "sprite", spr);
                    else stats.Errors.Add($"sprite asset not found for '{name}': {assetPath}");
                }
            }
        }

        static void ApplyText(GameObject go, JObject text, string element, ApplyStats stats)
        {
            if (text == null) return;
            string value = text["value"]?.ToString();
            string engine = text["engine"]?.ToString();

            if (element == "button")
            {
                // Button label is a stretched child (mirrors ManageUI.BuildButton).
                var labelGo = new GameObject("Text", typeof(RectTransform));
                var txt = AddTextComponent(labelGo, engine, stats);
                if (txt != null && value != null) SetProp(txt, "text", value);
                labelGo.transform.SetParent(go.transform, worldPositionStays: false);
                Stretch(labelGo);
            }
            else
            {
                var txt = GetComponentByName(go, "TextMeshProUGUI") ?? GetComponentByName(go, "Text");
                if (txt != null && value != null) SetProp(txt, "text", value);
            }
        }

        // ---- element-build helpers (replicated minimal from ManageUI) ----

        static Component AddTextComponent(GameObject go, string engine, ApplyStats stats)
        {
            string e = engine?.ToLowerInvariant();
            bool wantTmp;
            if (e == "tmp" || e == "textmeshpro" || e == "textmeshprougui") wantTmp = true;
            else if (e == "legacy" || e == "text") wantTmp = false;
            else wantTmp = ComponentTypeResolver.Resolve("TextMeshProUGUI") != null;

            if (wantTmp)
            {
                var tmp = AddByName(go, "TextMeshProUGUI");
                if (tmp != null) return tmp;
            }
            var legacy = AddByName(go, "Text");
            if (legacy == null) { stats.Errors.Add($"could not add a Text component on '{go.name}' (is com.unity.ugui installed?)."); return null; }
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) SetProp(legacy, "font", font);
            return legacy;
        }

        static Component AddOrError(GameObject go, string typeName, ApplyStats stats)
        {
            var comp = AddByName(go, typeName);
            if (comp == null) stats.Errors.Add($"could not add {typeName} on '{go.name}' (is com.unity.ugui installed?).");
            return comp;
        }

        static Component AddByName(GameObject go, string typeName)
        {
            var type = ComponentTypeResolver.Resolve(typeName);
            if (type == null) return null;
            try { return go.AddComponent(type); }
            catch { return null; }
        }

        static Component GetComponentByName(GameObject go, string typeName)
        {
            var type = ComponentTypeResolver.Resolve(typeName);
            return type == null ? null : go.GetComponent(type);
        }

        static void SizeTo(GameObject go, float w, float h)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(w, h);
        }

        static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? "UIElement" : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ---- reflection property get/set (matches ManageUI's approach) ----

        static object GetProp(Component comp, string prop)
        {
            if (comp == null) return null;
            var pi = comp.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            if (pi == null || !pi.CanRead) return null;
            try { return pi.GetValue(comp); }
            catch { return null; }
        }

        static void SetProp(Component comp, string prop, object value)
        {
            if (comp == null || value == null) return;
            var pi = comp.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            if (pi == null || !pi.CanWrite) return;
            if (!pi.PropertyType.IsInstanceOfType(value)) return;
            try { pi.SetValue(comp, value); }
            catch { /* best-effort */ }
        }

        static JArray Vec(Vector2 v) => new JArray { Rnd(v.x), Rnd(v.y) };
        static double Rnd(float f) => System.Math.Round(f, 3);

        // ---- anchor presets (replicated minimal from ManageUI; extract at 3rd consumer) ----

        static class AnchorPreset
        {
            public static bool TryParse(string name, out Vector2 min, out Vector2 max, out Vector2 pivot)
            {
                min = max = pivot = new Vector2(0.5f, 0.5f);
                if (string.IsNullOrEmpty(name)) return false;
                string s = name.Trim().ToLowerInvariant();
                string vert, horiz;
                switch (s)
                {
                    case "stretch":
                    case "full":
                    case "fill":
                        vert = "stretch"; horiz = "stretch"; break;
                    case "center":
                    case "middle":
                        vert = "middle"; horiz = "center"; break;
                    default:
                        var parts = s.Split('-');
                        if (parts.Length != 2) return false;
                        vert = parts[0]; horiz = parts[1]; break;
                }
                if (!Axis(horiz, true, out float minX, out float maxX, out float pvX)) return false;
                if (!Axis(vert, false, out float minY, out float maxY, out float pvY)) return false;
                min = new Vector2(minX, minY);
                max = new Vector2(maxX, maxY);
                pivot = new Vector2(pvX, pvY);
                return true;
            }

            static bool Axis(string token, bool horizontal, out float lo, out float hi, out float pivot)
            {
                lo = hi = pivot = 0.5f;
                switch (token)
                {
                    case "left" when horizontal: lo = hi = pivot = 0f; return true;
                    case "center" when horizontal: lo = hi = pivot = 0.5f; return true;
                    case "right" when horizontal: lo = hi = pivot = 1f; return true;
                    case "bottom" when !horizontal: lo = hi = pivot = 0f; return true;
                    case "middle" when !horizontal: lo = hi = pivot = 0.5f; return true;
                    case "top" when !horizontal: lo = hi = pivot = 1f; return true;
                    case "stretch": lo = 0f; hi = 1f; pivot = 0.5f; return true;
                    default: return false;
                }
            }

            public static string Detect(Vector2 min, Vector2 max)
            {
                string h = AxisName(min.x, max.x, true);
                string v = AxisName(min.y, max.y, false);
                if (h == null || v == null) return null;
                if (h == "stretch" && v == "stretch") return "stretch";
                return v + "-" + h;
            }

            static string AxisName(float lo, float hi, bool horizontal)
            {
                const float e = 0.0001f;
                if (Approx(lo, 0f, e) && Approx(hi, 1f, e)) return "stretch";
                if (!Approx(lo, hi, e)) return null;
                if (horizontal)
                {
                    if (Approx(lo, 0f, e)) return "left";
                    if (Approx(lo, 0.5f, e)) return "center";
                    if (Approx(lo, 1f, e)) return "right";
                }
                else
                {
                    if (Approx(lo, 0f, e)) return "bottom";
                    if (Approx(lo, 0.5f, e)) return "middle";
                    if (Approx(lo, 1f, e)) return "top";
                }
                return null;
            }

            static bool Approx(float a, float b, float e) => Mathf.Abs(a - b) <= e;
        }
    }
}
