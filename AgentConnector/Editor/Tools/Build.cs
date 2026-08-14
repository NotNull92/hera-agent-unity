using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace HeraAgent.Tools
{
    [HeraActionContract("start", typeof(Build.StartParameters), ResultType = typeof(Build.StartResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("status", typeof(object), ResultType = typeof(Build.StatusResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("get_settings", typeof(object), ResultType = typeof(Build.SettingsResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("set_settings", typeof(Build.SetSettingsParameters), ResultType = typeof(Build.SetSettingsResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("add_scene", typeof(Build.AddSceneParameters), ResultType = typeof(Build.SceneListResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("remove_scene", typeof(Build.ScenePathParameters), ResultType = typeof(Build.SceneListResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("list_targets", typeof(object), ResultType = typeof(Build.TargetsResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionSafety("start", RequiresConfirmation = true)]
    [HeraTool(
        Name = "build",
        Description = "Player builds for the active target. start queues the build and returns immediately — the Editor blocks while building, so poll `build status` (or use the CLI's `build start --wait`) until the compact report lands: result, output path, size, and the first error messages. get/set_settings, add_scene/remove_scene manage the Build Settings scene list and flags; list_targets shows installed build support.",
        Examples = new[]
        {
            "build get_settings",
            "build add_scene --path Assets/Scenes/Main.unity",
            "build start --wait",
            "build status",
        },
        ExampleDescriptions = new[]
        {
            "Read the active target, flags, and scene list",
            "Add a scene to the Build Settings list (idempotent)",
            "Queue the build and wait for the report (CLI polls the file bus)",
            "Read building state and the last report",
        },
        Profiles = new[] { "full" },
        RiskClass = HeraRiskClass.Write,
        ContractMode = ToolContractMode.Strict)]
    public static partial class Build
    {
        const int MaxErrorMessages = 20;
        const int MaxErrorLength = 300;

        static bool s_Queued;

        // The CLI's --wait path consumes the result file (the shared file-bus
        // poller deletes what it reads), so status keeps its own copy of the
        // last report for this domain. A domain reload clears it, after which
        // status honestly reports no retained report.
        static object s_LastReport;

        public sealed class StartParameters
        {
            [ToolParameter("Output path. Default: Builds/<target>/<product><ext> under the project root. Must resolve outside Assets/.")]
            public string OutputPath { get; set; }
        }

        public sealed class SetSettingsParameters
        {
            [ToolParameter("Development build.")]
            public bool? Development { get; set; }

            [ToolParameter("Allow script debugging (only meaningful with development=true).")]
            public bool? AllowDebugging { get; set; }

            [ToolParameter("Build scripts only.")]
            public bool? BuildScriptsOnly { get; set; }

            [ToolParameter("Validate and report what would change without touching anything.")]
            public bool? DryRun { get; set; }
        }

        public class ScenePathParameters
        {
            [ToolParameter("Scene asset path, e.g. Assets/Scenes/Main.unity.", Required = true)]
            public string Path { get; set; }
        }

        public sealed class AddSceneParameters : ScenePathParameters
        {
            [ToolParameter("Whether the scene is enabled in the list (default true).")]
            public bool? Enabled { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class StartResult
        {
            public bool Queued { get; set; }
            public string Target { get; set; }
            public string OutputPath { get; set; }
            public int Port { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SceneEntry
        {
            public string Path { get; set; }
            public bool Enabled { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SettingsResult
        {
            public string ActiveTarget { get; set; }
            public string TargetGroup { get; set; }
            public bool Development { get; set; }
            public bool AllowDebugging { get; set; }
            public bool BuildScriptsOnly { get; set; }
            public SceneEntry[] Scenes { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class StatusResult
        {
            public string State { get; set; }

            /// The last build report, or absent until one has run in this session.
            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public object LastReport { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SetSettingsResult
        {
            /// Setting name to the value that was (or would be) written.
            public object Applied { get; set; }

            public bool DryRun { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class TargetEntry
        {
            public string Name { get; set; }
            public string Group { get; set; }
            public bool Installed { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class TargetsResult
        {
            public TargetEntry[] Targets { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SceneListResult
        {
            public SceneEntry[] Scenes { get; set; }
        }

        public class Parameters
        {
            [ToolParameter(
                "Action to perform.",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"start\",\"status\",\"get_settings\",\"set_settings\",\"add_scene\",\"remove_scene\",\"list_targets\"]}")]
            public string Action { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("MISSING_PARAM", "Parameters cannot be null.");
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            if (!actionResult.IsSuccess)
                return new ErrorResponse("MISSING_PARAM", actionResult.ErrorMessage);

            switch (actionResult.Value.ToLowerInvariant())
            {
                case "start": return Start(p);
                case "status": return Status();
                case "get_settings": return GetSettings();
                case "set_settings": return SetSettings(p);
                case "add_scene": return AddScene(p);
                case "remove_scene": return RemoveScene(p);
                case "list_targets": return ListTargets();
                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action '{actionResult.Value}'. Valid: start, status, get_settings, set_settings, add_scene, remove_scene, list_targets.");
            }
        }

        // Same file-bus directory the test runner and package jobs use; the
        // TestRunner assembly keeps its copy internal, so the path is derived
        // here rather than referenced across the asmdef boundary.
        static readonly string StatusDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hera-agent-unity", "status");

        internal static string ResultFilePath(int port)
            => Path.Combine(StatusDir, $"build-result-{port}.json");

        // ---- start / status ----

        static object Start(ToolParams p)
        {
            if (UnityEngine.Application.isPlaying)
                return new ErrorResponse("IN_PLAY_MODE", "Cannot build while in play mode. Stop play mode first.");
            if (BuildPipeline.isBuildingPlayer || s_Queued)
                return new ErrorResponse("ALREADY_BUILDING", "A build is already queued or running. Poll `build status`.");

            var enabledScenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    enabledScenes.Add(scene.path);
            if (enabledScenes.Count == 0)
                return new ErrorResponse("NO_SCENES", "The Build Settings scene list has no enabled scenes. Use `build add_scene` first.");

            var target = EditorUserBuildSettings.activeBuildTarget;
            var (outputPath, pathErr) = ResolveOutputPath(p.Get("output_path"), target);
            if (pathErr != null) return pathErr;

            int port = HttpServer.Port;
            try
            {
                var stale = ResultFilePath(port);
                if (File.Exists(stale)) File.Delete(stale);
            }
            catch (Exception ex)
            {
                return new ErrorResponse("RESULT_FILE_BUSY", $"[Hera] I couldn't clear the previous build result file: {ex.Message}");
            }

            s_Queued = true;
            var scenes = enabledScenes.ToArray();
            // Not delayCall: it does not run in an unfocused Editor, so the build would
            // stay queued forever while the caller was told it started.
            EditorUpdate.Once(() => RunBuild(scenes, target, outputPath, port));

            return new SuccessResponse(
                "Build queued. The Editor blocks while building; poll `build status` or use `build start --wait`.",
                new StartResult
                {
                    Queued = true,
                    Target = target.ToString(),
                    OutputPath = outputPath,
                    Port = port,
                });
        }

        static void RunBuild(string[] scenes, BuildTarget target, string outputPath, int port)
        {
            s_Queued = false;
            object report;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
                var unityReport = BuildPipeline.BuildPlayer(
                    CreatePlayerOptions(scenes, target, outputPath));
                report = Summarize(unityReport, outputPath);
            }
            catch (Exception ex)
            {
                report = new
                {
                    result = "Failed",
                    output_path = outputPath,
                    target = target.ToString(),
                    error_count = 1,
                    warning_count = 0,
                    errors = new[] { Truncate(ex.Message) },
                };
            }

            s_LastReport = report;
            try
            {
                Directory.CreateDirectory(StatusDir);
                var envelope = new SuccessResponse("build finished", report);
                AtomicFile.WriteAllText(ResultFilePath(port), JsonConvert.SerializeObject(envelope));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Hera] I couldn't write the build result file: {ex.Message}");
            }
        }

        static object Summarize(BuildReport unityReport, string outputPath)
        {
            var summary = unityReport.summary;
            var errors = new List<string>();
            var seen = new HashSet<string>();
            foreach (var step in unityReport.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type != UnityEngine.LogType.Error && message.type != UnityEngine.LogType.Exception)
                        continue;
                    var text = Truncate(message.content);
                    if (seen.Add(text))
                        errors.Add(text);
                    if (errors.Count >= MaxErrorMessages) break;
                }
                if (errors.Count >= MaxErrorMessages) break;
            }
            return new
            {
                result = summary.result.ToString(),
                output_path = outputPath,
                target = summary.platform.ToString(),
                size_bytes = (long)summary.totalSize,
                total_seconds = summary.totalTime.TotalSeconds,
                error_count = summary.totalErrors,
                warning_count = summary.totalWarnings,
                errors,
            };
        }

        static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Trim();
            return text.Length <= MaxErrorLength ? text : text.Substring(0, MaxErrorLength) + "…";
        }

        static object Status()
        {
            string state = BuildPipeline.isBuildingPlayer ? "building" : (s_Queued ? "queued" : "idle");
            object lastReport = s_LastReport;
            if (lastReport == null)
            {
                try
                {
                    var path = ResultFilePath(HttpServer.Port);
                    if (File.Exists(path))
                        lastReport = JObject.Parse(File.ReadAllText(path))["data"];
                }
                catch { /* a half-written or consumed file simply yields no report */ }
            }
            return new SuccessResponse(
                $"build: {state}.",
                new { state, last_report = lastReport });
        }

        static (string path, ErrorResponse err) ResolveOutputPath(string requested, BuildTarget target)
        {
            string path = requested;
            if (string.IsNullOrEmpty(path))
            {
                var product = SanitizeFileName(PlayerSettings.productName);
                path = $"Builds/{target}/{product}{DefaultExtension(target)}";
            }

            var full = Path.GetFullPath(path).Replace('\\', '/');
            var assets = Path.GetFullPath("Assets").Replace('\\', '/');
            if (full.StartsWith(assets + "/", StringComparison.OrdinalIgnoreCase) || string.Equals(full, assets, StringComparison.OrdinalIgnoreCase))
                return (null, new ErrorResponse("INVALID_OUTPUT_PATH", "The build output cannot land inside Assets/ — Unity would try to import its own build."));
            return (path, null);
        }

        static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Player";
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name;
        }

        static string DefaultExtension(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64: return ".exe";
                case BuildTarget.StandaloneOSX: return ".app";
                case BuildTarget.Android: return ".apk";
                default: return "";
            }
        }

        // ---- settings / scenes / targets ----

        static object GetSettings()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            return new SuccessResponse("OK", new SettingsResult
            {
                ActiveTarget = target.ToString(),
                TargetGroup = BuildPipeline.GetBuildTargetGroup(target).ToString(),
                Development = EditorUserBuildSettings.development,
                AllowDebugging = EditorUserBuildSettings.allowDebugging,
                BuildScriptsOnly = EditorUserBuildSettings.buildScriptsOnly,
                Scenes = CurrentScenes(),
            });
        }

        static object SetSettings(ToolParams p)
        {
            bool dryRun = p.GetBool("dry_run");
            var applied = new Dictionary<string, object>();
            var appliers = new List<Action>();
            void StageBool(string field, Action<bool> apply)
            {
                var raw = p.GetRaw(field);
                if (raw == null || raw.Type == JTokenType.Null) return;
                bool value = p.GetBool(field);
                applied[field] = value;
                appliers.Add(() => apply(value));
            }
            StageBool("development", v => EditorUserBuildSettings.development = v);
            StageBool("allow_debugging", v => EditorUserBuildSettings.allowDebugging = v);
            StageBool("build_scripts_only", v => EditorUserBuildSettings.buildScriptsOnly = v);
            if (applied.Count == 0)
                return new ErrorResponse("NO_FIELDS", "No build settings fields were provided; pass at least one field to change.");
            if (!dryRun)
                foreach (var apply in appliers)
                    apply();
            return new SuccessResponse(
                $"{(dryRun ? "Would change" : "Changed")} {applied.Count} build setting(s).",
                new { applied, dry_run = dryRun });
        }

        static SceneEntry[] CurrentScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            var entries = new SceneEntry[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
                entries[i] = new SceneEntry { Path = scenes[i].path, Enabled = scenes[i].enabled };
            return entries;
        }

        static object AddScene(ToolParams p)
        {
            var pathResult = p.GetRequired("path", "'path' parameter required (scene asset path).");
            if (!pathResult.IsSuccess) return new ErrorResponse("MISSING_PARAM", pathResult.ErrorMessage);
            if (!AssetPathGuard.TryNormalizeAssetFile(pathResult.Value, out var path, out var pathErr))
                return new ErrorResponse("INVALID_PATH", pathErr);

            bool enabled = p.GetRaw("enabled") == null || p.GetBool("enabled", true);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var existing = scenes.FindIndex(s => string.Equals(s.path, path, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                scenes[existing] = new EditorBuildSettingsScene(path, enabled);
            else
                scenes.Add(new EditorBuildSettingsScene(path, enabled));
            EditorBuildSettings.scenes = scenes.ToArray();
            return new SuccessResponse(
                $"Scene {(existing >= 0 ? "updated" : "added")}: {path} (enabled={enabled}).",
                new SceneListResult { Scenes = CurrentScenes() });
        }

        static object RemoveScene(ToolParams p)
        {
            var pathResult = p.GetRequired("path", "'path' parameter required (scene asset path).");
            if (!pathResult.IsSuccess) return new ErrorResponse("MISSING_PARAM", pathResult.ErrorMessage);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int removed = scenes.RemoveAll(s => string.Equals(s.path, pathResult.Value, StringComparison.OrdinalIgnoreCase));
            EditorBuildSettings.scenes = scenes.ToArray();
            return new SuccessResponse(
                removed > 0 ? $"Scene removed: {pathResult.Value}." : $"Scene was not in the list: {pathResult.Value}.",
                new SceneListResult { Scenes = CurrentScenes() });
        }

        static object ListTargets()
        {
            var targets = new List<object>();
            foreach (BuildTarget target in Enum.GetValues(typeof(BuildTarget)))
            {
                if (target <= 0) continue;
                var group = BuildPipeline.GetBuildTargetGroup(target);
                targets.Add(new
                {
                    name = target.ToString(),
                    group = group.ToString(),
                    installed = BuildPipeline.IsBuildTargetSupported(group, target),
                });
            }
            return new SuccessResponse($"{targets.Count} build target(s).", new { targets });
        }
    }
}
