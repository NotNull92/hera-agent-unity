using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AI;
using UnityEngine;
using UnityEngine.Rendering;

#pragma warning disable CS0618

namespace HeraAgent.Tools
{
    [HeraActionContract("get_graphics", typeof(ManageSettings.EmptyParameters), ResultType = typeof(ManageSettings.GraphicsResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("set_graphics", typeof(ManageSettings.SetGraphicsParameters), ResultType = typeof(ManageSettings.SetResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("get_input", typeof(ManageSettings.GetInputParameters), ResultType = typeof(ManageSettings.InputResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("set_input", typeof(ManageSettings.SetInputParameters), ResultType = typeof(ManageSettings.SetResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("get_lighting", typeof(ManageSettings.EmptyParameters), ResultType = typeof(ManageSettings.LightingResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("set_lighting", typeof(ManageSettings.SetLightingParameters), ResultType = typeof(ManageSettings.SetResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("get_navmesh", typeof(ManageSettings.EmptyParameters), ResultType = typeof(ManageSettings.NavMeshResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("set_navmesh", typeof(ManageSettings.SetNavMeshParameters), ResultType = typeof(ManageSettings.SetResult), RiskClass = HeraRiskClass.Destructive)]
    public static partial class ManageSettings
    {
        private const string InputManagerPath = "ProjectSettings/InputManager.asset";
        private const string NavAgentRadius = "m_BuildSettings.agentRadius";
        private const string NavAgentHeight = "m_BuildSettings.agentHeight";
        private const string NavAgentSlope = "m_BuildSettings.agentSlope";
        private const string NavAgentClimb = "m_BuildSettings.agentClimb";
        private const string NavMinRegionArea = "m_BuildSettings.minRegionArea";
        private const string NavManualVoxelSize = "m_BuildSettings.manualCellSize";
        private const string NavVoxelSize = "m_BuildSettings.cellSize";

        public sealed class SetGraphicsParameters : DryRunParameters
        {
            [ToolParameter("Assets/ path or durable handle for a RenderPipelineAsset. Pass null or an empty string for the built-in render pipeline.", AllowNull = true)]
            public string RenderPipelineAsset { get; set; }
        }

        public sealed class GetInputParameters
        {
            [ToolParameter("Maximum axes returned (default 100, maximum 500).", SchemaJson = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":500}")]
            public int? Limit { get; set; }
        }

        public sealed class SetInputParameters : DryRunParameters
        {
            [ToolParameter("Exact legacy Input Manager axis name.", Required = true)]
            public string Axis { get; set; }

            [ToolParameter("Axis sensitivity (>= 0).", SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float? Sensitivity { get; set; }

            [ToolParameter("Axis gravity (>= 0).", SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float? Gravity { get; set; }

            [ToolParameter("Axis dead-zone size (>= 0).", SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float? Dead { get; set; }
        }

        public sealed class SetLightingParameters : DryRunParameters
        {
            [ToolParameter("LightingSettings.Lightmapper enum name.")]
            public string Lightmapper { get; set; }
            [ToolParameter("Enable baked global illumination.")]
            public bool? BakedGi { get; set; }
            [ToolParameter("Enable realtime global illumination.")]
            public bool? RealtimeGi { get; set; }
            [ToolParameter("Direct samples (>= 1).", SchemaJson = "{\"type\":\"integer\",\"minimum\":1}")]
            public int? DirectSampleCount { get; set; }
            [ToolParameter("Indirect samples (>= 1).", SchemaJson = "{\"type\":\"integer\",\"minimum\":1}")]
            public int? IndirectSampleCount { get; set; }
            [ToolParameter("Environment samples (>= 1).", SchemaJson = "{\"type\":\"integer\",\"minimum\":1}")]
            public int? EnvironmentSampleCount { get; set; }
            [ToolParameter("Maximum light bounces (>= 0).", SchemaJson = "{\"type\":\"integer\",\"minimum\":0}")]
            public int? Bounces { get; set; }
            [ToolParameter("Lightmap texels per world unit (> 0).", SchemaJson = "{\"type\":\"number\",\"exclusiveMinimum\":0}")]
            public float? LightmapResolution { get; set; }
            [ToolParameter("Lightmap padding in texels (>= 0).", SchemaJson = "{\"type\":\"integer\",\"minimum\":0}")]
            public int? LightmapPadding { get; set; }
            [ToolParameter("Maximum lightmap texture size (> 0).", SchemaJson = "{\"type\":\"integer\",\"exclusiveMinimum\":0}")]
            public int? MaxLightmapSize { get; set; }
            [ToolParameter("LightmapCompression enum name.")]
            public string LightmapCompression { get; set; }
            [ToolParameter("Directionality: non_directional or directional.", SchemaJson = "{\"type\":\"string\",\"enum\":[\"non_directional\",\"directional\"]}")]
            public string DirectionalMode { get; set; }
            [ToolParameter("Enable ambient occlusion.")]
            public bool? Ao { get; set; }
            [ToolParameter("Ambient-occlusion maximum distance (>= 0).", SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float? AoMaxDistance { get; set; }
            [ToolParameter("LightingSettings.FilterMode enum name.")]
            public string FilteringMode { get; set; }
        }

        public sealed class SetNavMeshParameters : DryRunParameters
        {
            [ToolParameter("Default agent radius (> 0).", SchemaJson = "{\"type\":\"number\",\"exclusiveMinimum\":0}")]
            public float? AgentRadius { get; set; }
            [ToolParameter("Default agent height (> 0).", SchemaJson = "{\"type\":\"number\",\"exclusiveMinimum\":0}")]
            public float? AgentHeight { get; set; }
            [ToolParameter("Maximum walkable slope in degrees (0-60).", SchemaJson = "{\"type\":\"number\",\"minimum\":0,\"maximum\":60}")]
            public float? AgentSlope { get; set; }
            [ToolParameter("Maximum step height (>= 0).", SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float? AgentClimb { get; set; }
            [ToolParameter("Minimum retained region area (>= 0).", SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float? MinRegionArea { get; set; }
            [ToolParameter("Manual NavMesh voxel size (> 0). Setting it enables manual_voxel_size.", SchemaJson = "{\"type\":\"number\",\"exclusiveMinimum\":0}")]
            public float? VoxelSize { get; set; }
            [ToolParameter("Use the explicit voxel size.")]
            public bool? ManualVoxelSize { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class GraphicsResult
        {
            public string RenderPipelineAsset { get; set; }
            public string RenderPipelineAssetPath { get; set; }
            public bool UsingScriptableRenderPipeline { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class InputAxisResult
        {
            public string Name { get; set; }
            public float Sensitivity { get; set; }
            public float Gravity { get; set; }
            public float Dead { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class InputResult
        {
            public int Total { get; set; }
            public int Returned { get; set; }
            public bool Truncated { get; set; }
            public InputAxisResult[] Axes { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class LightingResult
        {
            public bool Available { get; set; }
            public string Lightmapper { get; set; }
            public bool BakedGi { get; set; }
            public bool RealtimeGi { get; set; }
            public int DirectSampleCount { get; set; }
            public int IndirectSampleCount { get; set; }
            public int EnvironmentSampleCount { get; set; }
            public int Bounces { get; set; }
            public float LightmapResolution { get; set; }
            public int LightmapPadding { get; set; }
            public int MaxLightmapSize { get; set; }
            public string LightmapCompression { get; set; }
            public string DirectionalMode { get; set; }
            public bool Ao { get; set; }
            public float AoMaxDistance { get; set; }
            public string FilteringMode { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class NavMeshResult
        {
            public bool Available { get; set; }
            public float AgentRadius { get; set; }
            public float AgentHeight { get; set; }
            public float AgentSlope { get; set; }
            public float AgentClimb { get; set; }
            public float MinRegionArea { get; set; }
            public float VoxelSize { get; set; }
            public bool ManualVoxelSize { get; set; }
        }

        private static object GetGraphics()
        {
            var asset = GraphicsSettings.defaultRenderPipeline;
            return new SuccessResponse("OK", new GraphicsResult
            {
                RenderPipelineAsset = asset == null ? null : asset.name,
                RenderPipelineAssetPath = asset == null ? null : AssetDatabase.GetAssetPath(asset),
                UsingScriptableRenderPipeline = asset != null,
            });
        }

        private static object SetGraphics(ToolParams parameters)
        {
            var changes = new ChangeSet(parameters);
            var raw = parameters.GetRaw("render_pipeline_asset");
            if (raw != null)
            {
                RenderPipelineAsset asset = null;
                string assetPath = null;
                if (raw.Type != JTokenType.Null && !string.IsNullOrWhiteSpace(raw.ToString()))
                {
                    if (!AssetPathGuard.TryNormalizeExistingAssetFile(
                            raw.ToString(), out assetPath, out var resolved, out var errorCode, out var error))
                        return new ErrorResponse(errorCode ?? "INVALID_PATH", error);
                    asset = resolved as RenderPipelineAsset
                        ?? AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(assetPath);
                    if (asset == null)
                        return new ErrorResponse("RENDER_PIPELINE_ASSET_REQUIRED", $"'{assetPath}' is not a RenderPipelineAsset.");
                }
                changes.Stage("render_pipeline_asset", assetPath, () => GraphicsSettings.defaultRenderPipeline = asset);
            }

            var response = changes.Commit("graphics");
            if (!changes.DryRun && changes.Applied.Count > 0)
                AssetDatabase.SaveAssets();
            return response;
        }

        private static object GetInput(ToolParams parameters)
        {
            var limit = parameters.GetInt("limit") ?? 100;
            if (limit < 1 || limit > 500)
                return new ErrorResponse("INVALID_PARAM", "'limit' must be 1-500.");
            var input = LoadInputManager(out var error);
            if (input == null) return error;
            using (input)
            {
                input.Update();
                var axes = input.FindProperty("m_Axes");
                if (axes == null || !axes.isArray)
                    return new ErrorResponse("INPUT_SETTINGS_UNAVAILABLE", "Could not read InputManager.m_Axes.");
                var returned = Math.Min(axes.arraySize, limit);
                var results = new InputAxisResult[returned];
                for (var i = 0; i < returned; i++)
                {
                    var axis = axes.GetArrayElementAtIndex(i);
                    results[i] = new InputAxisResult
                    {
                        Name = axis.FindPropertyRelative("m_Name")?.stringValue,
                        Sensitivity = axis.FindPropertyRelative("sensitivity")?.floatValue ?? 0f,
                        Gravity = axis.FindPropertyRelative("gravity")?.floatValue ?? 0f,
                        Dead = axis.FindPropertyRelative("dead")?.floatValue ?? 0f,
                    };
                }
                return new SuccessResponse("OK", new InputResult
                {
                    Total = axes.arraySize,
                    Returned = returned,
                    Truncated = axes.arraySize > returned,
                    Axes = results,
                });
            }
        }

        private static object SetInput(ToolParams parameters)
        {
            var axisName = parameters.Get("axis");
            if (string.IsNullOrEmpty(axisName))
                return new ErrorResponse("MISSING_PARAM", "'axis' is required.");
            var input = LoadInputManager(out var error);
            if (input == null) return error;
            using (input)
            {
                input.Update();
                var axes = input.FindProperty("m_Axes");
                var axis = FindInputAxis(axes, axisName);
                if (axis == null)
                    return new ErrorResponse("INPUT_AXIS_NOT_FOUND", $"No legacy Input Manager axis named '{axisName}'.");

                var changes = new ChangeSet(parameters);
                StageSerializedFloat(parameters, changes, axis, "sensitivity", "sensitivity", value => value >= 0f, "must be >= 0");
                StageSerializedFloat(parameters, changes, axis, "gravity", "gravity", value => value >= 0f, "must be >= 0");
                StageSerializedFloat(parameters, changes, axis, "dead", "dead", value => value >= 0f, "must be >= 0");
                var response = changes.Commit("input");
                if (!changes.DryRun && changes.Applied.Count > 0)
                {
                    input.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                }
                return response;
            }
        }

        private static object GetLighting()
        {
            var settings = ActiveLightingSettings();
            if (settings == null)
                return new SuccessResponse("No active LightingSettings.", new LightingResult { Available = false });
            return new SuccessResponse("OK", new LightingResult
            {
                Available = true,
                Lightmapper = settings.lightmapper.ToString(),
                BakedGi = settings.bakedGI,
                RealtimeGi = settings.realtimeGI,
                DirectSampleCount = settings.directSampleCount,
                IndirectSampleCount = settings.indirectSampleCount,
                EnvironmentSampleCount = settings.environmentSampleCount,
                Bounces = settings.maxBounces,
                LightmapResolution = settings.lightmapResolution,
                LightmapPadding = settings.lightmapPadding,
                MaxLightmapSize = settings.lightmapMaxSize,
                LightmapCompression = settings.lightmapCompression.ToString(),
                DirectionalMode = (int)settings.directionalityMode == 0 ? "non_directional" : "directional",
                Ao = settings.ao,
                AoMaxDistance = settings.aoMaxDistance,
                FilteringMode = settings.filteringMode.ToString(),
            });
        }

        private static object SetLighting(ToolParams parameters)
        {
            var settings = ActiveLightingSettings();
            if (settings == null)
                return new ErrorResponse("LIGHTING_SETTINGS_UNAVAILABLE", "No active LightingSettings are available for the current scene.");
            var changes = new ChangeSet(parameters);
            StageEnum<LightingSettings.Lightmapper>(parameters, changes, "lightmapper", value => settings.lightmapper = value);
            StageBool(parameters, changes, "baked_gi", value => settings.bakedGI = value);
            StageBool(parameters, changes, "realtime_gi", value => settings.realtimeGI = value);
            StageInt(parameters, changes, "direct_sample_count", value => value >= 1, "must be >= 1", value => settings.directSampleCount = value);
            StageInt(parameters, changes, "indirect_sample_count", value => value >= 1, "must be >= 1", value => settings.indirectSampleCount = value);
            StageInt(parameters, changes, "environment_sample_count", value => value >= 1, "must be >= 1", value => settings.environmentSampleCount = value);
            StageInt(parameters, changes, "bounces", value => value >= 0, "must be >= 0", value => settings.maxBounces = value);
            StageFloat(parameters, changes, "lightmap_resolution", value => value > 0, "must be > 0", value => settings.lightmapResolution = value);
            StageInt(parameters, changes, "lightmap_padding", value => value >= 0, "must be >= 0", value => settings.lightmapPadding = value);
            StageInt(parameters, changes, "max_lightmap_size", value => value > 0, "must be > 0", value => settings.lightmapMaxSize = value);
            StageEnum<LightmapCompression>(parameters, changes, "lightmap_compression", value => settings.lightmapCompression = value);
            StageDirectionalMode(parameters, changes, settings);
            StageBool(parameters, changes, "ao", value => settings.ao = value);
            StageFloat(parameters, changes, "ao_max_distance", value => value >= 0, "must be >= 0", value => settings.aoMaxDistance = value);
            StageEnum<LightingSettings.FilterMode>(parameters, changes, "filtering_mode", value => settings.filteringMode = value);
            var response = changes.Commit("lighting");
            if (!changes.DryRun && changes.Applied.Count > 0)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            return response;
        }

        private static object GetNavMesh()
        {
            var settings = NavMeshBuilder.navMeshSettingsObject;
            if (settings == null)
                return new SuccessResponse("Legacy NavMesh settings unavailable.", new NavMeshResult { Available = false });
            using (var serialized = new SerializedObject(settings))
            {
                serialized.Update();
                return new SuccessResponse("OK", new NavMeshResult
                {
                    Available = true,
                    AgentRadius = FindFloat(serialized, NavAgentRadius),
                    AgentHeight = FindFloat(serialized, NavAgentHeight),
                    AgentSlope = FindFloat(serialized, NavAgentSlope),
                    AgentClimb = FindFloat(serialized, NavAgentClimb),
                    MinRegionArea = FindFloat(serialized, NavMinRegionArea),
                    VoxelSize = FindFloat(serialized, NavVoxelSize),
                    ManualVoxelSize = FindBool(serialized, NavManualVoxelSize),
                });
            }
        }

        private static object SetNavMesh(ToolParams parameters)
        {
            var settings = NavMeshBuilder.navMeshSettingsObject;
            if (settings == null)
                return new ErrorResponse("NAVMESH_SETTINGS_UNAVAILABLE", "Could not access the legacy NavMesh settings object.");
            using (var serialized = new SerializedObject(settings))
            {
                serialized.Update();
                var changes = new ChangeSet(parameters);
                StageSerializedFloat(parameters, changes, serialized, "agent_radius", NavAgentRadius, value => value > 0, "must be > 0");
                StageSerializedFloat(parameters, changes, serialized, "agent_height", NavAgentHeight, value => value > 0, "must be > 0");
                StageSerializedFloat(parameters, changes, serialized, "agent_slope", NavAgentSlope, value => value >= 0 && value <= 60, "must be 0-60");
                StageSerializedFloat(parameters, changes, serialized, "agent_climb", NavAgentClimb, value => value >= 0, "must be >= 0");
                StageSerializedFloat(parameters, changes, serialized, "min_region_area", NavMinRegionArea, value => value >= 0, "must be >= 0");
                StageVoxelSize(parameters, changes, serialized);
                StageSerializedBool(parameters, changes, serialized, "manual_voxel_size", NavManualVoxelSize);
                var response = changes.Commit("navmesh");
                if (!changes.DryRun && changes.Applied.Count > 0)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                }
                return response;
            }
        }

        private static SerializedObject LoadInputManager(out ErrorResponse error)
        {
            error = null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(InputManagerPath);
            if (assets != null && assets.Length > 0)
                return new SerializedObject(assets[0]);
            error = new ErrorResponse("INPUT_SETTINGS_UNAVAILABLE", "Could not access ProjectSettings/InputManager.asset.");
            return null;
        }

        private static SerializedProperty FindInputAxis(SerializedProperty axes, string name)
        {
            if (axes == null || !axes.isArray) return null;
            for (var i = 0; i < axes.arraySize; i++)
            {
                var axis = axes.GetArrayElementAtIndex(i);
                if (string.Equals(axis.FindPropertyRelative("m_Name")?.stringValue, name, StringComparison.Ordinal))
                    return axis;
            }
            return null;
        }

        private static LightingSettings ActiveLightingSettings()
        {
            try { return Lightmapping.lightingSettings; }
            catch { return null; }
        }

        private static void StageEnum<T>(ToolParams parameters, ChangeSet changes, string field, Action<T> apply)
            where T : struct
        {
            var raw = parameters.GetRaw(field);
            if (raw == null || raw.Type == JTokenType.Null) return;
            if (!Enum.TryParse(raw.ToString(), true, out T value) || !Enum.IsDefined(typeof(T), value))
            {
                changes.Stage(field, null, null, $"must be one of: {string.Join(", ", Enum.GetNames(typeof(T)))}");
                return;
            }
            changes.Stage(field, value.ToString(), () => apply(value));
        }

        private static void StageBool(ToolParams parameters, ChangeSet changes, string field, Action<bool> apply)
        {
            var raw = parameters.GetRaw(field);
            if (raw == null || raw.Type == JTokenType.Null) return;
            if (raw.Type != JTokenType.Boolean)
            {
                changes.Stage(field, null, null, "must be a boolean");
                return;
            }
            var value = raw.Value<bool>();
            changes.Stage(field, value, () => apply(value));
        }

        private static void StageDirectionalMode(ToolParams parameters, ChangeSet changes, LightingSettings settings)
        {
            var raw = parameters.GetRaw("directional_mode");
            if (raw == null || raw.Type == JTokenType.Null) return;
            var value = raw.ToString().ToLowerInvariant();
            if (value != "non_directional" && value != "directional")
            {
                changes.Stage("directional_mode", null, null, "must be non_directional or directional");
                return;
            }
            var mode = (LightmapsMode)(value == "non_directional" ? 0 : 1);
            changes.Stage("directional_mode", value, () => settings.directionalityMode = mode);
        }

        private static void StageSerializedFloat(
            ToolParams parameters,
            ChangeSet changes,
            SerializedProperty parent,
            string field,
            string relativePath,
            Func<float, bool> valid,
            string reason)
        {
            var property = parent.FindPropertyRelative(relativePath);
            StageSerializedFloat(parameters, changes, property, field, valid, reason);
        }

        private static void StageSerializedFloat(
            ToolParams parameters,
            ChangeSet changes,
            SerializedObject serialized,
            string field,
            string propertyPath,
            Func<float, bool> valid,
            string reason)
        {
            StageSerializedFloat(parameters, changes, serialized.FindProperty(propertyPath), field, valid, reason);
        }

        private static void StageSerializedFloat(
            ToolParams parameters,
            ChangeSet changes,
            SerializedProperty property,
            string field,
            Func<float, bool> valid,
            string reason)
        {
            var raw = parameters.GetRaw(field);
            if (raw == null || raw.Type == JTokenType.Null) return;
            var value = parameters.GetFloat(field);
            if (value == null)
            {
                changes.Stage(field, null, null, "must be a number");
                return;
            }
            if (!valid(value.Value))
            {
                changes.Stage(field, null, null, reason);
                return;
            }
            if (property == null)
            {
                changes.Stage(field, null, null, "not exposed by this Unity version");
                return;
            }
            changes.Stage(field, value.Value, () => property.floatValue = value.Value);
        }

        private static void StageSerializedBool(
            ToolParams parameters,
            ChangeSet changes,
            SerializedObject serialized,
            string field,
            string propertyPath)
        {
            var raw = parameters.GetRaw(field);
            if (raw == null || raw.Type == JTokenType.Null) return;
            if (raw.Type != JTokenType.Boolean)
            {
                changes.Stage(field, null, null, "must be a boolean");
                return;
            }
            var property = serialized.FindProperty(propertyPath);
            if (property == null)
            {
                changes.Stage(field, null, null, "not exposed by this Unity version");
                return;
            }
            var value = raw.Value<bool>();
            changes.Stage(field, value, () => property.boolValue = value);
        }

        private static void StageVoxelSize(ToolParams parameters, ChangeSet changes, SerializedObject serialized)
        {
            var raw = parameters.GetRaw("voxel_size");
            if (raw == null || raw.Type == JTokenType.Null) return;
            var value = parameters.GetFloat("voxel_size");
            var voxel = serialized.FindProperty(NavVoxelSize);
            var manual = serialized.FindProperty(NavManualVoxelSize);
            if (value == null || value <= 0)
            {
                changes.Stage("voxel_size", null, null, "must be > 0");
                return;
            }
            if (voxel == null || manual == null)
            {
                changes.Stage("voxel_size", null, null, "not exposed by this Unity version");
                return;
            }
            changes.Stage("voxel_size", value.Value, () =>
            {
                voxel.floatValue = value.Value;
                manual.boolValue = true;
            });
        }

        private static float FindFloat(SerializedObject serialized, string path)
        {
            return serialized.FindProperty(path)?.floatValue ?? 0f;
        }

        private static bool FindBool(SerializedObject serialized, string path)
        {
            return serialized.FindProperty(path)?.boolValue ?? false;
        }
    }
}
