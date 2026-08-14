using System;
using System.IO;
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
        private static readonly (string id, string[] folders, string[] files, string[] assemblies)[] DetectionRules = new[]
        {
            ("odin_inspector",
                new[] {
                    "Assets/Plugins/Sirenix/Odin Inspector",
                    "Assets/ThirdParty/Sirenix/Odin Inspector",
                    "Assets/Sirenix/Odin Inspector",
                    "Packages/com.sirenix.odin-inspector"
                },
                new[] {
                    "Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll",
                    "Assets/Plugins/Sirenix/Assemblies/NoEditor/Sirenix.OdinInspector.Attributes.dll",
                    "Assets/Plugins/Sirenix/Assemblies/NoEmitAndNoEditor/Sirenix.OdinInspector.Attributes.dll"
                },
                new[] { "Sirenix.OdinInspector.Attributes", "Sirenix.OdinInspector.Editor" }),
            ("odin_validator",
                new[] {
                    "Assets/Plugins/Sirenix/Odin Validator",
                    "Assets/Plugins/Sirenix/Odin/Modules/Sirenix.OdinValidator",
                    "Assets/ThirdParty/Sirenix/Odin/Modules/Sirenix.OdinValidator",
                    "Packages/com.sirenix.odin-validator"
                },
                new[] {
                    "Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinValidator.dll",
                    "Assets/Plugins/Sirenix/Assemblies/NoEditor/Sirenix.OdinValidator.dll"
                },
                new[] { "Sirenix.OdinValidator" }),
            ("odin_serializer",
                new[] {
                    "Assets/Plugins/Sirenix/Odin Serializer",
                    "Assets/Plugins/Sirenix/Odin/Modules/Sirenix.OdinSerializer",
                    "Assets/ThirdParty/Sirenix/Odin/Modules/Sirenix.OdinSerializer",
                    "Packages/com.sirenix.odin-serializer"
                },
                new[] {
                    "Assets/Plugins/Sirenix/Assemblies/Sirenix.Serialization.dll",
                    "Assets/Plugins/Sirenix/Assemblies/NoEditor/Sirenix.Serialization.dll",
                    "Assets/Plugins/Sirenix/Assemblies/NoEmitAndNoEditor/Sirenix.Serialization.dll"
                },
                new[] { "Sirenix.Serialization" }),
            ("dotween",
                new[] {
                    "Assets/Demigiant/DOTween",
                    "Assets/Plugins/Demigiant/DOTween",
                    "Assets/Plugins/DOTween",
                    "Assets/ThirdParty/DOTween",
                    "Packages/com.demigiant.dotween"
                },
                new[] {
                    "Assets/Plugins/Demigiant/DOTween/DOTween.dll",
                    "Assets/Demigiant/DOTween/DOTween.dll"
                },
                new[] { "DOTween" }),
            ("dotween_pro",
                new[] {
                    "Assets/Demigiant/DOTweenPro",
                    "Assets/Plugins/Demigiant/DOTweenPro",
                    "Assets/Plugins/DOTweenPro",
                    "Assets/ThirdParty/DOTweenPro",
                    "Packages/com.demigiant.dotween-pro"
                },
                new[] {
                    "Assets/Plugins/Demigiant/DOTweenPro/DOTweenPro.dll",
                    "Assets/Demigiant/DOTweenPro/DOTweenPro.dll"
                },
                new[] { "DOTweenPro" }),
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
            projectPath = NormalizeProjectPath(projectPath);

            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".hera-agent-unity", "asset-config.json");

            var detectedAssets = Scan(projectPath);

            AssetConfigFile.Update(configPath, config =>
            {
                if (config == null)
                    throw new InvalidDataException("asset-config.json does not exist. Run asset-config before detect_assets.");
                foreach (var detection in detectedAssets)
                    UpdateConfig(config, detection.Value<string>("id"), detection.Value<bool>("installed"));
                return config;
            });

            return new Result
            {
                projectPath = projectPath,
                configPath = configPath,
                detected = detectedAssets
            };
        }

        internal static JArray Scan(string projectPath)
        {
            return Scan(projectPath, Application.dataPath, CheckAssemblyReferences);
        }

        internal static JArray Scan(
            string projectPath,
            string activeProjectPath,
            Func<string[], bool> checkAssemblies)
        {
            var projectRoot = NormalizeProjectPath(projectPath);
            var activeProjectRoot = NormalizeProjectPath(activeProjectPath);
            var canUseLoadedAssemblies = string.Equals(
                projectRoot,
                activeProjectRoot,
                StringComparison.OrdinalIgnoreCase);
            var detectedAssets = new JArray();

            foreach (var (id, folders, files, assemblies) in DetectionRules)
            {
                var found = false;
                string foundPath = null;

                foreach (var folder in folders)
                {
                    if (!Directory.Exists(Path.Combine(projectRoot, folder))) continue;
                    found = true;
                    foundPath = folder;
                    break;
                }

                if (!found)
                {
                    foreach (var file in files)
                    {
                        if (!File.Exists(Path.Combine(projectRoot, file))) continue;
                        found = true;
                        foundPath = file;
                        break;
                    }
                }

                if (!found && canUseLoadedAssemblies && checkAssemblies != null)
                    found = checkAssemblies(assemblies);

                detectedAssets.Add(new JObject
                {
                    ["id"] = id,
                    ["installed"] = found,
                    ["path"] = foundPath ?? (string)null,
                });
            }

            return detectedAssets;
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

        private static string NormalizeProjectPath(string projectPath)
        {
            var path = string.IsNullOrWhiteSpace(projectPath)
                ? Application.dataPath
                : projectPath;
            path = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(Path.GetFileName(path), "Assets", StringComparison.OrdinalIgnoreCase))
                path = Directory.GetParent(path)?.FullName ?? path;
            return path;
        }

        private static bool CheckAssemblyReferences(string[] prefixes)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (string.IsNullOrEmpty(name)) continue;
                foreach (var prefix in prefixes)
                {
                    if (string.Equals(name, prefix, StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }
    }
}
