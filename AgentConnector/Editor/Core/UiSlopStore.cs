using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using UnityEditor;
// `using UnityEditor;` brings UnityEditor.PackageInfo (legacy AssetStore type)
// into scope alongside UnityEditor.PackageManager.PackageInfo; alias to the
// PackageManager type explicitly (see AGENT.md §4.14, mirrors GameFeelStore).
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HeraAgent
{
    /// <summary>
    /// Loads the connector-bundled Unity UI-slop taxonomy (ported from the
    /// slopslap methodology, grounded in live hera measurement and per-version
    /// editor-binary reflection) into a keyed dictionary on first access. Powers
    /// the unity-deslop pipeline hints (Unity De-slop Mode). Mirrors
    /// GameFeelStore's load/reload pattern: one immutable bundle file, plain
    /// full-scan Levenshtein suggest (corpus is ~50 topics — the 3-layer
    /// prefix-bucket optimization would be premature here).
    /// </summary>
    public static class UiSlopStore
    {
        public class Entry
        {
            public string id;
            public string area;      // A | B | C | D | E (fixed execution order)
            public string severity;  // strong | weak
            public string tell;
            public string check_ugui;
            public string check_uitk;
            public string exception; // null when none
            public string fix;
            public object borrow;    // { src, ... } for replacement tells; null for deletion tells
            public string deep_topic;
        }

        const string DataDir = "Editor/Data";
        const string DataFileName = "ui_slop_1.0.jsonl.gz.bytes";

        // Areas are inspected in parallel but executed in this fixed order —
        // upstream commits dissolve downstream conflicts (A flattens surfaces
        // before E disciplines the palette, etc.).
        static readonly string[] AreaOrder = { "A", "B", "C", "D", "E" };

        static Dictionary<string, Entry> s_index;
        static string[] s_keys;
        static string s_loadError;
        static string s_loadedDataPath;
        static long s_loadedDataLength;
        static DateTime s_loadedDataLastWriteUtc;
        static readonly object s_lock = new object();

        /// <summary>
        /// Returns the entry for an exact id match, or null on miss.
        /// </summary>
        public static Entry Lookup(string id)
        {
            EnsureLoaded();
            if (s_index == null || string.IsNullOrEmpty(id)) return null;
            return s_index.TryGetValue(id, out var entry) ? entry : null;
        }

        /// <summary>
        /// The version-appropriate check predicate for a tell: the UI Toolkit
        /// variant when uiSystem is "uitk", the uGUI variant otherwise (the
        /// default). Returns null on miss.
        /// </summary>
        public static string CheckFor(string id, string uiSystem)
        {
            var entry = Lookup(id);
            if (entry == null) return null;
            return string.Equals(uiSystem, "uitk", StringComparison.OrdinalIgnoreCase)
                ? entry.check_uitk
                : entry.check_ugui;
        }

        /// <summary>
        /// Number of indexed entries; 0 if the data file failed to load.
        /// </summary>
        public static int Count
        {
            get { EnsureLoaded(); return s_index == null ? 0 : s_index.Count; }
        }

        /// <summary>
        /// Error message surfaced when the data file could not be located or
        /// decompressed. Null when the load succeeded.
        /// </summary>
        public static string LoadError
        {
            get { EnsureLoaded(); return s_loadError; }
        }

        /// <summary>
        /// Ordered taxonomy index — one { area, tells: [{id, severity, tell}] }
        /// group per area, A through E. Areas absent from the bundle are
        /// skipped; unknown areas are appended in encounter order.
        /// </summary>
        public static List<object> BuildIndex()
        {
            EnsureLoaded();
            var groups = new List<object>();
            if (s_index == null) return groups;

            var byArea = new Dictionary<string, List<Entry>>();
            var extraAreas = new List<string>();
            foreach (var entry in s_index.Values)
            {
                var area = string.IsNullOrEmpty(entry.area) ? "?" : entry.area;
                if (!byArea.TryGetValue(area, out var list))
                {
                    list = new List<Entry>();
                    byArea[area] = list;
                    if (Array.IndexOf(AreaOrder, area) < 0) extraAreas.Add(area);
                }
                list.Add(entry);
            }

            var ordered = new List<string>(AreaOrder);
            ordered.AddRange(extraAreas);
            foreach (var area in ordered)
            {
                if (!byArea.TryGetValue(area, out var entries)) continue;
                entries.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
                var tells = new List<object>(entries.Count);
                foreach (var e in entries) tells.Add(new { id = e.id, severity = e.severity, tell = e.tell });
                groups.Add(new { area, tells });
            }
            return groups;
        }

        /// <summary>
        /// Returns up to <paramref name="max"/> ids within
        /// <paramref name="maxDistance"/> Levenshtein distance of the query.
        /// Full scan — the corpus is small enough that pre-filters would cost
        /// more than they save.
        /// </summary>
        public static List<string> SuggestSimilar(string query, int maxDistance = 3, int max = 5)
        {
            EnsureLoaded();
            var result = new List<string>();
            if (s_keys == null || s_keys.Length == 0 || string.IsNullOrEmpty(query))
                return result;

            var candidates = new List<(string key, int dist)>();
            foreach (var k in s_keys)
            {
                var d = Levenshtein.DistanceBounded(query, k, maxDistance);
                if (d <= maxDistance) candidates.Add((k, d));
            }
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            foreach (var (k, _) in candidates)
            {
                result.Add(k);
                if (result.Count >= max) break;
            }
            return result;
        }

        static void EnsureLoaded()
        {
            // Already resolved (loaded or terminally failed). The bundled file is
            // immutable UPM content, so skip the per-call path resolution +
            // FileInfo stat once we have an answer.
            if (s_index != null || s_loadError != null) return;
            var path = ResolveDataPath();
            if (path == null)
            {
                if (s_index != null || s_loadError != null) return;
                s_loadError = $"could not resolve bundled ui-slop file {DataFileName}";
                return;
            }
            if (!File.Exists(path))
            {
                if (s_index != null || s_loadError != null) return;
                s_loadError = $"bundled ui-slop file missing: {path}";
                return;
            }

            lock (s_lock)
            {
                var info = new FileInfo(path);
                if (IsLoadedDataCurrent(path, info))
                    return;

                try
                {
                    var index = new Dictionary<string, Entry>(64);
                    using (var fs = File.OpenRead(path))
                    using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gz))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Length == 0) continue;
                            Entry entry;
                            try { entry = JsonConvert.DeserializeObject<Entry>(line); }
                            catch { continue; }
                            if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                            index[entry.id] = entry;
                        }
                    }
                    s_index = index;
                    s_loadedDataPath = path;
                    s_loadedDataLength = info.Length;
                    s_loadedDataLastWriteUtc = info.LastWriteTimeUtc;
                    var keys = new string[index.Count];
                    int i = 0;
                    foreach (var k in index.Keys) keys[i++] = k;
                    s_keys = keys;
                    s_loadError = null;
                }
                catch (Exception ex)
                {
                    s_loadError = $"failed to load {path}: {ex.Message}";
                    s_index = null;
                    s_keys = null;
                    s_loadedDataPath = null;
                    s_loadedDataLength = 0;
                    s_loadedDataLastWriteUtc = default(DateTime);
                }
            }
        }

        static bool IsLoadedDataCurrent(string path, FileInfo info)
        {
            return s_index != null
                && s_loadError == null
                && string.Equals(s_loadedDataPath, path, StringComparison.OrdinalIgnoreCase)
                && s_loadedDataLength == info.Length
                && s_loadedDataLastWriteUtc == info.LastWriteTimeUtc;
        }

        static string ResolveDataPath()
        {
            PackageInfo pi = null;
            try { pi = PackageInfo.FindForAssembly(typeof(UiSlopStore).Assembly); }
            catch { /* fall through to the AssetDatabase-based fallback */ }

            if (pi != null && !string.IsNullOrEmpty(pi.resolvedPath))
            {
                var path = Path.Combine(pi.resolvedPath, DataDir, DataFileName);
                if (File.Exists(path)) return path;
            }

            // Fallback for in-project (non-UPM) checkouts: search via
            // AssetDatabase so embedded copies in Assets/ still resolve.
            var guids = AssetDatabase.FindAssets("ui_slop_1.0 t:DefaultAsset");
            foreach (var g in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(g);
                if (assetPath.EndsWith(DataFileName, StringComparison.OrdinalIgnoreCase))
                    return assetPath;
            }
            return null;
        }
    }
}
