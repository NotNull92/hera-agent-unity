using System.Collections.Generic;
using UnityEngine;

namespace HeraAgent
{
    /// <summary>
    /// Computes the canonical "/Root/Child/Grandchild" hierarchy path for a
    /// Transform. Shared by tools that return GameObject shallow shapes
    /// (manage_gameobject, find_gameobjects) so the path field is consistent
    /// across responses.
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
    }
}
