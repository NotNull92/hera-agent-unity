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
        public static (GameObject go, ErrorResponse err) ResolveGameObject(ToolParams p, string altPathKey = null)
        {
            var idToken = p.GetRaw("instance_id");
            if (idToken != null && idToken.Type != Newtonsoft.Json.Linq.JTokenType.Null)
            {
                int? id = p.GetInt("instance_id");
                if (id == null)
                    return (null, new ErrorResponse("INVALID_INSTANCE_ID", $"Invalid 'instance_id': '{idToken}'."));
                var obj = EntityIdCompat.ToObject(id.Value);
                if (obj == null)
                    return (null, new ErrorResponse("OBJECT_NOT_FOUND", $"No object for instance_id={id.Value}."));
                GameObject go = obj as GameObject;
                if (go == null && obj is Component c) go = c.gameObject;
                if (go == null)
                    return (null, new ErrorResponse("NOT_A_GAMEOBJECT", $"instance_id={id.Value} is not a GameObject (type={obj.GetType().Name})."));
                return (go, null);
            }

            string path = p.Get("path");
            if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(altPathKey))
                path = p.Get(altPathKey);

            if (!string.IsNullOrEmpty(path))
            {
                if (ObjectIdentity.IsDurableForm(path))
                {
                    var (durableGo, durableErr) = ResolveDurableGameObject(path);
                    return durableGo != null ? (durableGo, null) : (null, durableErr);
                }
                var go = HierarchyPath.Find(path);
                if (go == null)
                    return (null, new ErrorResponse("TARGET_NOT_FOUND", $"No GameObject at path: '{path}'."));
                return (go, null);
            }

            return (null, new ErrorResponse("MISSING_TARGET", "Target required: pass 'instance_id' or 'path'."));
        }

        static (GameObject go, ErrorResponse err) ResolveDurableGameObject(string s)
        {
            if (s.StartsWith("guid:", System.StringComparison.Ordinal))
                return (null, new ErrorResponse("NOT_A_GAMEOBJECT", "A guid: handle names an asset; this tool targets scene GameObjects. Use a hierarchy path, instance_id, or GlobalObjectId."));
            if (!ObjectIdentity.TryResolve(s, out var obj, out var err))
                return (null, new ErrorResponse("TARGET_NOT_FOUND", err));
            var go = obj as GameObject ?? (obj as Component)?.gameObject;
            if (go == null)
                return (null, new ErrorResponse("NOT_A_GAMEOBJECT", $"'{s}' resolved to {obj.GetType().Name}, not a GameObject."));
            return (go, null);
        }

        /// <summary>
        /// Resolve a GameObject from <paramref name="p"/> and then fetch the
        /// specified component on it.
        /// </summary>
        public static (T comp, ErrorResponse err) ResolveComponent<T>(ToolParams p) where T : Component
        {
            var (go, err) = ResolveGameObject(p);
            if (go == null) return (null, err);
            var comp = go.GetComponent<T>();
            if (comp == null)
            {
                string typeName = typeof(T).Name;
                if (typeName == "RectTransform")
                    return (null, new ErrorResponse("COMPONENT_NOT_FOUND", $"'{go.name}' has no RectTransform (not a UI element)."));
                return (null, new ErrorResponse("COMPONENT_NOT_FOUND", $"'{go.name}' has no {typeName}."));
            }
            return (comp, null);
        }

        /// <summary>
        /// Resolve a Transform from a raw string that is either an
        /// <c>instance_id</c> integer or a hierarchy <c>path</c>.
        /// </summary>
        public static (Transform t, ErrorResponse err) ResolveTransform(string s)
        {
            if (string.IsNullOrEmpty(s)) return (null, null);
            if (int.TryParse(s, out var id))
            {
                var obj = EntityIdCompat.ToObject(id);
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go != null) return (go.transform, null);
                // A stale id is the common case here (ids die on domain
                // reload), but a GameObject literally named "104194" is still
                // reachable — fall through to a hierarchy lookup and report
                // both strategies when neither lands.
                var byName = HierarchyPath.Find(s);
                if (byName != null) return (byName.transform, null);
                return (null, new ErrorResponse(
                    "OBJECT_NOT_FOUND",
                    $"No GameObject for instance_id={id} (and no GameObject at path '{s}').",
                    new
                    {
                        tried = new object[]
                        {
                            new { form = "instance_id", error = $"no object for instance_id={id}" },
                            new { form = "hierarchy_path", error = $"no GameObject at path '{s}'" },
                        },
                    }));
            }
            if (ObjectIdentity.IsDurableForm(s))
            {
                var (durableGo, durableErr) = ResolveDurableGameObject(s);
                return durableGo != null ? (durableGo.transform, null) : (null, durableErr);
            }
            var found = HierarchyPath.Find(s);
            if (found == null)
                return (null, new ErrorResponse("TARGET_NOT_FOUND", $"No GameObject at path: '{s}'."));
            return (found.transform, null);
        }
    }
}
