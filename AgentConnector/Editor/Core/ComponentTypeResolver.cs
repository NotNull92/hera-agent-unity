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
                var d = Levenshtein.Distance(name, t.Name);
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
    }
}
