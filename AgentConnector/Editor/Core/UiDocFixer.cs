using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HeraAgent
{
    public static class UiDocFixer
    {
        public class Profile
        {
            public string docs_version;
            public string ugui_package;
            public string manual_url;
        }

        public class Report
        {
            public string rule;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string severity;
            public string path;
            public string message;
        }

        public static Profile CurrentProfile()
        {
            return ProfileForDocsVersion(UnityVersionCompat.CurrentDocsVersion());
        }

        internal static Profile ProfileForDocsVersion(string docs)
        {
            var pkg = docs == UnityVersionCompat.Docs2022_3 ? "com.unity.ugui@1.0"
                : docs == UnityVersionCompat.Docs6000_5 ? "com.unity.ugui@2.5"
                : "com.unity.ugui@2.0";
            return new Profile
            {
                docs_version = docs,
                ugui_package = pkg,
                manual_url = "https://docs.unity3d.com/Packages/" + pkg + "/manual/index.html",
            };
        }

        public static void PreflightDocument(JObject doc, IList<Report> diagnostics)
        {
            if (doc == null) return;
            var canvas = doc["canvas"] as JObject;
            if ((canvas == null || canvas["reference_resolution"] == null) && HasFixedRect(doc["root"] as JObject))
            {
                Add(diagnostics, "canvas_scaler.no_reference_resolution", "warning", "/",
                    "ui_doc uses fixed rect sizes but has no canvas.reference_resolution; Scale With Screen Size cannot preserve the source design size deterministically.");
            }
        }

        public static void NormalizeNode(JObject node, RectTransform parent, string path, IList<Report> fixes, IList<Report> diagnostics)
        {
            if (node == null) return;
            if (node["rect"] is JObject rect) NormalizeRect(rect, parent, path, fixes, diagnostics);
            if (node["image"] is JObject image) NormalizeImage(image, path, fixes, diagnostics);
        }

        public static void DiagnoseGameObject(GameObject go, string path, IList<Report> diagnostics)
        {
            if (go == null) return;
            DiagnoseCanvas(go, path, diagnostics);
            DiagnoseScrollRect(go, path, diagnostics);
            DiagnoseLayoutConflict(go, path, diagnostics);
            DiagnoseImage(go, path, diagnostics);
        }

        static void NormalizeRect(JObject rect, RectTransform parent, string path, IList<Report> fixes, IList<Report> diagnostics)
        {
            if (!TryResolveRect(rect, out var min, out var max, out var pivot)) return;
            if (rect["pivot"] != null && TryFloats(rect["pivot"], 2, out var pv))
                pivot = new Vector2(pv[0], pv[1]);

            bool stretchX = !Approx(min.x, max.x);
            bool stretchY = !Approx(min.y, max.y);
            bool hasOffsets = rect["offset_min"] != null || rect["offset_max"] != null;
            bool hasPos = rect["pos"] != null;
            bool hasSize = rect["size"] != null;

            if (stretchX && stretchY && !hasOffsets && !hasPos && !hasSize)
            {
                rect["offset_min"] = Vec(Vector2.zero);
                rect["offset_max"] = Vec(Vector2.zero);
                Add(fixes, "rect.full_stretch_zero", null, path, "Added zero offsets for a full-stretch RectTransform.");
                return;
            }

            if (!(stretchX || stretchY) || hasOffsets) return;
            if (!hasSize)
            {
                Add(diagnostics, "rect.stretch_offsets", "warning", path,
                    "Stretched RectTransform has pos but no size; cannot convert to official offset fields safely.");
                return;
            }
            if (parent == null || parent.rect.size == Vector2.zero)
            {
                Add(diagnostics, "rect.stretch_offsets", "warning", path,
                    "Stretched RectTransform uses size/pos but parent size is unavailable; kept the original fields.");
                return;
            }

            if (!TryFloats(rect["size"], 2, out var size))
            {
                Add(diagnostics, "rect.stretch_offsets", "warning", path,
                    "Stretched RectTransform size could not be parsed; kept the original fields.");
                return;
            }
            var pos = new[] { 0f, 0f };
            if (hasPos) TryFloats(rect["pos"], 2, out pos);

            var parentSize = parent.rect.size;
            var offMin = new Vector2(
                OffsetMin(pos[0], size[0], pivot.x, (max.x - min.x) * parentSize.x),
                OffsetMin(pos[1], size[1], pivot.y, (max.y - min.y) * parentSize.y));
            var offMax = new Vector2(
                offMin.x + size[0] - (max.x - min.x) * parentSize.x,
                offMin.y + size[1] - (max.y - min.y) * parentSize.y);

            rect["offset_min"] = Vec(offMin);
            rect["offset_max"] = Vec(offMax);
            Add(fixes, "rect.stretch_offsets", null, path,
                "Converted stretched RectTransform size/pos to official offset_min/offset_max fields.");
        }

        static void NormalizeImage(JObject image, string path, IList<Report> fixes, IList<Report> diagnostics)
        {
            if (image["fill"] is JObject && image["type"] == null)
            {
                image["type"] = "filled";
                Add(fixes, "image.fill_type", null, path, "Set Image.type to Filled because image.fill is present.");
            }
            if (image["type"] != null && image["type"].ToString().ToLowerInvariant() == "filled" && !(image["fill"] is JObject))
            {
                Add(diagnostics, "image.progress_fill", "warning", path,
                    "Image.type is Filled but image.fill is missing; fillAmount/method/origin will remain defaults.");
            }
        }

        static void DiagnoseCanvas(GameObject go, string path, IList<Report> diagnostics)
        {
            if (go.GetComponent<Canvas>() == null) return;
            if (GetComponent(go, "GraphicRaycaster") == null)
                Add(diagnostics, "canvas.graphic_raycaster", "warning", path, "Canvas has no GraphicRaycaster, so UI input will not hit this Canvas.");
        }

        static void DiagnoseScrollRect(GameObject go, string path, IList<Report> diagnostics)
        {
            var sr = GetComponent(go, "ScrollRect");
            if (sr == null) return;
            var content = GetProp(sr, "content") as RectTransform;
            var viewport = GetProp(sr, "viewport") as RectTransform;
            if (content == null)
                Add(diagnostics, "scrollrect.missing_content", "error", path, "ScrollRect.content is not assigned.");
            if (viewport == null)
            {
                Add(diagnostics, "scrollrect.missing_viewport", "warning", path, "ScrollRect.viewport is not assigned.");
                return;
            }
            var vgo = viewport.gameObject;
            if (GetComponent(vgo, "Mask") == null && GetComponent(vgo, "RectMask2D") == null)
                Add(diagnostics, "scrollrect.missing_viewport_clip", "error", path, "ScrollRect viewport has no Mask or RectMask2D.");
        }

        static void DiagnoseLayoutConflict(GameObject go, string path, IList<Report> diagnostics)
        {
            if (GetComponent(go, "ContentSizeFitter") == null) return;
            var parent = go.transform.parent != null ? go.transform.parent.gameObject : null;
            if (parent == null) return;
            if (HasLayoutGroup(parent))
                Add(diagnostics, "fit.child_layout_conflict", "warning", path,
                    "ContentSizeFitter is on a child controlled by a parent LayoutGroup; official uGUI docs describe this as a layout-control conflict.");
        }

        static void DiagnoseImage(GameObject go, string path, IList<Report> diagnostics)
        {
            var profile = CurrentProfile();
            if (profile.docs_version != UnityVersionCompat.Docs6000_5) return;
            var img = GetComponent(go, "Image");
            if (img == null) return;
            if (GetProp(img, "color") is Color c && c.a <= 0.001f && GetProp(img, "raycastTarget") is bool ray && ray)
                Add(diagnostics, "image.transparent_hit_zone", "info", path,
                    "In uGUI 2.5, Raycast Receiver is documented for invisible hit zones and may be preferable to a transparent Image.");
        }

        static bool HasFixedRect(JObject node)
        {
            if (node == null) return false;
            if (node["rect"] is JObject rect && rect["size"] != null) return true;
            if (node["children"] is JArray children)
                foreach (var child in children)
                    if (HasFixedRect(child as JObject)) return true;
            return false;
        }

        static bool TryResolveRect(JObject rect, out Vector2 min, out Vector2 max, out Vector2 pivot)
        {
            min = max = pivot = new Vector2(0.5f, 0.5f);
            var anchor = rect["anchor"]?.ToString();
            if (!string.IsNullOrEmpty(anchor) && TryParsePreset(anchor, out min, out max, out pivot)) return true;
            if (TryFloats(rect["anchor_min"], 2, out var mn) && TryFloats(rect["anchor_max"], 2, out var mx))
            {
                min = new Vector2(mn[0], mn[1]);
                max = new Vector2(mx[0], mx[1]);
                return true;
            }
            return false;
        }

        static bool TryParsePreset(string preset, out Vector2 min, out Vector2 max, out Vector2 pivot)
        {
            min = max = pivot = new Vector2(0.5f, 0.5f);
            var s = preset.Trim().ToLowerInvariant();
            string vert, horiz;
            if (s == "stretch" || s == "full" || s == "fill") { vert = "stretch"; horiz = "stretch"; }
            else if (s == "center" || s == "middle") { vert = "middle"; horiz = "center"; }
            else
            {
                var p = s.Split('-');
                if (p.Length != 2) return false;
                vert = p[0]; horiz = p[1];
            }
            if (!Axis(horiz, true, out var minX, out var maxX, out var pivX)) return false;
            if (!Axis(vert, false, out var minY, out var maxY, out var pivY)) return false;
            min = new Vector2(minX, minY);
            max = new Vector2(maxX, maxY);
            pivot = new Vector2(pivX, pivY);
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

        static bool HasLayoutGroup(GameObject go) =>
            GetComponent(go, "HorizontalLayoutGroup") != null || GetComponent(go, "VerticalLayoutGroup") != null || GetComponent(go, "GridLayoutGroup") != null;

        static Component GetComponent(GameObject go, string typeName)
        {
            var type = ComponentTypeResolver.Resolve(typeName);
            return type == null ? null : go.GetComponent(type);
        }

        static object GetProp(Component comp, string prop)
        {
            var pi = comp?.GetType().GetProperty(prop);
            if (pi == null || !pi.CanRead) return null;
            try { return pi.GetValue(comp); } catch { return null; }
        }

        static bool TryFloats(JToken token, int count, out float[] values)
        {
            values = null;
            return token != null && SerializedPropertyValue.TryParseFloats(token, count, out values, out _);
        }

        static float OffsetMin(float pos, float size, float pivot, float anchorSize) =>
            pos - (size - anchorSize) * pivot;

        static void Add(IList<Report> list, string rule, string severity, string path, string message)
        {
            if (list == null) return;
            list.Add(new Report { rule = rule, severity = severity, path = path, message = message });
        }

        static JArray Vec(Vector2 v) => new JArray { System.Math.Round(v.x, 3), System.Math.Round(v.y, 3) };
        static bool Approx(float a, float b) => Mathf.Abs(a - b) < 0.0001f;
    }
}
