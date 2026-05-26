using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraTool(Name = "reserialize", Description = "Force reserialize Unity assets. No params = entire project.")]
    public static class ReserializeAssets
    {
        public class Parameters
        {
            [ToolParameter("Single asset path to reserialize")]
            public string Path { get; set; }

            [ToolParameter("Multiple asset paths to reserialize")]
            public string[] Paths { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            var p = new ToolParams(parameters);
            var argsToken = p.GetRaw("args") as JArray;
            var pathToken = p.GetRaw("path");
            var pathsToken = p.GetRaw("paths");

            string[] paths;
            if (argsToken != null && argsToken.Count > 0)
                paths = argsToken.ToObject<string[]>();
            else if (pathsToken != null && pathsToken.Type == JTokenType.Array)
                paths = pathsToken.ToObject<string[]>();
            else if (pathToken != null)
                paths = new[] { pathToken.ToString() };
            else
                paths = null;

            if (paths == null || paths.Length == 0)
            {
                AssetDatabase.ForceReserializeAssets();
                Debug.Log("[Hera] ForceReserializeAssets: entire project");
                return new SuccessResponse("Reserialized entire project");
            }

            AssetDatabase.ForceReserializeAssets(paths);
            Debug.Log($"[Hera] ForceReserializeAssets: {string.Join(", ", paths)}");
            return new SuccessResponse($"Reserialized {paths.Length} asset(s)", new { paths });
        }
    }
}
