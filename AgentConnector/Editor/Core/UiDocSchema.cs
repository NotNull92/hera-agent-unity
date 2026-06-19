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
    /// The ui_doc IR (schema "ui_doc/2"): a compact, defaults-omitted JSON tree
    /// that round-trips a uGUI subtree. <see cref="ExportNode"/> serializes the
    /// current state (export); <see cref="ApplyNode"/> builds it (create or upsert).
    /// Mirrors uGUI's serialized model: rect (both anchor modes incl. stretch
    /// offsets), Image (type/Filled fill/extras), text, and the layout system
    /// (Horizontal/Vertical/GridLayoutGroup, LayoutElement, ContentSizeFitter).
    /// See docs/UI_DOC_IR.md. Reuses Core utilities (ComponentTypeResolver,
    /// SerializedPropertyValue) so the connector stays compile-free of
    /// com.unity.ugui / TextMeshPro — all uGUI types resolve at runtime.
    ///
    /// The anchor-preset grid and element-build helpers are replicated minimally
    /// from ManageUI (ui_doc is the 2nd consumer); extract to Core at the 3rd per
    /// the project's replicate-then-extract convention.
    /// </summary>
    public static class UiDocSchema
    {
        public const string SchemaId = "ui_doc/2";

        /// <summary>Accumulates apply results for a compact, token-disciplined summary.</summary>
        public class ApplyStats
        {
            public int Created;
            public int Updated;
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
            if (rt.pivot != new Vector2(0.5f, 0.5f)) o["pivot"] = Vec(rt.pivot);

            // On a stretched axis sizeDelta is padding, not size — emit offsets so
            // the rect round-trips exactly. Non-stretched: emit pos/size.
            bool stretched = rt.anchorMin.x != rt.anchorMax.x || rt.anchorMin.y != rt.anchorMax.y;
            if (stretched)
            {
                o["offset_min"] = Vec(rt.offsetMin);
                o["offset_max"] = Vec(rt.offsetMax);
            }
            else
            {
                if (rt.anchoredPosition != Vector2.zero) o["pos"] = Vec(rt.anchoredPosition);
                if (rt.sizeDelta != Vector2.zero) o["size"] = Vec(rt.sizeDelta);
            }
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

            var typeObj = GetProp(img, "type");
            string typeName = typeObj?.ToString();
            if (typeName != null && typeName != "Simple") o["type"] = typeName.ToLowerInvariant();
            if (typeName == "Filled")
            {
                var fo = new JObject();
                if (GetProp(img, "fillAmount") is float fa) fo["amount"] = System.Math.Round(fa, 3);
                if (GetProp(img, "fillMethod") is object fm) fo["method"] = fm.ToString().ToLowerInvariant();
                if (GetProp(img, "fillOrigin") is int forig) fo["origin"] = forig;
                if (GetProp(img, "fillClockwise") is bool fcw) fo["clockwise"] = fcw;
                o["fill"] = fo;
            }
            return o.Count > 0 ? o : null;
        }

        static JObject ExportText(GameObject go)
        {
            var txt = GetComponentByName(go, "TextMeshProUGUI") ?? GetComponentByName(go, "Text");
            if (txt == null) return null;
            if (!(GetProp(txt, "text") is string val) || string.IsNullOrEmpty(val)) return null;
            var o = new JObject
            {
                ["value"] = val,
                ["engine"] = txt.GetType().Name == "Text" ? "legacy" : "tmp",
            };
            if (GetProp(txt, "color") is Color tc && tc != Color.white)
                o["color"] = "#" + ColorUtility.ToHtmlStringRGBA(tc);
            return o;
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

        /// <summary>
        /// Realizes the node (and its children) under <paramref name="parent"/>.
        /// create mode always makes new objects; upsert matches an existing child
        /// by name and updates its rect/graphic/text in place (no component or
        /// element-type changes, no deletion of objects absent from the doc).
        /// Returns the realized root GameObject.
        /// </summary>
        public static GameObject ApplyNode(JObject node, Transform parent, ApplyStats stats, bool upsert, JObject canvasConfig = null)
        {
            if (node == null) return null;
            string element = (node["element"]?.ToString() ?? "empty").ToLowerInvariant();
            string name = node["name"]?.ToString();

            GameObject go = null;
            if (upsert && !string.IsNullOrEmpty(name))
                go = FindChildByName(parent, name);

            if (go == null)
            {
                go = BuildElement(element, name, node, stats, canvasConfig);
                go.transform.SetParent(parent, worldPositionStays: false);
                Undo.RegisterCreatedObjectUndo(go, "Hera ui_doc apply");
                stats.Created++;
            }
            else
            {
                Undo.RecordObject(go.transform, "Hera ui_doc upsert");
                stats.Updated++;
            }

            if (node["rect"] is JObject rect) ApplyRect(go, rect);
            ApplyImage(go, node["image"] as JObject, name, stats);
            ApplyText(go, node["text"] as JObject, element, stats);
            // Layout group goes on before children so they auto-arrange as created.
            ApplyLayout(go, node);

            // A filled image is the idiomatic progress / HP bar — surface the
            // bar-specific juice recipe (instant drop + delayed chip bar, segment
            // ticks) instead of the generic image one.
            stats.ElementTypes.Add(element == "image" && IsBarImage(node["image"] as JObject) ? "bar" : element);

            // Canvas scaler config applies to the root canvas only; nested canvases
            // receive null so they keep default scaler settings.
            if (node["children"] is JArray children)
                foreach (var child in children)
                    if (child is JObject co) ApplyNode(co, go.transform, stats, upsert, null);

            return go;
        }

        // A filled Image (image.fill present, or image.type == "filled") is a
        // progress / health / damage bar rather than a plain graphic.
        static bool IsBarImage(JObject image)
        {
            if (image == null) return false;
            if (image["fill"] is JObject) return true;
            var t = image["type"]?.ToString();
            return t != null && t.ToLowerInvariant() == "filled";
        }

        static GameObject BuildElement(string element, string name, JObject node, ApplyStats stats, JObject canvasConfig = null)
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
                    if (canvasConfig != null) ApplyCanvasConfig(go, canvasConfig);
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

        static void ApplyCanvasConfig(GameObject go, JObject config)
        {
            var scaler = GetComponentByName(go, "CanvasScaler");
            if (scaler == null) return;
            var type = scaler.GetType();

            try
            {
                var scaleModeProp = type.GetProperty("uiScaleMode");
                if (scaleModeProp != null && config["scale_mode"] is JValue sm)
                {
                    var modeStr = sm.ToString().ToLowerInvariant().Replace("_", "");
                    var enumType = scaleModeProp.PropertyType;
                    object enumValue = null;
                    foreach (var name in Enum.GetNames(enumType))
                    {
                        if (name.ToLowerInvariant() == modeStr)
                        {
                            enumValue = Enum.Parse(enumType, name);
                            break;
                        }
                    }
                    if (enumValue != null) scaleModeProp.SetValue(scaler, enumValue);
                }

                var refResProp = type.GetProperty("referenceResolution");
                if (refResProp != null && config["reference_resolution"] is JArray rr && rr.Count >= 2)
                {
                    var vec = Activator.CreateInstance(typeof(Vector2), rr[0].Value<float>(), rr[1].Value<float>());
                    refResProp.SetValue(scaler, vec);
                }

                var matchProp = type.GetProperty("matchWidthOrHeight");
                if (matchProp != null && config["match"] is JValue m)
                    matchProp.SetValue(scaler, m.Value<float>());

                var scaleFactorProp = type.GetProperty("scaleFactor");
                if (scaleFactorProp != null && config["scale_factor"] is JValue sf)
                    scaleFactorProp.SetValue(scaler, sf.Value<float>());

                var ppuProp = type.GetProperty("referencePixelsPerUnit");
                if (ppuProp != null && config["reference_pixels_per_unit"] is JValue rp)
                    ppuProp.SetValue(scaler, rp.Value<float>());
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Hera] Failed to apply canvas config to '{go.name}': {ex.Message}");
            }
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

            // Stretched anchors: offsets are authoritative (Left/Bottom, Right/Top).
            // Set after size/pos so they win when both are present.
            if (rect["offset_min"] != null && SerializedPropertyValue.TryParseFloats(rect["offset_min"], 2, out var omn, out _))
                rt.offsetMin = new Vector2(omn[0], omn[1]);
            if (rect["offset_max"] != null && SerializedPropertyValue.TryParseFloats(rect["offset_max"], 2, out var omx, out _))
                rt.offsetMax = new Vector2(omx[0], omx[1]);
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
                    if (spr != null)
                    {
                        SetProp(img, "sprite", spr);
                        // A bordered sprite (nine_slice) only renders correctly with
                        // Image.type = Sliced; Simple (the default) stretches the
                        // corners into an oval. Set it via reflection so the connector
                        // keeps no compile-time com.unity.ugui dependency.
                        if (spr.border != Vector4.zero) SetImageSliced(img);
                    }
                    else stats.Errors.Add($"sprite asset not found for '{name}': {assetPath}");
                }
            }

            // Explicit Image.type overrides the auto-Sliced default above.
            var typeStr = image["type"]?.ToString();
            if (!string.IsNullOrEmpty(typeStr)) SetEnumProp(img, "type", typeStr);

            // Filled image = the idiomatic progress / HP / damage bar (fill_amount),
            // instead of resizing a child. If a fill is given without a type, it's Filled.
            if (image["fill"] is JObject fill)
            {
                if (string.IsNullOrEmpty(typeStr)) SetEnumProp(img, "type", "Filled");
                if (fill["amount"] != null) SetProp(img, "fillAmount", fill["amount"].Value<float>());
                if (fill["method"] != null) SetEnumProp(img, "fillMethod", fill["method"].ToString());
                if (fill["origin"] != null) SetProp(img, "fillOrigin", fill["origin"].Value<int>());
                if (fill["clockwise"] != null) SetProp(img, "fillClockwise", fill["clockwise"].Value<bool>());
            }

            if (image["fill_center"] != null) SetProp(img, "fillCenter", image["fill_center"].Value<bool>());
            if (image["preserve_aspect"] != null) SetProp(img, "preserveAspect", image["preserve_aspect"].Value<bool>());
            if (image["ppu_multiplier"] != null) SetProp(img, "pixelsPerUnitMultiplier", image["ppu_multiplier"].Value<float>());
            if (image["raycast_target"] != null) SetProp(img, "raycastTarget", image["raycast_target"].Value<bool>());
        }

        static void ApplyText(GameObject go, JObject text, string element, ApplyStats stats)
        {
            if (text == null) return;
            string engine = text["engine"]?.ToString();

            Component txt;
            if (element == "button")
            {
                // Reuse an existing label child if present (idempotent / upsert-safe);
                // otherwise create one — a stretched child (mirrors ManageUI.BuildButton).
                txt = FindTextInChildren(go);
                if (txt == null)
                {
                    var labelGo = new GameObject("Text", typeof(RectTransform));
                    txt = AddTextComponent(labelGo, engine, stats);
                    if (txt != null)
                    {
                        labelGo.transform.SetParent(go.transform, worldPositionStays: false);
                        Stretch(labelGo);
                    }
                }
            }
            else
            {
                txt = GetComponentByName(go, "TextMeshProUGUI") ?? GetComponentByName(go, "Text");
            }

            if (txt == null) return;
            StyleText(txt, text);
        }

        // Applies value + optional color + optional align to a text component
        // (TMP or legacy Text), all via reflection so the connector stays free of
        // a compile-time com.unity.ugui / TextMeshPro dependency.
        static void StyleText(Component txt, JObject text)
        {
            if (text["value"] != null) SetProp(txt, "text", text["value"].ToString());
            if (text["color"] is JToken ct && SerializedPropertyValue.TryParseColor(ct, out var c, out _))
                SetProp(txt, "color", c);
            var align = text["align"]?.ToString();
            if (!string.IsNullOrEmpty(align)) SetTextAlign(txt, align);
            // Optional font: an asset path to a TMP_FontAsset (for TMP text) or a
            // Font (legacy). SetProp's type check ignores a mismatch, so a TMP font
            // path is a no-op on a legacy Text component and vice versa.
            var fontPath = text["font"]?.ToString();
            if (!string.IsNullOrEmpty(fontPath))
            {
                var font = AssetDatabase.LoadAssetAtPath<Object>(fontPath);
                if (font != null) SetProp(txt, "font", font);
            }
        }

        static void SetTextAlign(Component txt, string align)
        {
            var pi = txt.GetType().GetProperty("alignment", BindingFlags.Public | BindingFlags.Instance);
            if (pi == null || !pi.CanWrite) return;
            // TMP uses TextAlignmentOptions, legacy Text uses TextAnchor — map to each.
            bool tmp = pi.PropertyType.Name == "TextAlignmentOptions";
            string name;
            switch (align.ToLowerInvariant())
            {
                case "center": name = tmp ? "Center" : "MiddleCenter"; break;
                case "left": name = tmp ? "Left" : "MiddleLeft"; break;
                case "right": name = tmp ? "Right" : "MiddleRight"; break;
                case "top-left": case "topleft": name = tmp ? "TopLeft" : "UpperLeft"; break;
                case "top-center": case "top": name = tmp ? "Top" : "UpperCenter"; break;
                default: return;
            }
            try { pi.SetValue(txt, System.Enum.Parse(pi.PropertyType, name)); }
            catch { /* best-effort */ }
        }

        static GameObject FindChildByName(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c.gameObject;
            }
            return null;
        }

        static Component FindTextInChildren(GameObject go)
        {
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                var t = GetComponentByName(child, "TextMeshProUGUI") ?? GetComponentByName(child, "Text");
                if (t != null) return t;
            }
            return null;
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

        static void SetImageSliced(Component img) => SetEnumProp(img, "type", "Sliced");

        // Sets an enum property by case-insensitive name, resolving the enum type at
        // runtime (the connector has no compile-time com.unity.ugui reference).
        static void SetEnumProp(Component comp, string prop, string enumName)
        {
            if (comp == null || string.IsNullOrEmpty(enumName)) return;
            var pi = comp.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            if (pi == null || !pi.CanWrite || !pi.PropertyType.IsEnum) return;
            try { pi.SetValue(comp, System.Enum.Parse(pi.PropertyType, enumName, true)); }
            catch { /* best-effort */ }
        }

        static Component GetOrAddByName(GameObject go, string typeName)
        {
            var existing = GetComponentByName(go, typeName);
            return existing != null ? existing : AddByName(go, typeName);
        }

        // ---- layout: LayoutGroups / LayoutElement / ContentSizeFitter ----

        static void ApplyLayout(GameObject go, JObject node)
        {
            if (node["layout"] is JObject layout) ApplyLayoutGroup(go, layout);
            if (node["layout_element"] is JObject le) ApplyLayoutElement(go, le);
            if (node["fit"] is JObject fit) ApplyFit(go, fit);
        }

        static void ApplyLayoutGroup(GameObject go, JObject layout)
        {
            string type = (layout["type"]?.ToString() ?? "horizontal").ToLowerInvariant();
            string comp = type == "vertical" ? "VerticalLayoutGroup"
                : type == "grid" ? "GridLayoutGroup" : "HorizontalLayoutGroup";
            var g = GetOrAddByName(go, comp);
            if (g == null) return;

            if (layout["padding"] != null && SerializedPropertyValue.TryParseFloats(layout["padding"], 4, out var pad, out _))
                SetProp(g, "padding", new RectOffset((int)pad[0], (int)pad[1], (int)pad[2], (int)pad[3]));
            var align = layout["align"]?.ToString();
            if (!string.IsNullOrEmpty(align)) SetEnumProp(g, "childAlignment", MapTextAnchor(align));

            if (type == "grid")
            {
                if (layout["cell"] != null && SerializedPropertyValue.TryParseFloats(layout["cell"], 2, out var cs, out _))
                    SetProp(g, "cellSize", new Vector2(cs[0], cs[1]));
                if (layout["spacing"] != null && SerializedPropertyValue.TryParseFloats(layout["spacing"], 2, out var sp, out _))
                    SetProp(g, "spacing", new Vector2(sp[0], sp[1]));
                if (layout["start_corner"] != null) SetEnumProp(g, "startCorner", MapEnumWord(layout["start_corner"].ToString()));
                if (layout["start_axis"] != null) SetEnumProp(g, "startAxis", MapEnumWord(layout["start_axis"].ToString()));
                if (layout["constraint"] != null) SetEnumProp(g, "constraint", MapConstraint(layout["constraint"].ToString()));
                if (layout["count"] != null) SetProp(g, "constraintCount", layout["count"].Value<int>());
            }
            else
            {
                if (layout["spacing"] != null) SetProp(g, "spacing", layout["spacing"].Value<float>());
                if (layout["control_size"] is JArray cw && cw.Count == 2)
                {
                    SetProp(g, "childControlWidth", cw[0].Value<bool>());
                    SetProp(g, "childControlHeight", cw[1].Value<bool>());
                }
                if (layout["force_expand"] is JArray fe && fe.Count == 2)
                {
                    SetProp(g, "childForceExpandWidth", fe[0].Value<bool>());
                    SetProp(g, "childForceExpandHeight", fe[1].Value<bool>());
                }
                if (layout["reverse"] != null) SetProp(g, "reverseArrangement", layout["reverse"].Value<bool>());
            }
        }

        static void ApplyLayoutElement(GameObject go, JObject le)
        {
            var c = GetOrAddByName(go, "LayoutElement");
            if (c == null) return;
            if (le["min"] is JArray mn && mn.Count == 2) { SetProp(c, "minWidth", mn[0].Value<float>()); SetProp(c, "minHeight", mn[1].Value<float>()); }
            if (le["preferred"] is JArray pf && pf.Count == 2) { SetProp(c, "preferredWidth", pf[0].Value<float>()); SetProp(c, "preferredHeight", pf[1].Value<float>()); }
            if (le["flexible"] is JArray fl && fl.Count == 2) { SetProp(c, "flexibleWidth", fl[0].Value<float>()); SetProp(c, "flexibleHeight", fl[1].Value<float>()); }
            if (le["ignore"] != null) SetProp(c, "ignoreLayout", le["ignore"].Value<bool>());
        }

        static void ApplyFit(GameObject go, JObject fit)
        {
            var c = GetOrAddByName(go, "ContentSizeFitter");
            if (c == null) return;
            if (fit["h"] != null) SetEnumProp(c, "horizontalFit", MapFit(fit["h"].ToString()));
            if (fit["v"] != null) SetEnumProp(c, "verticalFit", MapFit(fit["v"].ToString()));
        }

        // IR align ("middle-center", "top-left", "center") → TextAnchor name.
        static string MapTextAnchor(string a)
        {
            var p = a.Trim().ToLowerInvariant().Split('-');
            string v = "Middle", h = "Center";
            string MV(string s) => s == "top" ? "Upper" : s == "bottom" ? "Lower" : s == "middle" ? "Middle" : null;
            string MH(string s) => s == "left" ? "Left" : s == "right" ? "Right" : s == "center" ? "Center" : null;
            if (p.Length == 2) { v = MV(p[0]) ?? "Middle"; h = MH(p[1]) ?? "Center"; }
            else if (p.Length == 1) { var hh = MH(p[0]); var vv = MV(p[0]); if (hh != null) h = hh; if (vv != null) v = vv; }
            return v + h;
        }

        // "upper-left"/"horizontal" → "UpperLeft"/"Horizontal".
        static string MapEnumWord(string s)
        {
            var parts = s.Trim().ToLowerInvariant().Split('-', '_');
            string r = "";
            foreach (var p in parts) if (p.Length > 0) r += char.ToUpperInvariant(p[0]) + p.Substring(1);
            return r;
        }

        static string MapConstraint(string s)
        {
            switch (s.Trim().ToLowerInvariant())
            {
                case "fixed-columns": case "fixed_columns": case "columns": return "FixedColumnCount";
                case "fixed-rows": case "fixed_rows": case "rows": return "FixedRowCount";
                default: return "Flexible";
            }
        }

        static string MapFit(string s)
        {
            switch (s.Trim().ToLowerInvariant())
            {
                case "min": return "MinSize";
                case "preferred": return "PreferredSize";
                default: return "Unconstrained";
            }
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
