using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
// `using UnityEditor;` brings UnityEditor.PackageInfo (legacy AssetStore type)
// into scope alongside UnityEditor.PackageManager.PackageInfo; alias to the
// PackageManager type explicitly (see AGENT.md §4.14).
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HeraAgent
{
    /// <summary>
    /// Loads the connector-bundled Unity ScriptReference data set
    /// (`AgentConnector/Editor/Data/unity_docs_6.0.jsonl.gz.bytes`) into a
    /// keyed dictionary on first access. The file ships inside the UPM
    /// package itself so consumers don't have to point at a local
    /// Documentation folder.
    ///
    /// Replaces the earlier UnityDocsParser/UnityDocsIndex pair that read
    /// HTML from a user-configured docs_root and parsed it per call. The
    /// HTML→JSON conversion now happens once in
    /// `tools/build-unity-docs/main.go`; this file only loads + indexes the
    /// resulting JSONL.
    /// </summary>
    public static class UnityDocsStore
    {
        public class Entry
        {
            public string name;
            public string title;
            public string signature;
            public string summary;
            public string manual_url;
            public string scriptreference_url;
            public string unity_version;
        }

        const string DataRelativePath = "Editor/Data/unity_docs_6.0.jsonl.gz.bytes";

        static Dictionary<string, Entry> s_index;
        static string[] s_keys;
        static string s_loadError;
        static readonly object s_lock = new object();

        /// <summary>
        /// Returns the dictionary entry for an exact key match, or null on miss.
        /// </summary>
        public static Entry Lookup(string key)
        {
            EnsureLoaded();
            if (s_index == null) return null;
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
        /// Returns up to <paramref name="max"/> keys within
        /// <paramref name="maxDistance"/> Levenshtein distance of the query.
        /// Used to power DOC_NOT_FOUND `did_you_mean` hints.
        /// </summary>
        public static List<string> SuggestSimilar(string query, int maxDistance = 3, int max = 5)
        {
            EnsureLoaded();
            if (s_keys == null || s_keys.Length == 0 || string.IsNullOrEmpty(query))
                return new List<string>();

            var candidates = new List<(string name, int dist)>();
            foreach (var k in s_keys)
            {
                var d = Levenshtein.Distance(query, k);
                if (d <= maxDistance) candidates.Add((k, d));
            }
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            var result = new List<string>();
            foreach (var (n, _) in candidates)
            {
                result.Add(n);
                if (result.Count >= max) break;
            }
            return result;
        }

        /// <summary>
        /// Error message surfaced when the data file could not be located or
        /// decompressed. Null when the load succeeded (or has not been
        /// attempted yet — call EnsureLoaded first).
        /// </summary>
        public static string LoadError
        {
            get { EnsureLoaded(); return s_loadError; }
        }

        static void EnsureLoaded()
        {
            if (s_index != null || s_loadError != null) return;

            lock (s_lock)
            {
                if (s_index != null || s_loadError != null) return;

                var path = ResolveDataPath();
                if (path == null)
                {
                    s_loadError = $"could not resolve UPM package path for {DataRelativePath}";
                    return;
                }
                if (!File.Exists(path))
                {
                    s_loadError = $"bundled docs file missing: {path}";
                    return;
                }

                try
                {
                    var index = new Dictionary<string, Entry>(32 * 1024);
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
                            if (entry == null || string.IsNullOrEmpty(entry.name)) continue;
                            index[entry.name] = entry;
                        }
                    }
                    s_index = index;
                    var keys = new string[index.Count];
                    int i = 0;
                    foreach (var k in index.Keys) keys[i++] = k;
                    s_keys = keys;
                }
                catch (Exception ex)
                {
                    s_loadError = $"failed to load {path}: {ex.Message}";
                }
            }
        }

        static string ResolveDataPath()
        {
            PackageInfo pi = null;
            try { pi = PackageInfo.FindForAssembly(typeof(UnityDocsStore).Assembly); }
            catch { /* fall through to the AssetDatabase-based fallback */ }

            if (pi != null && !string.IsNullOrEmpty(pi.resolvedPath))
                return Path.Combine(pi.resolvedPath, DataRelativePath);

            // Fallback for in-project (non-UPM) checkouts: search via
            // AssetDatabase so embedded copies in Assets/ still resolve.
            var guids = AssetDatabase.FindAssets("unity_docs_6.0 t:DefaultAsset");
            foreach (var g in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(g);
                if (assetPath.EndsWith("unity_docs_6.0.jsonl.gz.bytes",
                    StringComparison.OrdinalIgnoreCase))
                    return assetPath;
            }
            return null;
        }
    }
}
