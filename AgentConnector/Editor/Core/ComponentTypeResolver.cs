using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HeraAgent
{
    /// <summary>
    /// Resolves a CLI-provided component type name (short like "Rigidbody" or
    /// fully-qualified like "UnityEngine.Rigidbody") to the corresponding
    /// System.Type. Backed by a snapshot of TypeCache that is rebuilt once per
    /// domain reload, so repeated lookups avoid scanning the entire derived-type
    /// graph. Used by find_gameobjects and manage_components.
    /// </summary>
    [InitializeOnLoad]
    public static class ComponentTypeResolver
    {
        private static readonly object s_Gate = new object();
        private static Dictionary<string, Type> s_ByName;
        private static Dictionary<string, Type> s_ByFullName;

        static ComponentTypeResolver()
        {
            AssemblyReloadEvents.afterAssemblyReload += RebuildCache;
            EditorApplication.quitting += ClearCache;
            RebuildCache();
        }

        private static void RebuildCache()
        {
            var byName = new Dictionary<string, Type>(StringComparer.Ordinal);
            var byFullName = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (var t in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (t == null) continue;
                if (!byName.ContainsKey(t.Name)) byName[t.Name] = t;
                if (!byFullName.ContainsKey(t.FullName)) byFullName[t.FullName] = t;
            }

            lock (s_Gate)
            {
                s_ByName = byName;
                s_ByFullName = byFullName;
            }
        }

        private static void ClearCache()
        {
            lock (s_Gate)
            {
                s_ByName = null;
                s_ByFullName = null;
            }
        }

        private static void EnsureCache()
        {
            lock (s_Gate)
            {
                if (s_ByName != null) return;
            }
            RebuildCache();
        }

        public static Type Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            EnsureCache();
            lock (s_Gate)
            {
                if (s_ByName.TryGetValue(name, out var t)) return t;
                if (s_ByFullName.TryGetValue(name, out t)) return t;
                return null;
            }
        }

        /// <summary>
        /// Returns up to <paramref name="max"/> component type names within
        /// <paramref name="maxDistance"/> Levenshtein distance of the input.
        /// Powers "did you mean" hints when Resolve returns null.
        /// </summary>
        public static List<string> SuggestSimilar(string name, int maxDistance = 2, int max = 5)
        {
            if (string.IsNullOrEmpty(name)) return new List<string>();

            EnsureCache();
            var candidates = new List<(string name, int dist)>();
            lock (s_Gate)
            {
                foreach (var pair in s_ByName)
                {
                    var d = Levenshtein.Distance(name, pair.Key);
                    if (d <= maxDistance) candidates.Add((pair.Key, d));
                }
            }
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (n, _) in candidates)
            {
                if (!seen.Add(n)) continue;
                result.Add(n);
                if (result.Count >= max) break;
            }
            return result;
        }
    }
}
