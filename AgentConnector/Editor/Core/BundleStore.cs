using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
// `using UnityEditor;` brings UnityEditor.PackageInfo (legacy AssetStore type)
// into scope alongside UnityEditor.PackageManager.PackageInfo; alias to the
// PackageManager type explicitly (see AGENT.md §4.14).
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HeraAgent
{
    /// <summary>
    /// Loads one connector-bundled gzipped-JSONL knowledge file into a keyed
    /// dictionary on first access, and answers exact lookups plus full-scan
    /// Levenshtein suggestions over its keys.
    ///
    /// The bundle is immutable UPM content, so the load is once-per-domain: the
    /// first caller that resolves an answer (index or terminal error) settles it
    /// and no one pays the path resolution or decompression again.
    ///
    /// Owned by the store that declares it — each knowledge store keeps its own
    /// entry shape, its own grouped index, and any predicates specific to it,
    /// and delegates only the loading machinery here. UnityDocsStore is
    /// deliberately not a consumer: it resolves a Unity-version bucket and runs a
    /// 3-layer prefix/length/bounded suggest, both locked decisions.
    /// </summary>
    public class BundleStore<TEntry> where TEntry : class
    {
        const string DataDir = "Editor/Data";

        readonly Assembly m_assembly;
        readonly string m_dataFileName;
        readonly string m_searchName;
        readonly string m_label;
        readonly Func<TEntry, string> m_keyOf;

        Dictionary<string, TEntry> m_index;
        string[] m_keys;
        string m_loadError;
        readonly object m_lock = new object();

        /// <param name="assembly">Assembly whose owning UPM package holds the bundle.</param>
        /// <param name="dataFileName">Bundle file name, e.g. <c>game_feel_1.0.jsonl.gz.bytes</c>.</param>
        /// <param name="label">Noun used in load-failure messages, e.g. <c>game-feel</c>.</param>
        /// <param name="keyOf">Reads the dictionary key off an entry; entries with a null or empty key are dropped.</param>
        public BundleStore(Assembly assembly, string dataFileName, string label, Func<TEntry, string> keyOf)
        {
            m_assembly = assembly;
            m_dataFileName = dataFileName;
            m_label = label;
            m_keyOf = keyOf;
            // The AssetDatabase fallback searches by asset name, which is the
            // file name up to the first extension.
            var dot = dataFileName.IndexOf(".jsonl", StringComparison.OrdinalIgnoreCase);
            m_searchName = dot > 0 ? dataFileName.Substring(0, dot) : dataFileName;
        }

        /// <summary>
        /// Returns the entry for an exact key match, or null on miss.
        /// </summary>
        public TEntry Lookup(string key)
        {
            EnsureLoaded();
            if (m_index == null || string.IsNullOrEmpty(key)) return null;
            return m_index.TryGetValue(key, out var entry) ? entry : null;
        }

        /// <summary>
        /// Number of indexed entries; 0 if the data file failed to load.
        /// </summary>
        public int Count
        {
            get { EnsureLoaded(); return m_index == null ? 0 : m_index.Count; }
        }

        /// <summary>
        /// Error message surfaced when the data file could not be located or
        /// decompressed. Null when the load succeeded.
        /// </summary>
        public string LoadError
        {
            get { EnsureLoaded(); return m_loadError; }
        }

        /// <summary>
        /// Every loaded entry in dictionary order; empty when the load failed.
        /// Stores group these into their own index shapes.
        /// </summary>
        public ICollection<TEntry> Values
        {
            get { EnsureLoaded(); return m_index == null ? Array.Empty<TEntry>() : (ICollection<TEntry>)m_index.Values; }
        }

        /// <summary>
        /// Returns up to <paramref name="max"/> keys within
        /// <paramref name="maxDistance"/> Levenshtein distance of the query.
        /// Full scan — these corpora are small enough (tens of entries) that
        /// pre-filters would cost more than they save.
        /// </summary>
        public List<string> SuggestSimilar(string query, int maxDistance = 3, int max = 5)
        {
            EnsureLoaded();
            var result = new List<string>();
            if (m_keys == null || m_keys.Length == 0 || string.IsNullOrEmpty(query))
                return result;

            var candidates = new List<(string key, int dist)>();
            foreach (var k in m_keys)
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

        void EnsureLoaded()
        {
            // Already resolved (loaded or terminally failed).
            if (m_index != null || m_loadError != null) return;

            lock (m_lock)
            {
                // Re-check inside the lock — two callers can clear the fast path
                // at the same time, and only one should pay for the load.
                if (m_index != null || m_loadError != null) return;

                var path = ResolveDataPath();
                if (path == null)
                {
                    m_loadError = $"could not resolve bundled {m_label} file {m_dataFileName}";
                    return;
                }
                if (!File.Exists(path))
                {
                    m_loadError = $"bundled {m_label} file missing: {path}";
                    return;
                }

                try
                {
                    var index = new Dictionary<string, TEntry>(64);
                    using (var fs = File.OpenRead(path))
                    using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gz))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Length == 0) continue;
                            TEntry entry;
                            try { entry = JsonConvert.DeserializeObject<TEntry>(line); }
                            catch { continue; }
                            if (entry == null) continue;
                            var key = m_keyOf(entry);
                            if (string.IsNullOrEmpty(key)) continue;
                            index[key] = entry;
                        }
                    }
                    m_index = index;
                    var keys = new string[index.Count];
                    int i = 0;
                    foreach (var k in index.Keys) keys[i++] = k;
                    m_keys = keys;
                    m_loadError = null;
                }
                catch (Exception ex)
                {
                    m_loadError = $"failed to load {path}: {ex.Message}";
                    m_index = null;
                    m_keys = null;
                }
            }
        }

        string ResolveDataPath()
        {
            PackageInfo pi = null;
            try { pi = PackageInfo.FindForAssembly(m_assembly); }
            catch { /* fall through to the AssetDatabase-based fallback */ }

            if (pi != null && !string.IsNullOrEmpty(pi.resolvedPath))
            {
                var path = Path.Combine(pi.resolvedPath, DataDir, m_dataFileName);
                if (File.Exists(path)) return path;
            }

            // Fallback for in-project (non-UPM) checkouts: search via
            // AssetDatabase so embedded copies in Assets/ still resolve.
            var guids = AssetDatabase.FindAssets(m_searchName + " t:DefaultAsset");
            foreach (var g in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(g);
                if (assetPath.EndsWith(m_dataFileName, StringComparison.OrdinalIgnoreCase))
                    return assetPath;
            }
            return null;
        }
    }
}
