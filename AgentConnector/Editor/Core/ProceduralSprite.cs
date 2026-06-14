using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
// `using System;` + `using UnityEngine;` leave `Object` ambiguous; alias to the
// engine type so bare Object here is UnityEngine.Object (CS0104 trap — CLAUDE.md).
using Object = UnityEngine.Object;

namespace HeraAgent
{
    /// <summary>
    /// Tier-1 procedural sprite generation: solid / rounded_rect / gradient.
    /// Bakes a Texture2D headlessly, writes a PNG under Assets/, and imports it as
    /// a Sprite. No external dependency (hera's zero-runtime-dep principle) — this
    /// covers the CSS-shape vocabulary an HTML mockup maps onto. Richer bitmap art
    /// is intentionally out of scope (would require an external image model).
    /// Shared by ui_doc's gen_sprite action and apply (inline sprite specs).
    /// </summary>
    public static class ProceduralSprite
    {
        public const string DefaultDir = "Assets/HeraGenerated";
        private const int MaxDimension = 4096;

        /// <summary>
        /// Generates a sprite PNG from a spec and imports it as a Sprite. Returns
        /// the asset path (under Assets/) or an error.
        /// spec keys: kind (solid|rounded_rect|gradient), size [w,h],
        /// color/from/to (#hex or r,g,b[,a]), radius (rounded_rect, px),
        /// direction (gradient: vertical|horizontal). outPath null → auto under
        /// <see cref="DefaultDir"/> keyed by the spec content.
        /// </summary>
        public static (string path, string err) Generate(JObject spec, string outPath)
        {
            if (spec == null) return (null, "sprite spec is null");
            string kind = (spec["kind"]?.ToString() ?? "solid").ToLowerInvariant();

            int w = 100, h = 100;
            var sizeTok = spec["size"];
            if (sizeTok != null && SerializedPropertyValue.TryParseFloats(sizeTok, 2, out var sz, out _))
            {
                w = Mathf.Max(1, Mathf.RoundToInt(sz[0]));
                h = Mathf.Max(1, Mathf.RoundToInt(sz[1]));
            }
            if (w > MaxDimension || h > MaxDimension)
                return (null, $"size {w}x{h} exceeds the {MaxDimension}px cap");

            Color[] pixels;
            switch (kind)
            {
                case "solid":
                    pixels = Solid(w, h, ColorOf(spec, "color", Color.white));
                    break;
                case "rounded_rect":
                    pixels = RoundedRect(w, h, ColorOf(spec, "color", Color.white), RadiusOf(spec, w, h));
                    break;
                case "gradient":
                    pixels = Gradient(w, h, ColorOf(spec, "from", Color.white), ColorOf(spec, "to", Color.black),
                        (spec["direction"]?.ToString() ?? "vertical").ToLowerInvariant());
                    break;
                default:
                    return (null, $"unknown sprite kind '{kind}'. Valid: solid, rounded_rect, gradient.");
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            if (png == null) return (null, "failed to encode PNG");

            string path = string.IsNullOrWhiteSpace(outPath) ? AutoPath(spec, kind, w, h) : outPath.Replace('\\', '/');
            if (!path.StartsWith("Assets/")) return (null, $"out path must be under Assets/ (got '{path}')");
            if (!path.EndsWith(".png")) path += ".png";

            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, png);
            }
            catch (Exception ex)
            {
                return (null, $"failed to write '{path}': {ex.Message}");
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            return (path, null);
        }

        static string AutoPath(JObject spec, string kind, int w, int h)
        {
            // Deterministic-ish name from spec content so identical specs reuse a file.
            uint hash = (uint)spec.ToString(Newtonsoft.Json.Formatting.None).GetHashCode();
            return $"{DefaultDir}/{kind}_{w}x{h}_{hash:x8}.png";
        }

        static Color ColorOf(JObject spec, string key, Color fallback)
        {
            var tok = spec[key];
            if (tok != null && SerializedPropertyValue.TryParseColor(tok, out var c, out _)) return c;
            return fallback;
        }

        static int RadiusOf(JObject spec, int w, int h)
        {
            int r = spec["radius"]?.Value<int>() ?? Mathf.RoundToInt(Mathf.Min(w, h) * 0.2f);
            return Mathf.Clamp(r, 0, Mathf.Min(w, h) / 2);
        }

        static Color[] Solid(int w, int h, Color c)
        {
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            return px;
        }

        static Color[] RoundedRect(int w, int h, Color c, int radius)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var col = c;
                    col.a *= Coverage(x, y, w, h, radius);
                    px[y * w + x] = col;
                }
            return px;
        }

        // Signed-distance coverage for a rounded box, anti-aliased over ~1px.
        static float Coverage(int x, int y, int w, int h, int radius)
        {
            if (radius <= 0) return 1f;
            float px = x + 0.5f - w / 2f;
            float py = y + 0.5f - h / 2f;
            float qx = Mathf.Abs(px) - (w / 2f - radius);
            float qy = Mathf.Abs(py) - (h / 2f - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            float dist = outside + inside - radius;
            return Mathf.Clamp01(0.5f - dist);
        }

        static Color[] Gradient(int w, int h, Color from, Color to, string dir)
        {
            var px = new Color[w * h];
            bool horizontal = dir == "horizontal";
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = horizontal
                        ? (w <= 1 ? 0f : (float)x / (w - 1))
                        : (h <= 1 ? 0f : (float)y / (h - 1));
                    px[y * w + x] = Color.Lerp(from, to, t);
                }
            return px;
        }
    }
}
