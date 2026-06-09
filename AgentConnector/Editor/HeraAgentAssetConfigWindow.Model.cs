using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace HeraAgent.Editor
{
    public partial class HeraAgentAssetConfigWindow : EditorWindow
        [System.Serializable]
        private class AssetEntry
        {
            public string id;
            public string name;
            public bool enabled;
            public bool installed;
            public string category;
            public string description;
            public string doc_url;
            public string reference_path;
        }

        [System.Serializable]
        private class AssetConfig
        {
            public string version = "1.0.0";
            public List<AssetEntry> assets = new List<AssetEntry>();
            public string defaultCscPath;
            public string defaultDotnetPath;
        }

        private static string GetConfigPath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".hera-agent-unity", "asset-config.json");
        }

        private void LoadConfig()
        {
            _isDirty = false;

            if (!File.Exists(_configPath))
            {
                _config = new AssetConfig();
                return;
            }

            try
            {
                var json = File.ReadAllText(_configPath);
                _config = JsonConvert.DeserializeObject<AssetConfig>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Hera] Failed to load asset-config.json: {ex.Message}");
                _config = new AssetConfig();
            }
        }

        private void SaveConfig()
        {
            if (_config == null) return;

            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
                _isDirty = false;
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Hera] Failed to save asset-config.json: {ex.Message}");
                EditorUtility.DisplayDialog("Save Failed", $"Could not write config:\n{ex.Message}", "OK");
            }
        }

        private void EnsureDefaults()
        {
            if (_config == null)
                _config = new AssetConfig();

            var defaults = GetDefaultAssets();
            var existingIds = new HashSet<string>(_config.assets.Select(a => a.id));
            bool added = false;

            foreach (var def in defaults)
            {
                if (!existingIds.Contains(def.id))
                {
                    _config.assets.Add(def);
                    added = true;
                }
            }

            if (added)
                SaveConfig();
        }

        private void InitializeCategoryStates()
        {
            foreach (var cat in CategoryOrder)
                _categoryExpanded[cat] = true;
        }

        private static List<AssetEntry> GetDefaultAssets()
        {
            return new List<AssetEntry>
            {
                new AssetEntry
                {
                    id = "odin_inspector",
                    name = "Odin Inspector",
                    enabled = false,
                    installed = false,
                    category = "inspector",
                    description = "Powerful inspector extension. Prioritize Odin API when generating custom editor code.",
                    doc_url = "https://odininspector.com/documentation",
                    reference_path = "references/odin-inspector.md"
                },
                new AssetEntry
                {
                    id = "odin_validator",
                    name = "Odin Validator",
                    enabled = false,
                    installed = false,
                    category = "validation",
                    description = "Data validation system. Use Odin Validator for runtime and editor data integrity checks.",
                    doc_url = "https://odininspector.com/tutorials/odin-validator/getting-started-with-odin-validator",
                    reference_path = "references/odin-validator.md"
                },
                new AssetEntry
                {
                    id = "odin_serializer",
                    name = "Odin Serializer",
                    enabled = false,
                    installed = false,
                    category = "serialization",
                    description = "High-performance serialization. Replace Unity's default serializer with Odin Serializer for complex data structures.",
                    doc_url = "https://odininspector.com/tutorials/serialize-anything/odin-serializer-quick-start",
                    reference_path = "references/odin-serializer.md"
                },
                new AssetEntry
                {
                    id = "dotween",
                    name = "DOTween",
                    enabled = false,
                    installed = false,
                    category = "animation",
                    description = "Tweening and animation engine. Default to DOTween API for all tweening and timing implementations.",
                    doc_url = "https://dotween.demigiant.com/documentation.php",
                    reference_path = "references/dotween.md"
                },
                new AssetEntry
                {
                    id = "dotween_pro",
                    name = "DOTween Pro",
                    enabled = false,
                    installed = false,
                    category = "animation",
                    description = "Advanced tweening features including Visual Animation, Physics2D integration, and Audio tweening.",
                    doc_url = "https://dotween.demigiant.com/pro.php",
                    reference_path = "references/dotween-pro.md"
                }
            };
        }

        private void DetectInstalledAssets()
        {
            ShowLoading("Surveying assets...");
            EditorApplication.delayCall += () => RunDetection();
        }

        private void RunDetection()
        {
            var projectPath = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            int detectedCount = 0;

            var rules = new (string id, string[] folders, string[] dlls, string[] assemblies)[]
            {
                ("odin_inspector",
                 new[] {
                     "Assets/Plugins/Sirenix",
                     "Assets/ThirdParty/Sirenix",
                     "Assets/Sirenix",
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

            foreach (var (id, folders, dlls, assemblies) in rules)
            {
                var asset = _config.assets.FirstOrDefault(a => a.id == id);
                if (asset == null) continue;

                bool found = false;

                if (!found)
                {
                    foreach (var folder in folders)
                    {
                        if (Directory.Exists(Path.Combine(projectPath, folder)))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    foreach (var dll in dlls)
                    {
                        if (File.Exists(Path.Combine(projectPath, dll)))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    found = CheckAssemblies(assemblies);
                }

                if (found && !asset.installed)
                    detectedCount++;

                asset.installed = found;
            }

            _isDirty = true;
            SaveConfig();
            HideLoading();
            RefreshUI();
        }

        private static bool CheckAssemblies(string[] prefixes)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.IsNullOrEmpty(asm.FullName)) continue;
                foreach (var prefix in prefixes)
                {
                    if (asm.FullName.StartsWith(prefix))
                        return true;
                }
            }
            return false;
        }

        private string FindValidCscPath()
        {
            // 1) Saved config
            if (!string.IsNullOrEmpty(_config?.defaultCscPath) && IsValidCscPath(_config.defaultCscPath))
                return _config.defaultCscPath;

            // 2) In-process cache
            if (!string.IsNullOrEmpty(s_CachedCscPath) && IsValidCscPath(s_CachedCscPath))
                return s_CachedCscPath;

            // 3) Collect all candidates with versions, then pick the best
            var candidates = new List<(string path, Version version)>();

            // Unity built-in (lowest priority among valid compilers)
            var unityCsc = FindUnityBuiltInCsc();
            if (!string.IsNullOrEmpty(unityCsc))
                candidates.Add((unityCsc, new Version(0, 0)));

            // Platform-specific SDKs
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    CollectCscCandidatesWindows(candidates);
                    break;
                case RuntimePlatform.OSXEditor:
                    CollectCscCandidatesMacOS(candidates);
                    break;
                default:
                    CollectCscCandidatesLinux(candidates);
                    break;
            }

            if (candidates.Count == 0) return null;

            var minVersion = GetMinimumRecommendedSdkVersion();

            // Prefer compatible versions (>= min), then pick latest
            var compatible = candidates
                .Where(c => c.version >= minVersion)
                .OrderByDescending(c => c.version)
                .FirstOrDefault();

            var best = compatible.path != null
                ? compatible
                : candidates.OrderByDescending(c => c.version).First();

            s_CachedCscPath = best.path;
            return best.path;
        }

        private static string FindUnityBuiltInCsc()
        {
            var appContents = EditorApplication.applicationContentsPath;
            if (string.IsNullOrEmpty(appContents)) return null;

            // Unity's own .NET SDK Roslyn (newer Unity versions)
            var dotNetSdkRoslyn = Path.Combine(appContents, "DotNetSdkRoslyn", "csc.dll");
            if (IsValidCscPath(dotNetSdkRoslyn)) return dotNetSdkRoslyn;

            // MonoBleedingEdge fallback (older Unity versions)
            var name = Application.platform == RuntimePlatform.WindowsEditor ? "csc.exe" : "csc.exe";
            var monoCsc = Path.Combine(appContents, "MonoBleedingEdge", "lib", "mono", "4.5", name);
            if (File.Exists(monoCsc)) return monoCsc;

            // Deep search as last resort (limited depth)
            try
            {
                foreach (var file in Directory.GetFiles(appContents, name, SearchOption.AllDirectories))
                    return file;
            }
            catch { }

            return null;
        }

        private static void CollectCscCandidatesWindows(List<(string path, Version version)> candidates)
        {
            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // .NET SDK locations
            var sdkRoots = new List<string>();
            if (!string.IsNullOrEmpty(programFilesX86))
                sdkRoots.Add(Path.Combine(programFilesX86, "dotnet", "sdk"));
            if (!string.IsNullOrEmpty(programFiles))
                sdkRoots.Add(Path.Combine(programFiles, "dotnet", "sdk"));
            if (!string.IsNullOrEmpty(userProfile))
                sdkRoots.Add(Path.Combine(userProfile, ".dotnet", "sdk"));

            foreach (var sdkDir in sdkRoots)
            {
                if (!Directory.Exists(sdkDir)) continue;
                foreach (var dir in Directory.GetDirectories(sdkDir))
                {
                    var version = ExtractSdkVersionFromPath(dir);

                    var path = Path.Combine(dir, "Roslyn", "bincore", "csc.exe");
                    if (File.Exists(path)) candidates.Add((path, version));

                    path = Path.Combine(dir, "Roslyn", "csc.exe");
                    if (File.Exists(path)) candidates.Add((path, version));
                }
            }

            // Visual Studio 2022 (ships Roslyn 4.x ≈ .NET 7+)
            if (!string.IsNullOrEmpty(programFiles))
            {
                var vsBase = Path.Combine(programFiles, "Microsoft Visual Studio", "2022");
                if (Directory.Exists(vsBase))
                {
                    foreach (var edition in Directory.GetDirectories(vsBase))
                    {
                        var path = Path.Combine(edition, "MSBuild", "Current", "Bin", "Roslyn", "csc.exe");
                        if (File.Exists(path)) candidates.Add((path, new Version(7, 0)));
                    }
                }
            }

            // JetBrains Rider
            if (!string.IsNullOrEmpty(programFiles))
            {
                var riderBase = Path.Combine(programFiles, "JetBrains");
                if (Directory.Exists(riderBase))
                {
                    foreach (var riderDir in Directory.GetDirectories(riderBase))
                    {
                        if (!riderDir.Contains("Rider")) continue;
                        var path = Path.Combine(riderDir, "tools", "MSBuild", "Current", "Bin", "Roslyn", "csc.exe");
                        if (File.Exists(path)) candidates.Add((path, new Version(0, 0)));
                    }
                }
            }
        }

        private static void CollectCscCandidatesMacOS(List<(string path, Version version)> candidates)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var sdkRoots = new[] {
                "/usr/local/share/dotnet/sdk",
                "/opt/homebrew/share/dotnet/sdk",
                Path.Combine(home, ".dotnet", "sdk")
            };

            foreach (var sdkDir in sdkRoots)
            {
                if (!Directory.Exists(sdkDir)) continue;
                foreach (var dir in Directory.GetDirectories(sdkDir))
                {
                    var version = ExtractSdkVersionFromPath(dir);

                    var path = Path.Combine(dir, "Roslyn", "bincore", "csc.dll");
                    if (IsValidCscPath(path)) candidates.Add((path, version));

                    path = Path.Combine(dir, "Roslyn", "csc.dll");
                    if (IsValidCscPath(path)) candidates.Add((path, version));
                }
            }
        }

        private static void CollectCscCandidatesLinux(List<(string path, Version version)> candidates)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var sdkRoots = new[] {
                "/usr/share/dotnet/sdk",
                "/usr/local/share/dotnet/sdk",
                Path.Combine(home, ".dotnet", "sdk")
            };

            foreach (var sdkDir in sdkRoots)
            {
                if (!Directory.Exists(sdkDir)) continue;
                foreach (var dir in Directory.GetDirectories(sdkDir))
                {
                    var version = ExtractSdkVersionFromPath(dir);

                    var path = Path.Combine(dir, "Roslyn", "bincore", "csc.dll");
                    if (IsValidCscPath(path)) candidates.Add((path, version));

                    path = Path.Combine(dir, "Roslyn", "csc.dll");
                    if (IsValidCscPath(path)) candidates.Add((path, version));
                }
            }
        }

        private string FindValidDotnetPath()
        {
            // 1) Check saved config
            if (!string.IsNullOrEmpty(_config?.defaultDotnetPath) && File.Exists(_config.defaultDotnetPath))
                return _config.defaultDotnetPath;

            // 2) Check in-process cache
            if (!string.IsNullOrEmpty(s_CachedDotnetPath) && File.Exists(s_CachedDotnetPath))
                return s_CachedDotnetPath;

            // 3) Platform-specific standard paths
            string found;
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    found = FindDotnetOnWindows();
                    break;
                case RuntimePlatform.OSXEditor:
                    found = FindDotnetOnMacOS();
                    break;
                default:
                    found = FindDotnetOnLinux();
                    break;
            }
            if (!string.IsNullOrEmpty(found))
            {
                s_CachedDotnetPath = found;
                return found;
            }

            return null;
        }

        private static string FindDotnetOnWindows()
        {
            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(programFilesX86))
                candidates.Add(Path.Combine(programFilesX86, "dotnet", "dotnet.exe"));
            if (!string.IsNullOrEmpty(programFiles))
                candidates.Add(Path.Combine(programFiles, "dotnet", "dotnet.exe"));
            if (!string.IsNullOrEmpty(userProfile))
                candidates.Add(Path.Combine(userProfile, ".dotnet", "dotnet.exe"));

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }

            // Search PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var p in pathEnv.Split(';'))
                {
                    var full = Path.Combine(p.Trim(), "dotnet.exe");
                    if (File.Exists(full)) return full;
                }
            }

            return null;
        }

        private static string FindDotnetOnMacOS()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var candidates = new[] {
                "/usr/local/share/dotnet/dotnet",
                "/opt/homebrew/bin/dotnet",
                "/usr/local/bin/dotnet",
                "/usr/bin/dotnet",
                Path.Combine(home, ".dotnet", "dotnet")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }

            // Search PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var p in pathEnv.Split(':'))
                {
                    var full = Path.Combine(p.Trim(), "dotnet");
                    if (File.Exists(full)) return full;
                }
            }

            return null;
        }

        private static string FindDotnetOnLinux()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var candidates = new[] {
                "/usr/share/dotnet/dotnet",
                "/usr/local/share/dotnet/dotnet",
                "/usr/bin/dotnet",
                "/usr/local/bin/dotnet",
                Path.Combine(home, ".dotnet", "dotnet")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }

            // Search PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var p in pathEnv.Split(':'))
                {
                    var full = Path.Combine(p.Trim(), "dotnet");
                    if (File.Exists(full)) return full;
                }
            }

            return null;
        }

    }
}
