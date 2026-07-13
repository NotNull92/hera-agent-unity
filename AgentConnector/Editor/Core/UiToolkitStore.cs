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
    /// Loads the connector-bundled UI Toolkit schema for the running Editor
    /// version into memory on first access. The bundle is produced by
    /// tools/build-uitk-schema (in-editor reflection over UIElements → gzipped
    /// JSONL) and ships inside the UPM package, so consumers don't reflect over
    /// UIElements per call.
    ///
    /// Mirrors UnityDocsStore's version-bucketed load/reload pattern (the schema
    /// is Unity-version-dependent). It answers the UI Toolkit emitter/fixer's
    /// questions when <c>ui_system</c> is <c>uitk</c>: is this a valid runtime
    /// element in this Unity version? what attributes does it take? is this USS
    /// property supported / animatable?
    /// </summary>
    public static class UiToolkitStore
    {
        public class AttributeInfo
        {
            public string name;
            public string type;

            // `default` is a C# keyword; map the JSON key explicitly.
            [JsonProperty("default")]
            public string defaultValue;
        }

        public class Element
        {
            public string element;
            public string full_type;
            public string surface;
            public List<AttributeInfo> attributes;
        }

        public class UssProperty
        {
            public string name;
            public bool animatable;
        }

        // One raw JSONL line, discriminated by `kind` (meta / element / structural / uss).
        class Line
        {
            public string kind;
            public string element;
            public string full_type;
            public string surface;
            public List<AttributeInfo> attributes;
            public string name;
            public bool animatable;
            public string unity_version;
            public string bucket;
            public bool uxml_element_attribute;
            public string uxml_traits;
        }

        const string DataDir = "Editor/Data";
        const string FallbackBucket = UnityVersionCompat.Docs6000_0;

        static Dictionary<string, Element> s_elements;      // key: element (uxml tag) name
        static HashSet<string> s_structural;                // UXML directive tags (UXML/Template/Style/AttributeOverrides)
        static Dictionary<string, UssProperty> s_uss;       // key: USS property name
        static string[] s_elementNames;
        static string[] s_ussNames;
        static string s_loadedBucket;
        static string s_unityVersion;
        static string s_uxmlTraits;
        static bool s_uxmlElementAttribute;
        static string s_loadError;
        static string s_loadedDataPath;
        static long s_loadedDataLength;
        static DateTime s_loadedDataLastWriteUtc;
        static readonly object s_lock = new object();

        /// <summary>Returns the element entry for an exact tag-name match, or null.</summary>
        public static Element GetElement(string name)
        {
            EnsureLoaded();
            if (s_elements == null || string.IsNullOrEmpty(name)) return null;
            return s_elements.TryGetValue(name, out var e) ? e : null;
        }

        /// <summary>True when <paramref name="name"/> is a built-in runtime element in this version.</summary>
        public static bool IsElement(string name) => GetElement(name) != null;

        /// <summary>True when <paramref name="name"/> is a UXML structural directive (UXML/Template/Style/AttributeOverrides).</summary>
        public static bool IsStructural(string name)
        {
            EnsureLoaded();
            return s_structural != null && !string.IsNullOrEmpty(name) && s_structural.Contains(name);
        }

        /// <summary>Returns the USS property entry for an exact match, or null.</summary>
        public static UssProperty GetUss(string name)
        {
            EnsureLoaded();
            if (s_uss == null || string.IsNullOrEmpty(name)) return null;
            return s_uss.TryGetValue(name, out var u) ? u : null;
        }

        /// <summary>True when <paramref name="name"/> is a supported USS property in this version.</summary>
        public static bool IsUssProperty(string name) => GetUss(name) != null;

        /// <summary>True when the USS property exists and is animatable (the Game Feel juice substrate).</summary>
        public static bool IsAnimatable(string name)
        {
            var u = GetUss(name);
            return u != null && u.animatable;
        }

        public static int ElementCount { get { EnsureLoaded(); return s_elements == null ? 0 : s_elements.Count; } }
        public static int UssCount { get { EnsureLoaded(); return s_uss == null ? 0 : s_uss.Count; } }
        public static string LoadedBucket { get { EnsureLoaded(); return s_loadedBucket; } }
        public static string UnityVersion { get { EnsureLoaded(); return s_unityVersion; } }

        /// <summary>UXML authoring API state for this version: "present" / "obsolete" / "absent".</summary>
        public static string UxmlTraits { get { EnsureLoaded(); return s_uxmlTraits; } }

        /// <summary>True when this version has the [UxmlElement] source-generated authoring API.</summary>
        public static bool SupportsUxmlElementAttribute { get { EnsureLoaded(); return s_uxmlElementAttribute; } }

        public static string LoadError { get { EnsureLoaded(); return s_loadError; } }

        public static IReadOnlyList<string> ElementNames { get { EnsureLoaded(); return s_elementNames ?? Array.Empty<string>(); } }
        public static IReadOnlyList<string> UssNames { get { EnsureLoaded(); return s_ussNames ?? Array.Empty<string>(); } }

        /// <summary>
        /// did-you-mean over element tag names. Full-scan Levenshtein — the corpus
        /// is ~50 elements, so the 3-layer prefix optimization UnityDocsStore uses
        /// over its 30k pages would be premature here (same call as GameFeelStore).
        /// </summary>
        public static List<string> SuggestElements(string query, int maxDistance = 3, int max = 5)
        {
            EnsureLoaded();
            return Suggest(s_elementNames, query, maxDistance, max);
        }

        /// <summary>did-you-mean over USS property names (full-scan, corpus ~100).</summary>
        public static List<string> SuggestUss(string query, int maxDistance = 3, int max = 5)
        {
            EnsureLoaded();
            return Suggest(s_ussNames, query, maxDistance, max);
        }

        static List<string> Suggest(string[] names, string query, int maxDistance, int max)
        {
            var result = new List<string>();
            if (names == null || names.Length == 0 || string.IsNullOrEmpty(query)) return result;

            var candidates = new List<(string name, int dist)>();
            foreach (var k in names)
            {
                var d = Levenshtein.DistanceBounded(query, k, maxDistance);
                if (d <= maxDistance) candidates.Add((k, d));
            }
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            foreach (var (n, _) in candidates)
            {
                result.Add(n);
                if (result.Count >= max) break;
            }
            return result;
        }

        static void EnsureLoaded()
        {
            // Already resolved (loaded or terminally failed). The bundled bucket is
            // fixed for the Unity version, which can't change without a domain
            // reload (statics reset then), so skip the per-call path resolution once
            // we have an answer.
            if (s_elements != null || s_loadError != null) return;
            var requestedBucket = UnityVersionCompat.CurrentDocsVersion();
            var path = ResolveDataPath(requestedBucket, out var loadedBucket);
            if (path == null)
            {
                if (s_elements != null || s_loadError != null) return;
                s_loadError = $"could not resolve bundled UI Toolkit schema for bucket {requestedBucket} (fallback {FallbackBucket})";
                return;
            }
            if (!File.Exists(path))
            {
                if (s_elements != null || s_loadError != null) return;
                s_loadError = $"bundled UI Toolkit schema missing: {path}";
                return;
            }

            lock (s_lock)
            {
                var info = new FileInfo(path);
                if (IsLoadedDataCurrent(path, info))
                    return;

                try
                {
                    var elements = new Dictionary<string, Element>(64);
                    var structural = new HashSet<string>();
                    var uss = new Dictionary<string, UssProperty>(128);
                    string unityVersion = null, uxmlTraits = null;
                    bool uxmlElementAttribute = false;

                    using (var fs = File.OpenRead(path))
                    using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gz))
                    {
                        string raw;
                        while ((raw = reader.ReadLine()) != null)
                        {
                            if (raw.Length == 0) continue;
                            Line line;
                            try { line = JsonConvert.DeserializeObject<Line>(raw); }
                            catch { continue; }
                            if (line == null) continue;

                            switch (line.kind)
                            {
                                case "element":
                                    if (string.IsNullOrEmpty(line.element)) break;
                                    elements[line.element] = new Element
                                    {
                                        element = line.element,
                                        full_type = line.full_type,
                                        surface = line.surface,
                                        attributes = line.attributes ?? new List<AttributeInfo>(),
                                    };
                                    break;
                                case "structural":
                                    if (!string.IsNullOrEmpty(line.element)) structural.Add(line.element);
                                    break;
                                case "uss":
                                    if (string.IsNullOrEmpty(line.name)) break;
                                    uss[line.name] = new UssProperty { name = line.name, animatable = line.animatable };
                                    break;
                                case "meta":
                                    unityVersion = line.unity_version;
                                    uxmlTraits = line.uxml_traits;
                                    uxmlElementAttribute = line.uxml_element_attribute;
                                    break;
                            }
                        }
                    }

                    s_elements = elements;
                    s_structural = structural;
                    s_uss = uss;
                    s_elementNames = CopyKeys(elements);
                    s_ussNames = CopyKeys(uss);
                    s_loadedBucket = loadedBucket;
                    s_unityVersion = unityVersion;
                    s_uxmlTraits = uxmlTraits;
                    s_uxmlElementAttribute = uxmlElementAttribute;
                    s_loadedDataPath = path;
                    s_loadedDataLength = info.Length;
                    s_loadedDataLastWriteUtc = info.LastWriteTimeUtc;
                    s_loadError = null;
                }
                catch (Exception ex)
                {
                    s_loadError = $"failed to load {path}: {ex.Message}";
                    s_elements = null;
                    s_structural = null;
                    s_uss = null;
                    s_elementNames = null;
                    s_ussNames = null;
                    s_loadedBucket = null;
                    s_unityVersion = null;
                    s_uxmlTraits = null;
                    s_uxmlElementAttribute = false;
                    s_loadedDataPath = null;
                    s_loadedDataLength = 0;
                    s_loadedDataLastWriteUtc = default(DateTime);
                }
            }
        }

        static string[] CopyKeys<T>(Dictionary<string, T> dict)
        {
            var keys = new string[dict.Count];
            int i = 0;
            foreach (var k in dict.Keys) keys[i++] = k;
            return keys;
        }

        static bool IsLoadedDataCurrent(string path, FileInfo info)
        {
            return s_elements != null
                && s_loadError == null
                && string.Equals(s_loadedDataPath, path, StringComparison.OrdinalIgnoreCase)
                && s_loadedDataLength == info.Length
                && s_loadedDataLastWriteUtc == info.LastWriteTimeUtc;
        }

        static string ResolveDataPath(string requestedBucket, out string loadedBucket)
        {
            loadedBucket = null;
            var candidateBuckets = CandidateBuckets(requestedBucket);

            PackageInfo pi = null;
            try { pi = PackageInfo.FindForAssembly(typeof(UiToolkitStore).Assembly); }
            catch { /* fall through to the AssetDatabase-based fallback */ }

            if (pi != null && !string.IsNullOrEmpty(pi.resolvedPath))
            {
                foreach (var bucket in candidateBuckets)
                {
                    var path = Path.Combine(pi.resolvedPath, DataDir, FileName(bucket));
                    if (File.Exists(path))
                    {
                        loadedBucket = bucket;
                        return path;
                    }
                }
            }

            // Fallback for in-project (non-UPM) checkouts: search via AssetDatabase
            // so embedded copies in Assets/ still resolve.
            foreach (var bucket in candidateBuckets)
            {
                var fileName = FileName(bucket);
                var guids = AssetDatabase.FindAssets("uitk_schema_" + bucket + " t:DefaultAsset");
                foreach (var g in guids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(g);
                    if (assetPath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        loadedBucket = bucket;
                        return assetPath;
                    }
                }
            }
            return null;
        }

        static List<string> CandidateBuckets(string requestedBucket)
        {
            var buckets = new List<string>();
            if (!string.IsNullOrEmpty(requestedBucket))
                buckets.Add(requestedBucket);
            if (!buckets.Contains(FallbackBucket))
                buckets.Add(FallbackBucket);
            return buckets;
        }

        static string FileName(string bucket) => "uitk_schema_" + bucket + ".jsonl.gz.bytes";
    }
}
