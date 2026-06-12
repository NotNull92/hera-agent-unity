using UnityEditor;
using UnityEngine;

namespace HeraAgent
{
    /// <summary>
    /// Shared target resolution helpers used by tools that need to locate a
    /// GameObject (or component on a GameObject) from ToolParams or a raw
    /// string.
    /// </summary>
    public static class TargetResolver
    {
        /// <summary>
        /// Resolve a GameObject from <paramref name="p"/> using
        /// <c>instance_id</c> (highest priority) or <c>path</c>.
        /// </summary>
        /// <param name="p">Tool parameters.</param>
        /// <param name="altPathKey">
        /// An additional parameter key to treat as a path fallback
        /// (e.g. <c>"target"</c>).
        /// </param>
        public static (GameObject go, string err) ResolveGameObject(ToolParams p, string altPathKey = null)
        {
            var idToken = p.GetRaw("instance_id");
            if (idToken != null && idToken.Type != Newtonsoft.Json.Linq.JTokenType.Null)
            {
                int? id = p.GetInt("instance_id");
                if (id == null) return (null, $"Invalid 'instance_id': '{idToken}'.");
                var obj = EntityIdCompat.ToObject(id.Value);
                if (obj == null) return (null, $"No object for instance_id={id.Value}.");
                GameObject go = obj as GameObject;
                if (go == null && obj is Component c) go = c.gameObject;
                if (go == null) return (null, $"instance_id={id.Value} is not a GameObject (type={obj.GetType().Name}).");
                return (go, null);
            }

            string path = p.Get("path");
            if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(altPathKey))
                path = p.Get(altPathKey);

            if (!string.IsNullOrEmpty(path))
            {
                var go = HierarchyPath.Find(path);
                if (go == null) return (null, $"No GameObject at path: '{path}'.");
                return (go, null);
            }

            return (null, "Target required: pass 'instance_id' or 'path'.");
        }

        /// <summary>
        /// Resolve a GameObject from <paramref name="p"/> and then fetch the
        /// specified component on it.
        /// </summary>
        public static (T comp, string err) ResolveComponent<T>(ToolParams p) where T : Component
        {
            var (go, err) = ResolveGameObject(p);
            if (go == null) return (null, err);
            var comp = go.GetComponent<T>();
            if (comp == null)
            {
                string typeName = typeof(T).Name;
                if (typeName == "RectTransform")
                    return (null, $"'{go.name}' has no RectTransform (not a UI element).");
                return (null, $"'{go.name}' has no {typeName}.");
            }
            return (comp, null);
        }

        /// <summary>
        /// Resolve a Transform from a raw string that is either an
        /// <c>instance_id</c> integer or a hierarchy <c>path</c>.
        /// </summary>
        public static (Transform t, string err) ResolveTransform(string s)
        {
            if (string.IsNullOrEmpty(s)) return (null, null);
            if (int.TryParse(s, out var id))
            {
                var obj = EntityIdCompat.ToObject(id);
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go == null) return (null, $"No GameObject for instance_id={id}.");
                return (go.transform, null);
            }
            var found = HierarchyPath.Find(s);
            if (found == null) return (null, $"No GameObject at path: '{s}'.");
            return (found.transform, null);
        }
    }
}
