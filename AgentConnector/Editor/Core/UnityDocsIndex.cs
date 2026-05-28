using System.Collections.Generic;
using System.IO;

namespace HeraAgent
{
    /// <summary>
    /// Lazy index of ScriptReference filenames (sans .html) used to power
    /// "did you mean" suggestions when a docs query misses. Built on first
    /// use per docs_root and re-used until the path changes (or the static
    /// state is dropped by a domain reload).
    /// </summary>
    public static class UnityDocsIndex
    {
        static string s_builtFor;
        static string[] s_files = System.Array.Empty<string>();
        static readonly object s_lock = new object();

        public static string[] EnsureBuilt(string docsRoot)
        {
            lock (s_lock)
            {
                if (s_builtFor == docsRoot && s_files != null) return s_files;

                var srDir = Path.Combine(docsRoot ?? string.Empty, "ScriptReference");
                if (!Directory.Exists(srDir))
                {
                    s_files = System.Array.Empty<string>();
                    s_builtFor = docsRoot;
                    return s_files;
                }

                var paths = Directory.GetFiles(srDir, "*.html", SearchOption.TopDirectoryOnly);
                var names = new List<string>(paths.Length);
                foreach (var p in paths)
                {
                    var name = Path.GetFileNameWithoutExtension(p);
                    if (!string.IsNullOrEmpty(name)) names.Add(name);
                }
                s_files = names.ToArray();
                s_builtFor = docsRoot;
                return s_files;
            }
        }

        public static int Count(string docsRoot) => EnsureBuilt(docsRoot).Length;

        /// <summary>
        /// Returns up to <paramref name="max"/> filenames within
        /// <paramref name="maxDistance"/> Levenshtein distance of the query.
        /// </summary>
        public static List<string> SuggestSimilar(
            string docsRoot, string query, int maxDistance = 3, int max = 5)
        {
            var files = EnsureBuilt(docsRoot);
            if (files.Length == 0 || string.IsNullOrEmpty(query))
                return new List<string>();

            var candidates = new List<(string name, int dist)>();
            foreach (var name in files)
            {
                var d = Levenshtein.Distance(query, name);
                if (d <= maxDistance) candidates.Add((name, d));
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
    }
}
