using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

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

        // Detection patterns: asset ID → list of paths/assembly names to check
        private static readonly (string id, string[] patterns)[] DetectionRules = new[]
        {
            ("odin_inspector", new[]
            {
                "Assets/Plugins/Sirenix",
                "Assets/ThirdParty/Sirenix",
                "Assets/Sirenix",
            }),
            ("odin_validator", new[]
            {
                "Assets/Plugins/Sirenix/Odin/Modules/Sirenix.OdinValidator",
                "Assets/ThirdParty/Sirenix/Odin/Modules/Sirenix.OdinValidator",
            }),
            ("odin_serializer", new[]
            {
                "Assets/Plugins/Sirenix/Odin/Modules/Sirenix.OdinSerializer",
                "Assets/ThirdParty/Sirenix/Odin/Modules/Sirenix.OdinSerializer",
            }),
            ("dotween", new[]
            {
                "Assets/Demigiant/DOTween",
                "Assets/Plugins/DOTween",
                "Assets/ThirdParty/DOTween",
            }),
            ("dotween_pro", new[]
            {
                "Assets/Demigiant/DOTweenPro",
                "Assets/Plugins/DOTweenPro",
                "Assets/ThirdParty/DOTweenPro",
            }),
        };

        public static object HandleCommand(JObject parameters)
        {
            var projectPath = parameters?["project_path"]?.ToString();
            if (string.IsNullOrEmpty(projectPath))
            {
                projectPath = Application.dataPath;
            }

            // Ensure projectPath points to the project root (parent of Assets)
            if (projectPath.EndsWith("Assets"))
            {
                projectPath = Directory.GetParent(projectPath)?.FullName ?? projectPath;
            }

            var results = new JObject();
            results["project_path"] = projectPath;

            var detectedAssets = new JArray();
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".hera-agent-unity", "asset-config.json");

            // Load existing config
            JObject config = null;
            if (File.Exists(configPath))
            {
                try
                {
                    config = JObject.Parse(File.ReadAllText(configPath));
                }
                catch
                {
                    config = null;
                }
            }

            foreach (var (id, patterns) in DetectionRules)
            {
                bool found = false;
                string foundPath = null;

                foreach (var pattern in patterns)
                {
                    var fullPath = Path.Combine(projectPath, pattern);
                    if (Directory.Exists(fullPath))
                    {
                        found = true;
                        foundPath = pattern;
                        break;
                    }
                }

                // Also check assembly references for Odin
                if (!found && (id.StartsWith("odin")))
                {
                    found = CheckAssemblyReference("Sirenix");
                }
                // Check for DOTween assembly
                if (!found && id == "dotween")
                {
                    found = CheckAssemblyReference("DOTween");
                }
                if (!found && id == "dotween_pro")
                {
                    found = CheckAssemblyReference("DOTweenPro");
                }

                var assetInfo = new JObject
                {
                    ["id"] = id,
                    ["installed"] = found,
                    ["path"] = foundPath ?? (string)null,
                };

                detectedAssets.Add(assetInfo);

                // Update config file
                if (config != null)
                {
                    var assetsArray = config["assets"] as JArray;
                    if (assetsArray != null)
                    {
                        foreach (var asset in assetsArray)
                        {
                            if (asset["id"]?.ToString() == id)
                            {
                                asset["installed"] = found;
                                break;
                            }
                        }
                    }
                }
            }

            // Save updated config
            if (config != null)
            {
                try
                {
                    var configDir = Path.GetDirectoryName(configPath);
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }
                    File.WriteAllText(configPath, config.ToString(Newtonsoft.Json.Formatting.Indented));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Hera] Failed to save asset config: {ex.Message}");
                }
            }

            results["detected"] = detectedAssets;
            results["config_path"] = configPath;

            return new SuccessResponse("Asset detection complete", results);
        }

        private static bool CheckAssemblyReference(string assemblyName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName != null && assembly.FullName.StartsWith(assemblyName))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
