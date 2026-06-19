using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HeraAgent
{
    /// <summary>
    /// Small helpers for reading the component list of a GameObject in a stable,
    /// serialization-friendly form. Used by manage_ui and manage_prefab.
    /// </summary>
    internal static class GameObjectComponents
    {
        /// <summary>
        /// Returns the type names of all non-null components on <paramref name="go"/>,
        /// in GetComponents order. Missing scripts (null entries) are skipped.
        /// </summary>
        public static string[] GetNames(GameObject go)
        {
            if (go == null) return new string[0];
            return go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => c.GetType().Name)
                .ToArray();
        }
    }
}
