using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent
{
    /// <summary>
    /// Low-level asset reserialization helper used by the reserialize tool.
    /// Centralizes ForceReserializeAssets calls and path normalization.
    /// </summary>
    public static class AssetReserializer
    {
        public class Result
        {
            public bool wholeProject;
            public string[] paths;
        }

        /// <summary>
        /// Reserializes the whole project if <paramref name="paths"/> is null or
        /// empty; otherwise reserializes only the specified asset paths.
        /// </summary>
        public static Result Reserialize(string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                AssetDatabase.ForceReserializeAssets();
                Debug.Log("[Hera] ForceReserializeAssets: entire project");
                return new Result { wholeProject = true };
            }

            AssetDatabase.ForceReserializeAssets(paths);
            Debug.Log($"[Hera] ForceReserializeAssets: {string.Join(\", \", paths)}");
            return new Result { wholeProject = false, paths = paths };
        }
    }
}
