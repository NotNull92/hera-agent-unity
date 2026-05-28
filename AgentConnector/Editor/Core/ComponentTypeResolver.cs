using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HeraAgent
{
    /// <summary>
    /// Resolves a CLI-provided component type name (short like "Rigidbody" or
    /// fully-qualified like "UnityEngine.Rigidbody") to the corresponding
    /// System.Type. Backed by TypeCache so the scan stays roughly free after
    /// the first call. Used by find_gameobjects and manage_components.
    /// </summary>
    public static class ComponentTypeResolver
    {
        public static Type Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var t in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (t.Name == name || t.FullName == name)
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Returns up to <paramref name="max"/> component type names within
        /// <paramref name="maxDistance"/> Levenshtein distance of the input.
        /// Powers "did you mean" hints when Resolve returns null.
        /// </summary>
        public static List<string> SuggestSimilar(string name, int maxDistance = 2, int max = 5)
        {
            if (string.IsNullOrEmpty(name)) return new List<string>();

            var candidates = new List<(string name, int dist)>();
            foreach (var t in TypeCache.GetTypesDerivedFrom<Component>())
            {
                var d = LevenshteinDistance(name, t.Name);
                if (d <= maxDistance) candidates.Add((t.Name, d));
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

        static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : b.Length;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int del = d[i - 1, j] + 1;
                    int ins = d[i, j - 1] + 1;
                    int sub = d[i - 1, j - 1] + cost;
                    int min = del < ins ? del : ins;
                    d[i, j] = min < sub ? min : sub;
                }
            }
            return d[a.Length, b.Length];
        }
    }
}
