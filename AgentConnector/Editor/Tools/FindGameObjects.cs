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
        Description = "Search loaded-scene GameObjects with filters (name substring, exact tag, layer name or index, component type, hierarchy path glob) and built-in pagination. Returns lean entries {instance_id, name} by default; pass --fields, --ids, or --names to control payload size. Prefer this over `exec` for any 'list/find/count GameObjects with X' question — it scales without serializing the whole hierarchy and respects inactive subtrees by default.",
        Examples = new[]
        {
            "find_gameobjects --name Player",
            "find_gameobjects --tag Enemy --include_inactive false",
            "find_gameobjects --component Rigidbody --limit 20",
            "find_gameobjects --path_glob /Root/**/Pickup",
            "find_gameobjects --layer UI",
            "find_gameobjects --limit 50 --offset 100",
            "find_gameobjects --component Rigidbody --ids",
            "find_gameobjects --name Pickup --fields instance_id,name,path",
        },
        ExampleDescriptions = new[]
        {
            "Substring match on name, case-insensitive",
            "Active enemies only",
            "Every GameObject carrying a Rigidbody, capped at 20",
            "Glob match on hierarchy path (** spans multiple segments)",
            "Layer filter accepts a layer name or an integer index",
            "Skip the first 100 matches — pair limit + offset for pagination",
            "Return only instance IDs for the lowest-token handoff to manage_* tools",
            "Request extra fields only when you need them",
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

            [ToolParameter("Comma-separated output fields: instance_id, name, path, scene, active, or all. Default: instance_id,name.")]
            public string Fields { get; set; }

            [ToolParameter("Return results as bare instance IDs. Lowest-token projection; mutually exclusive with names/fields.")]
            public bool? Ids { get; set; }

            [ToolParameter("Return results as bare names. Mutually exclusive with ids/fields.")]
            public bool? Names { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            if (parameters == null)
                return new ErrorResponse("MISSING_PARAM", "Parameters cannot be null.");

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

            var projection = ResolveProjection(p);
            if (!projection.IsSuccess) return projection.Error;

            int? layerIndex = null;
            if (!string.IsNullOrEmpty(layerStr))
            {
                if (int.TryParse(layerStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                {
                    if (idx < 0 || idx > 31)
                        return new ErrorResponse("INVALID_LAYER_INDEX", $"Invalid 'layer' index: {idx}. Must be 0..31.");
                    layerIndex = idx;
                }
                else
                {
                    var resolved = LayerMask.NameToLayer(layerStr);
                    if (resolved < 0)
                        return new ErrorResponse("UNKNOWN_LAYER_NAME", $"Unknown layer name: '{layerStr}'. Use the Tags & Layers inspector to add it, or pass an integer index.");
                    layerIndex = resolved;
                }
            }

            Type componentType = null;
            if (!string.IsNullOrEmpty(componentName))
            {
                componentType = ComponentTypeResolver.Resolve(componentName);
                if (componentType == null)
                {
                    var similar = ComponentTypeResolver.SuggestSimilar(componentName);
                    return new ErrorResponse(
                        "UNKNOWN_COMPONENT_TYPE",
                        $"Component type not found: '{componentName}'. Use the short name (e.g. 'Rigidbody') or a fully-qualified name (e.g. 'UnityEngine.Rigidbody').",
                        data: similar.Count > 0 ? new { did_you_mean = similar } : null);
                }
            }

            Regex pathRegex = null;
            if (!string.IsNullOrEmpty(pathGlob))
            {
                try { pathRegex = GlobToRegex(pathGlob); }
                catch (ArgumentException ex)
                {
                    return new ErrorResponse("INVALID_PATH_GLOB", $"Invalid path_glob '{pathGlob}': {ex.Message}");
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
                results.Add(Project(matched[i], projection));
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

        private sealed class Projection
        {
            public bool IsSuccess;
            public ErrorResponse Error;
            public bool IdsOnly;
            public bool NamesOnly;
            public List<string> Fields;
        }

        private static Projection ResolveProjection(ToolParams p)
        {
            bool idsOnly = p.GetBool("ids", false);
            bool namesOnly = p.GetBool("names", false);
            string fieldsText = p.Get("fields");
            int explicitModes = (idsOnly ? 1 : 0) + (namesOnly ? 1 : 0) + (!string.IsNullOrEmpty(fieldsText) ? 1 : 0);
            if (explicitModes > 1)
            {
                return new Projection
                {
                    IsSuccess = false,
                    Error = new ErrorResponse("INVALID_PROJECTION", "Use only one of --ids, --names, or --fields.")
                };
            }

            if (idsOnly)
                return new Projection { IsSuccess = true, IdsOnly = true };
            if (namesOnly)
                return new Projection { IsSuccess = true, NamesOnly = true };

            var fields = new List<string>();
            if (string.IsNullOrEmpty(fieldsText))
            {
                fields.Add("instance_id");
                fields.Add("name");
            }
            else
            {
                foreach (var raw in fieldsText.Split(','))
                {
                    var field = NormalizeField(raw);
                    if (field == "all")
                    {
                        fields.Clear();
                        fields.Add("instance_id");
                        fields.Add("name");
                        fields.Add("path");
                        fields.Add("scene");
                        fields.Add("active");
                        break;
                    }
                    if (field == null)
                    {
                        return new Projection
                        {
                            IsSuccess = false,
                            Error = new ErrorResponse(
                                "INVALID_FIELDS",
                                $"Unknown field in --fields: '{raw.Trim()}'. Allowed: instance_id, name, path, scene, active, all.")
                        };
                    }
                    if (!fields.Contains(field))
                        fields.Add(field);
                }
            }

            return new Projection { IsSuccess = true, Fields = fields };
        }

        private static string NormalizeField(string raw)
        {
            var field = (raw ?? "").Trim().ToLowerInvariant();
            if (field == "id") field = "instance_id";
            switch (field)
            {
                case "all":
                case "instance_id":
                case "name":
                case "path":
                case "scene":
                case "active":
                    return field;
                default:
                    return null;
            }
        }

        private static object Project(GameObject go, Projection projection)
        {
            if (projection.IdsOnly)
                return EntityIdCompat.IdOf(go);
            if (projection.NamesOnly)
                return go.name;

            var result = new Dictionary<string, object>();
            foreach (var field in projection.Fields)
            {
                switch (field)
                {
                    case "instance_id":
                        result["instance_id"] = EntityIdCompat.IdOf(go);
                        break;
                    case "name":
                        result["name"] = go.name;
                        break;
                    case "path":
                        result["path"] = HierarchyPath.Build(go.transform);
                        break;
                    case "scene":
                        result["scene"] = go.scene.name;
                        break;
                    case "active":
                        result["active"] = go.activeInHierarchy;
                        break;
                }
            }
            return result;
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
