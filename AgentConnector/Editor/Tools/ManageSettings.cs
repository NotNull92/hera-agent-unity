using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraActionContract("get_physics", typeof(ManageSettings.EmptyParameters), ResultType = typeof(ManageSettings.PhysicsResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("set_physics", typeof(ManageSettings.SetPhysicsParameters), ResultType = typeof(ManageSettings.SetResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("get_time", typeof(ManageSettings.EmptyParameters), ResultType = typeof(ManageSettings.TimeResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("set_time", typeof(ManageSettings.SetTimeParameters), ResultType = typeof(ManageSettings.SetResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("get_quality", typeof(ManageSettings.EmptyParameters), ResultType = typeof(ManageSettings.QualityResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("set_quality", typeof(ManageSettings.SetQualityParameters), ResultType = typeof(ManageSettings.SetResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("get_player", typeof(ManageSettings.EmptyParameters), ResultType = typeof(ManageSettings.PlayerResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("set_player", typeof(ManageSettings.SetPlayerParameters), ResultType = typeof(ManageSettings.SetResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("get_audio", typeof(ManageSettings.EmptyParameters), ResultType = typeof(ManageSettings.AudioResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("set_audio", typeof(ManageSettings.SetAudioParameters), ResultType = typeof(ManageSettings.SetResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraSafetyRule(
        "preview",
        "dry_run",
        "true",
        RiskClass = HeraRiskClass.ReadOnly,
        ReadOnly = true,
        Idempotent = true)]
    [HeraTool(
        Name = "manage_settings",
        Description = "Read and change project settings by area: physics, time, quality, player, audio. get_<area> returns the full snapshot; set_<area> applies only the fields you pass (omitted fields stay untouched), supports dry_run previews, and reports {applied, skipped}. Settings changes are project-wide and not undoable, so set_* requires approval; dry_run runs without one.",
        Examples = new[]
        {
            "manage_settings get_physics",
            "manage_settings set_physics --params '{\"gravity\":[0,-19.62,0]}'",
            "manage_settings set_time --params '{\"fixed_delta_time\":0.01,\"dry_run\":true}'",
            "manage_settings set_quality --params '{\"level_name\":\"High\"}'",
        },
        ExampleDescriptions = new[]
        {
            "Read gravity, solver iterations, and thresholds",
            "Double gravity (requires approval)",
            "Preview a timestep change without applying it",
            "Switch the active quality level by name",
        },
        Profiles = new[] { "diagnostics", "full" },
        RiskClass = HeraRiskClass.Destructive,
        ContractMode = ToolContractMode.Strict)]
    public static class ManageSettings
    {
        public sealed class EmptyParameters
        {
        }

        public class DryRunParameters
        {
            [ToolParameter("Validate and report what would change without touching anything. Runs without approval.")]
            public bool? DryRun { get; set; }
        }

        public sealed class SetPhysicsParameters : DryRunParameters
        {
            [ToolParameter("World gravity as [x, y, z].", SchemaJson = "{\"type\":\"array\",\"minItems\":3,\"maxItems\":3,\"items\":{\"type\":\"number\"}}")]
            public JArray Gravity { get; set; }

            [ToolParameter("Default solver position iterations (>= 1).", SchemaJson = "{\"type\":\"integer\",\"minimum\":1}")]
            public int? DefaultSolverIterations { get; set; }

            [ToolParameter("Default solver velocity iterations (>= 1).", SchemaJson = "{\"type\":\"integer\",\"minimum\":1}")]
            public int? DefaultSolverVelocityIterations { get; set; }

            [ToolParameter("Relative-velocity floor below which collisions do not bounce.", SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float? BounceThreshold { get; set; }

            [ToolParameter("Default collider contact offset (> 0).", SchemaJson = "{\"type\":\"number\",\"exclusiveMinimum\":0}")]
            public float? DefaultContactOffset { get; set; }

            [ToolParameter("Energy floor below which rigidbodies go to sleep.", SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float? SleepThreshold { get; set; }
        }

        public sealed class SetTimeParameters : DryRunParameters
        {
            [ToolParameter("Fixed timestep in seconds (> 0).", SchemaJson = "{\"type\":\"number\",\"exclusiveMinimum\":0}")]
            public float? FixedDeltaTime { get; set; }

            [ToolParameter("Upper bound one frame may consume, in seconds (> 0).", SchemaJson = "{\"type\":\"number\",\"exclusiveMinimum\":0}")]
            public float? MaximumDeltaTime { get; set; }

            [ToolParameter("Global time scale (>= 0).", SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float? TimeScale { get; set; }
        }

        public sealed class SetQualityParameters : DryRunParameters
        {
            [ToolParameter("Active quality level index.", SchemaJson = "{\"type\":\"integer\",\"minimum\":0}")]
            public int? Level { get; set; }

            [ToolParameter("Active quality level by name (exact match against the project's level names).")]
            public string LevelName { get; set; }

            [ToolParameter("VSync count for the active level (0-4).", SchemaJson = "{\"type\":\"integer\",\"minimum\":0,\"maximum\":4}")]
            public int? VsyncCount { get; set; }

            [ToolParameter("MSAA sample count for the active level.", SchemaJson = "{\"type\":\"integer\",\"enum\":[0,2,4,8]}")]
            public int? AntiAliasing { get; set; }
        }

        public sealed class SetPlayerParameters : DryRunParameters
        {
            [ToolParameter("Company name.")]
            public string CompanyName { get; set; }

            [ToolParameter("Product name.")]
            public string ProductName { get; set; }

            [ToolParameter("Application bundle version string.")]
            public string BundleVersion { get; set; }
        }

        public sealed class SetAudioParameters : DryRunParameters
        {
            [ToolParameter("Global audio volume (0-1). Persisted to the project audio configuration.", SchemaJson = "{\"type\":\"number\",\"minimum\":0,\"maximum\":1}")]
            public float? Volume { get; set; }

            [ToolParameter("Doppler factor (>= 0).", SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float? DopplerFactor { get; set; }

            [ToolParameter("Rolloff scale (>= 0).", SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float? RolloffScale { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class PhysicsResult
        {
            public float[] Gravity { get; set; }
            public int DefaultSolverIterations { get; set; }
            public int DefaultSolverVelocityIterations { get; set; }
            public float BounceThreshold { get; set; }
            public float DefaultContactOffset { get; set; }
            public float SleepThreshold { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class TimeResult
        {
            public float FixedDeltaTime { get; set; }
            public float MaximumDeltaTime { get; set; }
            public float TimeScale { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class QualityResult
        {
            public int Level { get; set; }
            public string LevelName { get; set; }
            public string[] Names { get; set; }
            public int VsyncCount { get; set; }
            public int AntiAliasing { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class PlayerResult
        {
            public string CompanyName { get; set; }
            public string ProductName { get; set; }
            public string BundleVersion { get; set; }
            public string ScriptingBackend { get; set; }
            public string ApiCompatibilityLevel { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class AudioResult
        {
            public float Volume { get; set; }
            public float DopplerFactor { get; set; }
            public float RolloffScale { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SetResult
        {
            public Dictionary<string, object> Applied { get; set; }
            public Dictionary<string, string> Skipped { get; set; }
            public bool DryRun { get; set; }
        }

        public class Parameters
        {
            [ToolParameter(
                "Action to perform.",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"get_physics\",\"set_physics\",\"get_time\",\"set_time\",\"get_quality\",\"set_quality\",\"get_player\",\"set_player\",\"get_audio\",\"set_audio\"]}")]
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
                case "get_physics": return GetPhysics();
                case "set_physics": return SetPhysics(p);
                case "get_time": return GetTime();
                case "set_time": return SetTime(p);
                case "get_quality": return GetQuality();
                case "set_quality": return SetQuality(p);
                case "get_player": return GetPlayer();
                case "set_player": return SetPlayer(p);
                case "get_audio": return GetAudio();
                case "set_audio": return SetAudio(p);
                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action '{actionResult.Value}'. Valid: get/set_physics, get/set_time, get/set_quality, get/set_player, get/set_audio.");
            }
        }

        // ---- change collector: omitted fields untouched, dry_run previews,
        // invalid fields land in skipped with a reason while the rest apply ----

        private sealed class ChangeSet
        {
            public readonly Dictionary<string, object> Applied = new Dictionary<string, object>();
            public readonly Dictionary<string, string> Skipped = new Dictionary<string, string>();
            public readonly bool DryRun;
            private readonly List<Action> m_Appliers = new List<Action>();

            public ChangeSet(ToolParams p)
            {
                DryRun = p.GetBool("dry_run");
            }

            public void Stage(string field, object newValue, Action apply, string invalidReason = null)
            {
                if (invalidReason != null)
                {
                    Skipped[field] = invalidReason;
                    return;
                }
                Applied[field] = newValue;
                m_Appliers.Add(apply);
            }

            public object Commit(string area)
            {
                if (Applied.Count == 0 && Skipped.Count == 0)
                    return new ErrorResponse("NO_FIELDS", $"No {area} fields were provided; pass at least one field to change.");
                if (!DryRun)
                    foreach (var apply in m_Appliers)
                        apply();
                var verb = DryRun ? "Would change" : "Changed";
                return new SuccessResponse(
                    $"{verb} {Applied.Count} {area} field(s){(Skipped.Count > 0 ? $", skipped {Skipped.Count}" : "")}.",
                    new SetResult { Applied = Applied, Skipped = Skipped, DryRun = DryRun });
            }
        }

        // ---- physics ----

        private static object GetPhysics()
        {
            var g = Physics.gravity;
            return new SuccessResponse("OK", new PhysicsResult
            {
                Gravity = new[] { g.x, g.y, g.z },
                DefaultSolverIterations = Physics.defaultSolverIterations,
                DefaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations,
                BounceThreshold = Physics.bounceThreshold,
                DefaultContactOffset = Physics.defaultContactOffset,
                SleepThreshold = Physics.sleepThreshold,
            });
        }

        private static object SetPhysics(ToolParams p)
        {
            var changes = new ChangeSet(p);
            if (p.GetRaw("gravity") is JArray gravityToken)
            {
                if (SerializedPropertyValue.TryParseFloats(gravityToken, 3, out var g, out var parseErr))
                    changes.Stage("gravity", new[] { g[0], g[1], g[2] }, () => Physics.gravity = new Vector3(g[0], g[1], g[2]));
                else
                    changes.Stage("gravity", null, null, parseErr);
            }
            StageInt(p, changes, "default_solver_iterations", v => v >= 1, "must be >= 1", v => Physics.defaultSolverIterations = v);
            StageInt(p, changes, "default_solver_velocity_iterations", v => v >= 1, "must be >= 1", v => Physics.defaultSolverVelocityIterations = v);
            StageFloat(p, changes, "bounce_threshold", v => v >= 0, "must be >= 0", v => Physics.bounceThreshold = v);
            StageFloat(p, changes, "default_contact_offset", v => v > 0, "must be > 0", v => Physics.defaultContactOffset = v);
            StageFloat(p, changes, "sleep_threshold", v => v >= 0, "must be >= 0", v => Physics.sleepThreshold = v);
            return changes.Commit("physics");
        }

        // ---- time ----

        private static object GetTime()
        {
            return new SuccessResponse("OK", new TimeResult
            {
                FixedDeltaTime = Time.fixedDeltaTime,
                MaximumDeltaTime = Time.maximumDeltaTime,
                TimeScale = Time.timeScale,
            });
        }

        private static object SetTime(ToolParams p)
        {
            var changes = new ChangeSet(p);
            StageFloat(p, changes, "fixed_delta_time", v => v > 0, "must be > 0", v => Time.fixedDeltaTime = v);
            StageFloat(p, changes, "maximum_delta_time", v => v > 0, "must be > 0", v => Time.maximumDeltaTime = v);
            StageFloat(p, changes, "time_scale", v => v >= 0, "must be >= 0", v => Time.timeScale = v);
            return changes.Commit("time");
        }

        // ---- quality ----

        private static object GetQuality()
        {
            int level = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            return new SuccessResponse("OK", new QualityResult
            {
                Level = level,
                LevelName = level >= 0 && level < names.Length ? names[level] : null,
                Names = names,
                VsyncCount = QualitySettings.vSyncCount,
                AntiAliasing = QualitySettings.antiAliasing,
            });
        }

        private static object SetQuality(ToolParams p)
        {
            var changes = new ChangeSet(p);
            var names = QualitySettings.names;

            int? targetLevel = p.GetInt("level");
            string levelName = p.Get("level_name");
            if (targetLevel != null && !string.IsNullOrEmpty(levelName))
                return new ErrorResponse("INVALID_PARAM", "Pass 'level' or 'level_name', not both.");
            if (!string.IsNullOrEmpty(levelName))
            {
                int found = Array.IndexOf(names, levelName);
                if (found < 0)
                {
                    changes.Stage("level_name", null, null, $"no quality level named '{levelName}' (levels: {string.Join(", ", names)})");
                }
                else
                {
                    targetLevel = found;
                }
            }
            if (targetLevel != null)
            {
                int level = targetLevel.Value;
                if (level < 0 || level >= names.Length)
                    changes.Stage("level", null, null, $"level must be 0-{names.Length - 1}");
                else
                    changes.Stage("level", level, () => QualitySettings.SetQualityLevel(level, true));
            }
            StageInt(p, changes, "vsync_count", v => v >= 0 && v <= 4, "must be 0-4", v => QualitySettings.vSyncCount = v);
            StageInt(p, changes, "anti_aliasing", v => v == 0 || v == 2 || v == 4 || v == 8, "must be 0, 2, 4, or 8", v => QualitySettings.antiAliasing = v);
            return changes.Commit("quality");
        }

        // ---- player ----

        private static object GetPlayer()
        {
            var buildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            return new SuccessResponse("OK", new PlayerResult
            {
                CompanyName = PlayerSettings.companyName,
                ProductName = PlayerSettings.productName,
                BundleVersion = PlayerSettings.bundleVersion,
                ScriptingBackend = PlayerSettings.GetScriptingBackend(buildTarget).ToString(),
                ApiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(buildTarget).ToString(),
            });
        }

        private static object SetPlayer(ToolParams p)
        {
            var changes = new ChangeSet(p);
            StageString(p, changes, "company_name", v => PlayerSettings.companyName = v);
            StageString(p, changes, "product_name", v => PlayerSettings.productName = v);
            StageString(p, changes, "bundle_version", v => PlayerSettings.bundleVersion = v);
            var result = changes.Commit("player");
            if (!changes.DryRun && changes.Applied.Count > 0)
                AssetDatabase.SaveAssets();
            return result;
        }

        // ---- audio (persisted project audio configuration) ----

        private static SerializedObject LoadAudioManager(out ErrorResponse err)
        {
            err = null;
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
            if (assets == null || assets.Length == 0)
            {
                err = new ErrorResponse("AUDIOMANAGER_ACCESS_FAILED", "Could not access the project audio configuration asset.");
                return null;
            }
            return new SerializedObject(assets[0]);
        }

        private static object GetAudio()
        {
            var so = LoadAudioManager(out var err);
            if (so == null) return err;
            using (so)
            {
                return new SuccessResponse("OK", new AudioResult
                {
                    Volume = so.FindProperty("m_Volume")?.floatValue ?? 0f,
                    DopplerFactor = so.FindProperty("Doppler Factor")?.floatValue ?? 0f,
                    RolloffScale = so.FindProperty("Rolloff Scale")?.floatValue ?? 0f,
                });
            }
        }

        private static object SetAudio(ToolParams p)
        {
            var so = LoadAudioManager(out var err);
            if (so == null) return err;
            using (so)
            {
                var changes = new ChangeSet(p);
                StageAudioFloat(so, p, changes, "volume", "m_Volume", v => v >= 0 && v <= 1, "must be 0-1");
                StageAudioFloat(so, p, changes, "doppler_factor", "Doppler Factor", v => v >= 0, "must be >= 0");
                StageAudioFloat(so, p, changes, "rolloff_scale", "Rolloff Scale", v => v >= 0, "must be >= 0");
                var result = changes.Commit("audio");
                if (!changes.DryRun && changes.Applied.Count > 0)
                {
                    so.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                }
                return result;
            }
        }

        private static void StageAudioFloat(
            SerializedObject so, ToolParams p, ChangeSet changes,
            string field, string propertyPath, Func<float, bool> valid, string reason)
        {
            var raw = p.GetRaw(field);
            if (raw == null || raw.Type == JTokenType.Null) return;
            float? value = p.GetFloat(field);
            if (value == null) { changes.Stage(field, null, null, "must be a number"); return; }
            if (!valid(value.Value)) { changes.Stage(field, null, null, reason); return; }
            var property = so.FindProperty(propertyPath);
            if (property == null) { changes.Stage(field, null, null, "not exposed by this Unity version"); return; }
            changes.Stage(field, value.Value, () => property.floatValue = value.Value);
        }

        // ---- staging helpers ----

        private static void StageFloat(
            ToolParams p, ChangeSet changes, string field,
            Func<float, bool> valid, string reason, Action<float> apply)
        {
            var raw = p.GetRaw(field);
            if (raw == null || raw.Type == JTokenType.Null) return;
            float? value = p.GetFloat(field);
            if (value == null) { changes.Stage(field, null, null, "must be a number"); return; }
            if (!valid(value.Value)) { changes.Stage(field, null, null, reason); return; }
            changes.Stage(field, value.Value, () => apply(value.Value));
        }

        private static void StageInt(
            ToolParams p, ChangeSet changes, string field,
            Func<int, bool> valid, string reason, Action<int> apply)
        {
            var raw = p.GetRaw(field);
            if (raw == null || raw.Type == JTokenType.Null) return;
            int? value = p.GetInt(field);
            if (value == null) { changes.Stage(field, null, null, "must be an integer"); return; }
            if (!valid(value.Value)) { changes.Stage(field, null, null, reason); return; }
            changes.Stage(field, value.Value, () => apply(value.Value));
        }

        private static void StageString(ToolParams p, ChangeSet changes, string field, Action<string> apply)
        {
            var raw = p.GetRaw(field);
            if (raw == null || raw.Type == JTokenType.Null) return;
            string value = p.Get(field);
            changes.Stage(field, value, () => apply(value));
        }
    }
}
