using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HeraAgent
{
    /// <summary>
    /// Detects installed third-party asset plugins in the Unity project and
    /// mirrors the result into ~/.hera-agent-unity/asset-config.json. Used by
    /// the detect_assets tool.
    /// </summary>
    public static class AssetDetector
    {
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

        public class Result
        {
            public string projectPath;
            public string configPath;
            public JArray detected;
        }

        /// <summary>
        /// Detects assets and updates the user asset-config file. Returns the
        /// detection summary as a JArray so the tool layer can wrap it in a
        /// response envelope.
        /// </summary>
        public static Result Detect(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                projectPath = UnityEngine.Application.dataPath;

            // Ensure projectPath points to the project root (parent of Assets)
            if (projectPath.EndsWith("Assets"))
                projectPath = Directory.GetParent(projectPath)?.FullName ?? projectPath;

            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".hera-agent-unity", "asset-config.json");

            var config = LoadConfig(configPath);
            var detectedAssets = new JArray();

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
                if (!found && id.StartsWith("odin"))
                    found = CheckAssemblyReference("Sirenix");
                // Check for DOTween assembly
                if (!found && id == "dotween")
                    found = CheckAssemblyReference("DOTween");
                if (!found && id == "dotween_pro")
                    found = CheckAssemblyReference("DOTweenPro");

                detectedAssets.Add(new JObject
                {
                    ["id"] = id,
                    ["installed"] = found,
                    ["path"] = foundPath ?? (string)null,
                });

                UpdateConfig(config, id, found);
            }

            SaveConfig(config, configPath);

            return new Result
            {
                projectPath = projectPath,
                configPath = configPath,
                detected = detectedAssets
            };
        }

        private static JObject LoadConfig(string configPath)
        {
            if (!File.Exists(configPath)) return null;
            try { return JObject.Parse(File.ReadAllText(configPath)); }
            catch { return null; }
        }

        private static void UpdateConfig(JObject config, string id, bool installed)
        {
            if (config == null) return;
            var assetsArray = config["assets"] as JArray;
            if (assetsArray == null) return;
            foreach (var asset in assetsArray)
            {
                if (asset["id"]?.ToString() == id)
                {
                    asset["installed"] = installed;
                    break;
                }
            }
        }

        private static void SaveConfig(JObject config, string configPath)
        {
            if (config == null) return;
            try
            {
                var configDir = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(configDir))
                    Directory.CreateDirectory(configDir);
                File.WriteAllText(configPath, config.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Hera] Failed to save asset config: {ex.Message}");
            }
        }

        private static bool CheckAssemblyReference(string assemblyName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName != null && assembly.FullName.StartsWith(assemblyName))
                    return true;
            }
            return false;
        }
    }
}
