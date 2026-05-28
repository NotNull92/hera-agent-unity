using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace HeraAgent
{
    /// <summary>
    /// Parses offline Unity ScriptReference HTML into a slim shape suitable
    /// for an AI agent. No HtmlAgilityPack dependency — Unity's docs HTML
    /// uses a stable handful of structural attributes (signature-CS sig-block,
    /// h3 Description, switch-link anchor) that hold up under plain regex.
    ///
    /// Parsed results are kept in a small in-memory LRU (32 entries) keyed by
    /// the relative filename. Domain reloads drop the cache by re-running the
    /// static constructor, which is fine — re-parsing is ~5-15 ms cold.
    /// </summary>
    public static class UnityDocsParser
    {
        public class Doc
        {
            public string title;
            public string signature;
            public string summary;
            public string manual_url;
            public string scriptreference_url;
            public string unity_version;
        }

        const int CacheCapacity = 32;
        static readonly LinkedList<KeyValuePair<string, Doc>> s_list = new LinkedList<KeyValuePair<string, Doc>>();
        static readonly Dictionary<string, LinkedListNode<KeyValuePair<string, Doc>>> s_map =
            new Dictionary<string, LinkedListNode<KeyValuePair<string, Doc>>>();
        static readonly object s_cacheLock = new object();

        static readonly Regex s_h1 = new Regex(
            @"<h1\s+class=""heading\s+inherit"">([\s\S]*?)</h1>",
            RegexOptions.Compiled);

        static readonly Regex s_sig = new Regex(
            @"<div\s+class=""signature-CS\s+sig-block"">([\s\S]*?)</div>",
            RegexOptions.Compiled);

        static readonly Regex s_summary = new Regex(
            @"<h3>\s*Description\s*</h3>\s*<p>([\s\S]*?)</p>",
            RegexOptions.Compiled);

        // Two orderings — class= before href= and href= before class= — both
        // appear in the wild. Try class-first, then href-first.
        static readonly Regex s_manualClassFirst = new Regex(
            @"<a[^>]*class\s*=\s*['""][^'""]*switch-link[^'""]*['""][^>]*href\s*=\s*['""]([^'""]+)['""]",
            RegexOptions.Compiled);
        static readonly Regex s_manualHrefFirst = new Regex(
            @"<a[^>]*href\s*=\s*['""]([^'""]+)['""][^>]*class\s*=\s*['""][^'""]*switch-link",
            RegexOptions.Compiled);

        static readonly Regex s_version = new Regex(
            @"Version:\s*<b>Unity\s+(\d+\.\d+)</b>",
            RegexOptions.Compiled);

        static readonly Regex s_htmlTag = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        static readonly Regex s_whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Reads a ScriptReference .html file relative to <paramref name="docsRoot"/>
        /// and returns the parsed doc. LRU-cached.
        /// </summary>
        public static (Doc doc, string err) ReadAndParse(string docsRoot, string relativeFilename)
        {
            if (string.IsNullOrEmpty(docsRoot)) return (null, "docs_root is empty");
            if (string.IsNullOrEmpty(relativeFilename)) return (null, "filename is empty");

            if (TryGetCached(relativeFilename, out var cached))
                return (cached, null);

            var fullPath = Path.Combine(docsRoot, "ScriptReference", relativeFilename);
            if (!File.Exists(fullPath))
                return (null, $"file does not exist: {fullPath}");

            string html;
            try { html = File.ReadAllText(fullPath); }
            catch (Exception ex) { return (null, $"cannot read {fullPath}: {ex.Message}"); }

            var doc = Parse(html, relativeFilename);
            SetCached(relativeFilename, doc);
            return (doc, null);
        }

        /// <summary>
        /// Pure-string parse used by tests and by ReadAndParse. Filename only
        /// fills the scriptreference_url field.
        /// </summary>
        public static Doc Parse(string html, string relativeFilename)
        {
            var doc = new Doc
            {
                scriptreference_url = "ScriptReference/" + (relativeFilename ?? string.Empty),
            };

            var h1m = s_h1.Match(html);
            if (h1m.Success) doc.title = CleanText(h1m.Groups[1].Value);

            var sigm = s_sig.Match(html);
            if (sigm.Success) doc.signature = CleanText(sigm.Groups[1].Value);

            var summarym = s_summary.Match(html);
            if (summarym.Success) doc.summary = CleanText(summarym.Groups[1].Value);

            var manualm = s_manualClassFirst.Match(html);
            if (!manualm.Success) manualm = s_manualHrefFirst.Match(html);
            if (manualm.Success)
            {
                var raw = manualm.Groups[1].Value;
                doc.manual_url = NormalizeRelativeUrl(raw);
            }

            var verm = s_version.Match(html);
            if (verm.Success) doc.unity_version = verm.Groups[1].Value;

            return doc;
        }

        public static void ClearCache()
        {
            lock (s_cacheLock)
            {
                s_list.Clear();
                s_map.Clear();
            }
        }

        // ---- helpers ----

        static bool TryGetCached(string key, out Doc value)
        {
            lock (s_cacheLock)
            {
                if (s_map.TryGetValue(key, out var node))
                {
                    s_list.Remove(node);
                    s_list.AddFirst(node);
                    value = node.Value.Value;
                    return true;
                }
                value = null;
                return false;
            }
        }

        static void SetCached(string key, Doc value)
        {
            lock (s_cacheLock)
            {
                if (s_map.TryGetValue(key, out var existing))
                {
                    s_list.Remove(existing);
                    existing.Value = new KeyValuePair<string, Doc>(key, value);
                    s_list.AddFirst(existing);
                    return;
                }
                if (s_map.Count >= CacheCapacity)
                {
                    var lru = s_list.Last;
                    s_list.RemoveLast();
                    s_map.Remove(lru.Value.Key);
                }
                var newNode = new LinkedListNode<KeyValuePair<string, Doc>>(
                    new KeyValuePair<string, Doc>(key, value));
                s_list.AddFirst(newNode);
                s_map[key] = newNode;
            }
        }

        static string CleanText(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s_htmlTag.Replace(s, " ");
            s = s.Replace("&lt;", "<")
                 .Replace("&gt;", ">")
                 .Replace("&amp;", "&")
                 .Replace("&quot;", "\"")
                 .Replace("&#39;", "'")
                 .Replace("&nbsp;", " ");
            s = s_whitespace.Replace(s, " ").Trim();
            return s;
        }

        static string NormalizeRelativeUrl(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            var s = raw.Replace('\\', '/');
            // Trim leading "../" and "./" segments so the path becomes
            // Manual/... or ScriptReference/... that the docs root resolves
            // against directly.
            while (s.StartsWith("../")) s = s.Substring(3);
            while (s.StartsWith("./")) s = s.Substring(2);
            return s;
        }
    }
}
