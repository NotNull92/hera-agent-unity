using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using UnityEditor;
// `using UnityEditor;` brings UnityEditor.PackageInfo (legacy AssetStore type)
// into scope alongside UnityEditor.PackageManager.PackageInfo; alias to the
// PackageManager type explicitly (see AGENT.md §4.14).
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HeraAgent
{
    /// <summary>
    /// Loads the connector-bundled Game Feel knowledge base (Game Feel &amp; Juice
    /// Bible + Ethical Engagement Game Feel Framework) into a keyed dictionary on
    /// first access. Powers the `game_feel` tool and the Game Feel Mode (Beta)
    /// hints. Mirrors UnityDocsStore's load/reload pattern but stays deliberately
    /// simpler: one bundle file (content is not Unity-version-dependent) and a
    /// plain full-scan Levenshtein suggest (corpus is ~40 topics — the 3-layer
    /// prefix-bucket optimization would be premature here).
    /// </summary>
    public static class GameFeelStore
    {
        public class Entry
        {
            public string key;
            public string category;
            public string title;
            public string body;
        }

        const string DataDir = "Editor/Data";
        const string DataFileName = "game_feel_1.0.jsonl.gz.bytes";

        // Ethics leads the index deliberately — recipes are meant to be applied
        // with the ethical constraints built in, not checked afterwards.
        static readonly string[] CategoryOrder =
            { "ethics", "theory", "technique", "ui", "workflow", "anti_pattern", "checklist" };

        static Dictionary<string, Entry> s_index;
        static string[] s_keys;
        static string s_loadError;
        static string s_loadedDataPath;
        static long s_loadedDataLength;
        static DateTime s_loadedDataLastWriteUtc;
        static readonly object s_lock = new object();

        /// <summary>
        /// Returns the entry for an exact key match, or null on miss.
        /// </summary>
        public static Entry Lookup(string key)
        {
            EnsureLoaded();
            if (s_index == null || string.IsNullOrEmpty(key)) return null;
            return s_index.TryGetValue(key, out var entry) ? entry : null;
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
        /// Ordered topic index — one { category, topics: [{key, title}] } group
        /// per known category, ethics first. Categories absent from the bundle
        /// are skipped; unknown categories are appended in encounter order.
        /// </summary>
        public static List<object> BuildIndex()
        {
            EnsureLoaded();
            var groups = new List<object>();
            if (s_index == null) return groups;

            var byCategory = new Dictionary<string, List<Entry>>();
            var extraCategories = new List<string>();
            foreach (var entry in s_index.Values)
            {
                var cat = string.IsNullOrEmpty(entry.category) ? "misc" : entry.category;
                if (!byCategory.TryGetValue(cat, out var list))
                {
                    list = new List<Entry>();
                    byCategory[cat] = list;
                    if (Array.IndexOf(CategoryOrder, cat) < 0) extraCategories.Add(cat);
                }
                list.Add(entry);
            }

            var ordered = new List<string>(CategoryOrder);
            ordered.AddRange(extraCategories);
            foreach (var cat in ordered)
            {
                if (!byCategory.TryGetValue(cat, out var entries)) continue;
                entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
                var topics = new List<object>(entries.Count);
                foreach (var e in entries) topics.Add(new { key = e.key, title = e.title });
                groups.Add(new { category = cat, topics });
            }
            return groups;
        }

        /// <summary>
        /// Returns up to <paramref name="max"/> keys within
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
                s_loadError = $"could not resolve bundled game-feel file {DataFileName}";
                return;
            }
            if (!File.Exists(path))
            {
                if (s_index != null || s_loadError != null) return;
                s_loadError = $"bundled game-feel file missing: {path}";
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
                            if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
                            index[entry.key] = entry;
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
            try { pi = PackageInfo.FindForAssembly(typeof(GameFeelStore).Assembly); }
            catch { /* fall through to the AssetDatabase-based fallback */ }

            if (pi != null && !string.IsNullOrEmpty(pi.resolvedPath))
            {
                var path = Path.Combine(pi.resolvedPath, DataDir, DataFileName);
                if (File.Exists(path)) return path;
            }

            // Fallback for in-project (non-UPM) checkouts: search via
            // AssetDatabase so embedded copies in Assets/ still resolve.
            var guids = AssetDatabase.FindAssets("game_feel_1.0 t:DefaultAsset");
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
