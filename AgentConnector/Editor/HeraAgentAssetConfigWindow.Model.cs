using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using HeraAgent.Tools;

namespace HeraAgent.Editor
{
    public partial class HeraAgentAssetConfigWindow : EditorWindow
    {
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
            public string loopEngineeringMode;
            public string ui_system = "ugui";

            // Game Feel UI Mode (Beta) — when on, manage_ui attaches Game Feel & Juice
            // Bible + UI Feedback Design Guide juice guidance (DOTween-aware) to its
            // create responses. Read at dispatch time by HeraSettings; surfaced to the
            // CLI via asset-config.json. (Persisted under `ui_juicy_mode` before the
            // rename; LoadConfig migrates that key transparently.)
            public bool game_feel_ui_mode;

            // Game Feel Mode (Beta) — gameplay-wide counterpart. When on, agent
            // rules and tool responses point agents at the bundled game_feel
            // knowledge base (Game Feel & Juice Bible + Ethical Engagement
            // Framework). Read at dispatch time by HeraSettings.
            public bool game_feel_mode;

            // Unity De-slop Mode (Beta) — static visual slop cleanup (layout,
            // spacing, typography, color). When on, `doctor --agent-rules`
            // injects the unity-deslop discipline and tool responses point agents
            // at the bundled ui_slop taxonomy. Read at dispatch time by HeraSettings.
            public bool ui_slop_mode;
        }

        private const string LoopEngineeringOff = "off";
        private const string LoopEngineeringLight = "light";
        private const string LoopEngineeringUltra = "ultra";
        private const string UiSystemUGUI = "ugui";
        private const string UiSystemUITK = "uitk";

        private static readonly string[] ConfigFieldNames =
        {
            "version", "defaultCscPath", "defaultDotnetPath", "loopEngineeringMode",
            "ui_system", "game_feel_ui_mode", "game_feel_mode", "ui_slop_mode"
        };

        private static readonly string[] AssetFieldNames =
        {
            "id", "name", "enabled", "installed", "category", "description", "doc_url", "reference_path"
        };

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

                // Migrate the pre-rename `ui_juicy_mode` key onto game_feel_ui_mode.
                // The old key isn't a field anymore, so it's dropped on the next Save.
                if (_config != null)
                {
                    var raw = JObject.Parse(json);
                    if (raw["game_feel_ui_mode"] == null && raw["ui_juicy_mode"] != null)
                        _config.game_feel_ui_mode = raw.Value<bool>("ui_juicy_mode");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Hera] I couldn't load asset-config.json: {ex.Message}");
                _config = new AssetConfig();
            }
        }

        private void SaveConfig()
        {
            if (_config == null) return;

            try
            {
                var desired = JObject.FromObject(_config);
                AssetConfigFile.Update(_configPath, current => MergeConfig(current, desired));
                _isDirty = false;
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Hera] Failed to save asset-config.json: {ex.Message}");
                EditorUtility.DisplayDialog("Save Failed", $"Could not write config:\n{ex.Message}", "OK");
            }
        }

        private static JObject MergeConfig(JObject current, JObject desired)
        {
            var merged = current == null ? new JObject() : (JObject)current.DeepClone();
            foreach (var field in ConfigFieldNames)
            {
                var value = desired[field];
                if (value == null)
                    merged.Remove(field);
                else
                    merged[field] = value.DeepClone();
            }
            merged["assets"] = MergeAssets(merged["assets"] as JArray, desired["assets"] as JArray);
            return merged;
        }

        private static JArray MergeAssets(JArray current, JArray desired)
        {
            var desiredById = new Dictionary<string, JObject>();
            if (desired != null)
            {
                foreach (var token in desired)
                {
                    if (!(token is JObject asset)) continue;
                    var id = asset.Value<string>("id");
                    if (!string.IsNullOrEmpty(id)) desiredById[id] = asset;
                }
            }

            var merged = new JArray();
            var consumed = new HashSet<string>();
            if (current != null)
            {
                foreach (var token in current)
                {
                    if (!(token is JObject persisted))
                    {
                        merged.Add(token.DeepClone());
                        continue;
                    }

                    var id = persisted.Value<string>("id");
                    if (string.IsNullOrEmpty(id) || !desiredById.TryGetValue(id, out var replacement))
                    {
                        merged.Add(persisted.DeepClone());
                        continue;
                    }

                    var updated = (JObject)persisted.DeepClone();
                    foreach (var field in AssetFieldNames)
                    {
                        var value = replacement[field];
                        if (value == null)
                            updated.Remove(field);
                        else
                            updated[field] = value.DeepClone();
                    }
                    merged.Add(updated);
                    consumed.Add(id);
                }
            }

            foreach (var pair in desiredById)
            {
                if (!consumed.Contains(pair.Key)) merged.Add(pair.Value.DeepClone());
            }
            return merged;
        }

        private void EnsureDefaults()
        {
            if (_config == null)
                _config = new AssetConfig();

            bool changed = false;
            var normalizedMode = NormalizeLoopEngineeringMode(_config.loopEngineeringMode);
            if (_config.loopEngineeringMode != normalizedMode)
            {
                _config.loopEngineeringMode = normalizedMode;
                changed = true;
            }

            var normalizedUiSystem = NormalizeUiSystem(_config.ui_system);
            if (_config.ui_system != normalizedUiSystem)
            {
                _config.ui_system = normalizedUiSystem;
                changed = true;
            }

            var defaults = GetDefaultAssets();
            var existingIds = new HashSet<string>(_config.assets.Select(a => a.id));

            foreach (var def in defaults)
            {
                if (!existingIds.Contains(def.id))
                {
                    _config.assets.Add(def);
                    changed = true;
                }
            }

            if (changed)
                SaveConfig();
        }

        private static string NormalizeLoopEngineeringMode(string mode)
        {
            switch ((mode ?? string.Empty).Trim().ToLowerInvariant())
            {
                case LoopEngineeringOff:
                    return LoopEngineeringOff;
                case LoopEngineeringUltra:
                    return LoopEngineeringUltra;
                case LoopEngineeringLight:
                default:
                    return LoopEngineeringLight;
            }
        }

        private static string NormalizeUiSystem(string system)
        {
            return string.Equals(system?.Trim(), UiSystemUITK, StringComparison.OrdinalIgnoreCase)
                ? UiSystemUITK
                : UiSystemUGUI;
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
            if (!string.IsNullOrEmpty(s_CachedCscPath) && IsValidCscPath(s_CachedCscPath))
                return s_CachedCscPath;

            var unityCsc = FindUnityBuiltInCsc();
            if (!string.IsNullOrEmpty(unityCsc))
            {
                s_CachedCscPath = unityCsc;
                return unityCsc;
            }

            var candidates = new List<(string path, Version version)>();

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

            // Unity's own .NET SDK Roslyn — the correct compiler. Its path moved
            // between versions (6.0–6.4: DotNetSdkRoslyn/csc.dll; 6.5+: DotNetSdk/
            // sdk/<version>/Roslyn/bincore/csc.dll) and macOS nests under
            // Resources/Scripting, so fast-path the stable layouts then fall back
            // to a recursive csc.dll search.
            var csc = ExecCompileCache.FindBundledCsc(appContents);
            if (!string.IsNullOrEmpty(csc) && IsValidCscPath(csc)) return csc;

            // Mono csc.exe is a last resort only — it fails to load
            // System.Text.Encoding.CodePages on a non-Latin Windows console, so we
            // reach for it only when no .NET SDK Roslyn csc.dll ships.
            var monoCsc = Path.Combine(appContents, "MonoBleedingEdge", "lib", "mono", "4.5", "csc.exe");
            if (File.Exists(monoCsc)) return monoCsc;
            try
            {
                foreach (var file in Directory.GetFiles(appContents, "csc.exe", SearchOption.AllDirectories))
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
            if (!string.IsNullOrEmpty(s_CachedDotnetPath) && File.Exists(s_CachedDotnetPath))
                return s_CachedDotnetPath;

            var name = "dotnet" + (Application.platform == RuntimePlatform.WindowsEditor ? ".exe" : "");
            var bundled = ExecCompileCache.FindBundledDotnet(EditorApplication.applicationContentsPath, name);
            if (!string.IsNullOrEmpty(bundled))
            {
                s_CachedDotnetPath = bundled;
                return bundled;
            }

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
