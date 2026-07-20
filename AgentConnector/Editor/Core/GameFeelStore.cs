using System;
using System.Collections.Generic;

namespace HeraAgent
{
    /// <summary>
    /// Loads the connector-bundled Game Feel knowledge base (Game Feel &amp; Juice
    /// Bible + Ethical Engagement Game Feel Framework) into a keyed dictionary on
    /// first access. Powers the `game_feel` tool and the Game Feel Mode (Beta)
    /// hints. The loading machinery lives in <see cref="BundleStore{TEntry}"/>;
    /// what stays here is the entry shape and the ethics-first category index.
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

        // Ethics leads the index deliberately — recipes are meant to be applied
        // with the ethical constraints built in, not checked afterwards.
        static readonly string[] CategoryOrder =
            { "ethics", "theory", "technique", "ui", "workflow", "anti_pattern", "checklist" };

        static readonly BundleStore<Entry> s_data = new BundleStore<Entry>(
            typeof(GameFeelStore).Assembly, "game_feel_1.0.jsonl.gz.bytes", "game-feel", e => e.key);

        /// <summary>
        /// Returns the entry for an exact key match, or null on miss.
        /// </summary>
        public static Entry Lookup(string key) => s_data.Lookup(key);

        /// <summary>
        /// Number of indexed entries; 0 if the data file failed to load.
        /// </summary>
        public static int Count => s_data.Count;

        /// <summary>
        /// Error message surfaced when the data file could not be located or
        /// decompressed. Null when the load succeeded.
        /// </summary>
        public static string LoadError => s_data.LoadError;

        /// <summary>
        /// Returns up to <paramref name="max"/> keys within
        /// <paramref name="maxDistance"/> Levenshtein distance of the query.
        /// </summary>
        public static List<string> SuggestSimilar(string query, int maxDistance = 3, int max = 5)
            => s_data.SuggestSimilar(query, maxDistance, max);

        /// <summary>
        /// Ordered topic index — one { category, topics: [{key, title}] } group
        /// per known category, ethics first. Categories absent from the bundle
        /// are skipped; unknown categories are appended in encounter order.
        /// </summary>
        public static List<object> BuildIndex()
        {
            var byCategory = new Dictionary<string, List<Entry>>();
            var extraCategories = new List<string>();
            foreach (var entry in s_data.Values)
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

            var groups = new List<object>();
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
    }
}
