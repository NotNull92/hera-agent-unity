using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "find_gameobjects",
        Description = "Search loaded-scene GameObjects with filters (name substring, exact tag, layer name or index, component type, hierarchy path glob) and built-in pagination. Returns shallow entries {instance_id, name, path, active}. Prefer this over `exec` for any 'list/find/count GameObjects with X' question — it scales without serializing the whole hierarchy and respects inactive subtrees by default.",
        Examples = new[]
        {
            "find_gameobjects --name Player",
            "find_gameobjects --tag Enemy --include_inactive false",
            "find_gameobjects --component Rigidbody --limit 20",
            "find_gameobjects --path_glob /Root/**/Pickup",
            "find_gameobjects --layer UI",
            "find_gameobjects --limit 50 --offset 100",
        },
        ExampleDescriptions = new[]
        {
            "Substring match on name, case-insensitive",
            "Active enemies only",
            "Every GameObject carrying a Rigidbody, capped at 20",
            "Glob match on hierarchy path (** spans multiple segments)",
            "Layer filter accepts a layer name or an integer index",
            "Skip the first 100 matches — pair limit + offset for pagination",
        })]
    public static class FindGameObjects
    {
        public class Parameters
        {
            [ToolParameter("Name substring filter (case-insensitive)")]
            public string Name { get; set; }

            [ToolParameter("Exact tag match")]
            public string Tag { get; set; }

            [ToolParameter("Layer filter — accepts layer name ('UI') or integer index (5)")]
            public string Layer { get; set; }

            [ToolParameter("Component type — short name ('Rigidbody') or fully-qualified ('UnityEngine.Rigidbody')")]
            public string Component { get; set; }

            [ToolParameter("Hierarchy path glob — '*' matches a single segment, '**' matches multiple. e.g. '/Root/**/Player'")]
            public string PathGlob { get; set; }

            [ToolParameter("Include inactive GameObjects (default true). False = activeInHierarchy only.")]
            public bool? IncludeInactive { get; set; }

            [ToolParameter("Max results to return (default 50; pass 0 for no cap)")]
            public int? Limit { get; set; }

            [ToolParameter("Skip the first N matches (default 0). Pair with limit for pagination.")]
            public int? Offset { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            if (parameters == null)
                return new ErrorResponse("Parameters cannot be null.");

            var p = new ToolParams(parameters);

            string nameFilter = p.Get("name");
            string tagFilter = p.Get("tag");
            string layerStr = p.Get("layer");
            string componentName = p.Get("component");
            string pathGlob = p.Get("path_glob");
            bool includeInactive = p.GetBool("include_inactive", true);
            int limit = p.GetInt("limit", 50) ?? 50;
            int offset = p.GetInt("offset", 0) ?? 0;
            if (limit < 0) limit = 0;
            if (offset < 0) offset = 0;

            int? layerIndex = null;
            if (!string.IsNullOrEmpty(layerStr))
            {
                if (int.TryParse(layerStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                {
                    if (idx < 0 || idx > 31)
                        return new ErrorResponse($"Invalid 'layer' index: {idx}. Must be 0..31.");
                    layerIndex = idx;
                }
                else
                {
                    var resolved = LayerMask.NameToLayer(layerStr);
                    if (resolved < 0)
                        return new ErrorResponse($"Unknown layer name: '{layerStr}'. Use the Tags & Layers inspector to add it, or pass an integer index.");
                    layerIndex = resolved;
                }
            }

            Type componentType = null;
            if (!string.IsNullOrEmpty(componentName))
            {
                componentType = ResolveComponentType(componentName);
                if (componentType == null)
                    return new ErrorResponse(
                        "UNKNOWN_COMPONENT_TYPE",
                        $"Component type not found: '{componentName}'. Use the short name (e.g. 'Rigidbody') or a fully-qualified name (e.g. 'UnityEngine.Rigidbody').");
            }

            Regex pathRegex = null;
            if (!string.IsNullOrEmpty(pathGlob))
            {
                try { pathRegex = GlobToRegex(pathGlob); }
                catch (ArgumentException ex)
                {
                    return new ErrorResponse($"Invalid path_glob '{pathGlob}': {ex.Message}");
                }
            }

            // FindObjectsOfTypeAll returns scene objects AND prefab assets AND
            // hidden internal objects. Strip persistent assets and hierarchy-
            // hidden flags so we only return what a user would see in the
            // Hierarchy window.
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            var matched = new List<GameObject>(64);

            foreach (var go in all)
            {
                if (go == null) continue;
                if (EditorUtility.IsPersistent(go)) continue;
                if (!go.scene.IsValid()) continue;
                if ((go.hideFlags & HideFlags.HideInHierarchy) != 0) continue;

                if (!includeInactive && !go.activeInHierarchy) continue;
                if (!string.IsNullOrEmpty(nameFilter) &&
                    go.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!string.IsNullOrEmpty(tagFilter) && !go.CompareTag(tagFilter)) continue;
                if (layerIndex.HasValue && go.layer != layerIndex.Value) continue;
                if (componentType != null && go.GetComponent(componentType) == null) continue;
                if (pathRegex != null && !pathRegex.IsMatch(HierarchyPath.Build(go.transform))) continue;

                matched.Add(go);
            }

            // Deterministic order so pagination is stable: by hierarchy path.
            matched.Sort((a, b) => string.CompareOrdinal(
                HierarchyPath.Build(a.transform),
                HierarchyPath.Build(b.transform)));

            int total = matched.Count;
            var results = new List<object>();
            int returned = 0;
            for (int i = offset; i < matched.Count; i++)
            {
                if (limit > 0 && returned >= limit) break;
                results.Add(BuildShallow(matched[i]));
                returned++;
            }

            return new SuccessResponse(
                $"{returned} of {total} matched (offset {offset}).",
                new
                {
                    total,
                    returned,
                    offset,
                    limit,
                    has_more = limit > 0 && offset + returned < total,
                    results,
                });
        }

        // ---- helpers ----

        private static object BuildShallow(GameObject go)
        {
            return new
            {
                instance_id = go.GetInstanceID(),
                name = go.name,
                path = HierarchyPath.Build(go.transform),
                scene = go.scene.name,
                active = go.activeInHierarchy,
            };
        }

        // TypeCache scan is ~10ms cold and cached afterwards. Accepts the
        // short name (last segment) or the full namespace-qualified name.
        // Tied to Component subclasses only — querying for a non-Component
        // type would never resolve via GetComponent anyway.
        private static Type ResolveComponentType(string name)
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (t.Name == name || t.FullName == name)
                    return t;
            }
            return null;
        }

        private static Regex GlobToRegex(string glob)
        {
            var sb = new StringBuilder("^");
            for (int i = 0; i < glob.Length; i++)
            {
                char c = glob[i];
                if (c == '*')
                {
                    if (i + 1 < glob.Length && glob[i + 1] == '*')
                    {
                        sb.Append(".*");
                        i++;
                    }
                    else
                    {
                        sb.Append("[^/]*");
                    }
                }
                else if (c == '?')
                {
                    sb.Append("[^/]");
                }
                else
                {
                    sb.Append(Regex.Escape(c.ToString()));
                }
            }
            sb.Append('$');
            return new Regex(sb.ToString());
        }
    }
}
