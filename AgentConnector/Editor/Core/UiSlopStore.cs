using System;
using System.Collections.Generic;

namespace HeraAgent
{
    /// <summary>
    /// Loads the connector-bundled Unity UI-slop taxonomy (authored against live
    /// hera measurement and per-version editor-binary reflection) into a keyed
    /// dictionary on first access. Powers the `ui_slop` tool and the Unity
    /// De-slop Mode (Beta) hints. The loading machinery lives in
    /// <see cref="BundleStore{TEntry}"/>; what stays here is the entry shape, the
    /// fixed A-E area index, and the ui_system-aware check selector.
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

        // Areas are inspected in parallel but executed in this fixed order —
        // upstream commits dissolve downstream conflicts (A flattens surfaces
        // before E disciplines the palette, etc.).
        static readonly string[] AreaOrder = { "A", "B", "C", "D", "E" };

        static readonly BundleStore<Entry> s_data = new BundleStore<Entry>(
            typeof(UiSlopStore).Assembly, "ui_slop_1.0.jsonl.gz.bytes", "ui-slop", e => e.id);

        /// <summary>
        /// Returns the entry for an exact id match, or null on miss.
        /// </summary>
        public static Entry Lookup(string id) => s_data.Lookup(id);

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
        /// Returns up to <paramref name="max"/> ids within
        /// <paramref name="maxDistance"/> Levenshtein distance of the query.
        /// </summary>
        public static List<string> SuggestSimilar(string query, int maxDistance = 3, int max = 5)
            => s_data.SuggestSimilar(query, maxDistance, max);

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
        /// Ordered taxonomy index — one { area, tells: [{id, severity, tell}] }
        /// group per area, A through E. Areas absent from the bundle are
        /// skipped; unknown areas are appended in encounter order.
        /// </summary>
        public static List<object> BuildIndex()
        {
            var byArea = new Dictionary<string, List<Entry>>();
            var extraAreas = new List<string>();
            foreach (var entry in s_data.Values)
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

            var groups = new List<object>();
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
    }
}
