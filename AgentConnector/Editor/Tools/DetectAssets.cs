using Newtonsoft.Json.Linq;

namespace HeraAgent.Tools
{
    /// <summary>
    /// Detects installed third-party asset plugins in the Unity project.
    /// Updates asset-config.json with installed status.
    /// </summary>
    [HeraTool(Description = "Detect installed third-party asset plugins and update asset config", Group = "config")]
    public static class DetectAssets
    {
        public class Parameters
        {
            [ToolParameter(Name = "project_path", Description = "Unity project root path (auto-detected if omitted)")]
            public string ProjectPath { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            var projectPath = parameters?["project_path"]?.ToString();
            var result = AssetDetector.Detect(projectPath);

            var results = new JObject
            {
                ["project_path"] = result.projectPath,
                ["detected"] = result.detected,
                ["config_path"] = result.configPath
            };

            return new SuccessResponse("Asset detection complete", results);
        }
    }
}
