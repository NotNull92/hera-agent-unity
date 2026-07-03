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
    /// Loads the connector-bundled Unity ScriptReference data set into a keyed
    /// dictionary on first access. The file ships inside the UPM package itself
    /// so consumers don't have to point at a local Documentation folder.
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

        const string DataDir = "Editor/Data";
        const string FallbackDocsVersion = UnityVersionCompat.Docs6000_0;

        static Dictionary<string, Entry> s_index;
        static string[] s_keys;
        static string s_loadError;
        static string s_loadedDocsVersion;
        static string s_loadedDataPath;
        static long s_loadedDataLength;
        static DateTime s_loadedDataLastWriteUtc;
        static readonly object s_lock = new object();

        // Lazy prefix-bucket index — keys grouped by lowercase first char.
        // Used by SuggestSimilar to scan ~1/26 of the corpus on typical misses
        // instead of the full 31k. Built once after EnsureLoaded succeeds.
        static Dictionary<char, string[]> s_prefixBuckets;
        static readonly object s_bucketsLock = new object();

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

        public static string LoadedDocsVersion
        {
            get { EnsureLoaded(); return s_loadedDocsVersion; }
        }

        /// <summary>
        /// Returns up to <paramref name="max"/> keys within
        /// <paramref name="maxDistance"/> Levenshtein distance of the query.
        /// Used to power DOC_NOT_FOUND `did_you_mean` hints.
        ///
        /// Three layered cheap pre-filters make this near-O(n/26) on typical
        /// typos instead of the naive O(n × L²) scan:
        ///   1. Prefix bucket — only keys starting with the same letter
        ///      (case-insensitive) are looked at first.
        ///   2. Length filter — keys whose length differs from the query
        ///      by more than maxDistance can't reach the budget; skipped.
        ///   3. Bounded Levenshtein — DP table bails out the moment a row
        ///      min exceeds maxDistance (sentinel return).
        ///
        /// If the prefix bucket yielded zero results (typically because the
        /// query has a first-character typo) the full key set is rescanned
        /// as a fallback so we don't silently miss obvious suggestions.
        /// </summary>
        public static List<string> SuggestSimilar(string query, int maxDistance = 3, int max = 5)
        {
            EnsureLoaded();
            if (s_keys == null || s_keys.Length == 0 || string.IsNullOrEmpty(query))
                return new List<string>();

            EnsurePrefixBucketsBuilt();

            var bucket = ResolveBucket(query);
            var primary = ScanBucket(bucket, query, maxDistance, max);
            if (primary.Count >= max || bucket == s_keys)
                return primary;

            // Bucket miss (or thin result) — fall back to the full key set.
            // Length filter + bounded Levenshtein still apply, so this is
            // ~the original cost only when the bucket really came up empty.
            var fallback = ScanBucket(s_keys, query, maxDistance, max);
            return fallback.Count > primary.Count ? fallback : primary;
        }

        static List<string> ScanBucket(string[] bucket, string query, int maxDistance, int max)
        {
            int queryLen = query.Length;
            var candidates = new List<(string name, int dist)>();
            foreach (var k in bucket)
            {
                // Length filter is the cheapest pre-check (no DP table at all);
                // a string pair whose lengths differ by more than maxDistance
                // can't possibly fit within the budget.
                if (System.Math.Abs(k.Length - queryLen) > maxDistance) continue;
                var d = Levenshtein.DistanceBounded(query, k, maxDistance);
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

        static string[] ResolveBucket(string query)
        {
            if (s_prefixBuckets == null || query.Length == 0) return s_keys;
            char first = char.ToLowerInvariant(query[0]);
            return s_prefixBuckets.TryGetValue(first, out var bucket) ? bucket : s_keys;
        }

        static void EnsurePrefixBucketsBuilt()
        {
            if (s_prefixBuckets != null) return;
            lock (s_bucketsLock)
            {
                if (s_prefixBuckets != null) return;

                var groups = new Dictionary<char, List<string>>(64);
                foreach (var k in s_keys)
                {
                    if (k.Length == 0) continue;
                    char first = char.ToLowerInvariant(k[0]);
                    if (!groups.TryGetValue(first, out var list))
                    {
                        list = new List<string>();
                        groups[first] = list;
                    }
                    list.Add(k);
                }
                var buckets = new Dictionary<char, string[]>(groups.Count);
                foreach (var kv in groups) buckets[kv.Key] = kv.Value.ToArray();
                s_prefixBuckets = buckets;
            }
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
            var requestedDocsVersion = UnityVersionCompat.CurrentDocsVersion();
            var path = ResolveDataPath(requestedDocsVersion, out var loadedDocsVersion);
            if (path == null)
            {
                if (s_index != null || s_loadError != null) return;
                s_loadError = $"could not resolve bundled docs file for Unity docs {requestedDocsVersion} (fallback {FallbackDocsVersion})";
                return;
            }
            if (!File.Exists(path))
            {
                if (s_index != null || s_loadError != null) return;
                s_loadError = $"bundled docs file missing: {path}";
                return;
            }

            lock (s_lock)
            {
                var info = new FileInfo(path);
                if (IsLoadedDataCurrent(path, info))
                    return;

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
                    s_loadedDocsVersion = loadedDocsVersion;
                    s_loadedDataPath = path;
                    s_loadedDataLength = info.Length;
                    s_loadedDataLastWriteUtc = info.LastWriteTimeUtc;
                    var keys = new string[index.Count];
                    int i = 0;
                    foreach (var k in index.Keys) keys[i++] = k;
                    s_keys = keys;
                    s_loadError = null;
                    s_prefixBuckets = null;
                }
                catch (Exception ex)
                {
                    s_loadError = $"failed to load {path}: {ex.Message}";
                    s_index = null;
                    s_keys = null;
                    s_loadedDocsVersion = null;
                    s_loadedDataPath = null;
                    s_loadedDataLength = 0;
                    s_loadedDataLastWriteUtc = default(DateTime);
                    s_prefixBuckets = null;
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

        static string ResolveDataPath(string requestedDocsVersion, out string loadedDocsVersion)
        {
            loadedDocsVersion = null;
            var candidateVersions = CandidateDocsVersions(requestedDocsVersion);

            PackageInfo pi = null;
            try { pi = PackageInfo.FindForAssembly(typeof(UnityDocsStore).Assembly); }
            catch { /* fall through to the AssetDatabase-based fallback */ }

            if (pi != null && !string.IsNullOrEmpty(pi.resolvedPath))
            {
                foreach (var version in candidateVersions)
                {
                    foreach (var fileName in DocsFileNames(version))
                    {
                        var path = Path.Combine(pi.resolvedPath, DataDir, fileName);
                        if (File.Exists(path))
                        {
                            loadedDocsVersion = version;
                            return path;
                        }
                    }
                }
            }

            // Fallback for in-project (non-UPM) checkouts: search via
            // AssetDatabase so embedded copies in Assets/ still resolve.
            foreach (var version in candidateVersions)
            {
                foreach (var fileName in DocsFileNames(version))
                {
                    var guids = AssetDatabase.FindAssets(DocsAssetSearch(fileName));
                    foreach (var g in guids)
                    {
                        var assetPath = AssetDatabase.GUIDToAssetPath(g);
                        if (assetPath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            loadedDocsVersion = version;
                            return assetPath;
                        }
                    }
                }
            }
            return null;
        }

        static List<string> CandidateDocsVersions(string requestedDocsVersion)
        {
            var versions = new List<string>();
            if (!string.IsNullOrEmpty(requestedDocsVersion))
                versions.Add(requestedDocsVersion);
            if (!versions.Contains(FallbackDocsVersion))
                versions.Add(FallbackDocsVersion);
            return versions;
        }

        static IEnumerable<string> DocsFileNames(string docsVersion)
        {
            yield return "unity_docs_" + docsVersion + ".jsonl.gz.bytes";
            if (docsVersion == UnityVersionCompat.Docs6000_0)
                yield return "unity_docs_6.0.jsonl.gz.bytes";
        }

        static string DocsAssetSearch(string fileName)
        {
            const string suffix = ".jsonl.gz.bytes";
            var stem = fileName.EndsWith(suffix, StringComparison.Ordinal)
                ? fileName.Substring(0, fileName.Length - suffix.Length)
                : Path.GetFileNameWithoutExtension(fileName);
            return stem + " t:DefaultAsset";
        }
    }
}
