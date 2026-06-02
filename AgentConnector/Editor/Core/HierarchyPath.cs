using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeraAgent
{
    /// <summary>
    /// Hierarchy path helpers shared by every tool that addresses GameObjects by
    /// path. <see cref="Build"/> goes Transform → "/Root/Child"; <see cref="Find"/>
    /// goes the reverse, "/Root/Child" → GameObject.
    /// </summary>
    public static class HierarchyPath
    {
        public static string Build(Transform t)
        {
            if (t == null) return null;
            var stack = new Stack<string>();
            var cursor = t;
            while (cursor != null)
            {
                stack.Push(cursor.name);
                cursor = cursor.parent;
            }
            return "/" + string.Join("/", stack);
        }

        /// <summary>
        /// Resolves a "/Root/Child" path to a GameObject. GameObject.Find covers
        /// active objects across loaded scenes; the fallback walk also matches
        /// inactive roots/children that Find skips — important because callers
        /// operate on inactive subtrees too (reparent, set_active, UI editing).
        /// Shared by manage_gameobject, manage_components, manage_ui.
        /// </summary>
        public static GameObject Find(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var found = GameObject.Find(path);
            if (found != null) return found;

            string trimmed = path.TrimStart('/');
            if (string.IsNullOrEmpty(trimmed)) return null;
            var segments = trimmed.Split('/');

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name != segments[0]) continue;
                    var t = WalkPath(root.transform, segments, 1);
                    if (t != null) return t.gameObject;
                }
            }
            return null;
        }

        static Transform WalkPath(Transform t, string[] segments, int index)
        {
            if (index >= segments.Length) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name != segments[index]) continue;
                var match = WalkPath(c, segments, index + 1);
                if (match != null) return match;
            }
            return null;
        }
    }
}
