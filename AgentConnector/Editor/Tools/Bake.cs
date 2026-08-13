using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace HeraAgent.Tools
{
    [HeraActionContract("start", typeof(Bake.AreaParameters), ResultType = typeof(Bake.StartResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("status", typeof(Bake.AreaParameters), ResultType = typeof(Bake.StatusResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("cancel", typeof(Bake.AreaParameters), ResultType = typeof(Bake.CancelResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("clear", typeof(Bake.AreaParameters), ResultType = typeof(Bake.ClearResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionSafety("start", SupportsCancellation = true)]
    [HeraActionSafety("cancel", Idempotent = true)]
    [HeraTool(
        Name = "bake",
        Description = "Scene bakes by area: lighting, navmesh (built-in scene NavMesh), occlusion. start triggers the async bake and returns immediately; poll status until it reports idle. cancel stops an in-progress bake; clear deletes the area's baked data (approval-gated). Status is computed live, so it survives reconnects and domain reloads.",
        Examples = new[]
        {
            "bake start --area lighting",
            "bake status --area lighting",
            "bake cancel --area lighting",
            "bake clear --area navmesh",
        },
        ExampleDescriptions = new[]
        {
            "Trigger an async lighting bake of the open scene(s)",
            "Poll bake state: idle | baking, with progress where available",
            "Cancel the in-progress lighting bake",
            "Delete the baked NavMesh (requires approval)",
        },
        Profiles = new[] { "scene", "full" },
        RiskClass = HeraRiskClass.Destructive,
        ContractMode = ToolContractMode.Strict)]
    public static class Bake
    {
        public sealed class AreaParameters
        {
            [ToolParameter(
                "Bake area.",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"lighting\",\"navmesh\",\"occlusion\"]}")]
            public string Area { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class StartResult
        {
            public string Area { get; set; }
            public bool Started { get; set; }

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string LightingWorkflowMode { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class StatusResult
        {
            public string Area { get; set; }
            public string State { get; set; }
            public bool HasBakedData { get; set; }

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public float? Progress { get; set; }

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public int? DataSizeBytes { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class CancelResult
        {
            public string Area { get; set; }
            public bool WasBaking { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class ClearResult
        {
            public string Area { get; set; }
            public bool Cleared { get; set; }
        }

        public class Parameters
        {
            [ToolParameter(
                "Action to perform.",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"start\",\"status\",\"cancel\",\"clear\"]}")]
            public string Action { get; set; }

            [ToolParameter("Bake area: lighting, navmesh, or occlusion.", Required = true)]
            public string Area { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("MISSING_PARAM", "Parameters cannot be null.");
            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            if (!actionResult.IsSuccess)
                return new ErrorResponse("MISSING_PARAM", actionResult.ErrorMessage);
            var areaResult = p.GetRequired("area", "'area' parameter required (lighting, navmesh, occlusion).");
            if (!areaResult.IsSuccess)
                return new ErrorResponse("MISSING_PARAM", areaResult.ErrorMessage);

            string action = actionResult.Value.ToLowerInvariant();
            string area = areaResult.Value.ToLowerInvariant();
            if (area != "lighting" && area != "navmesh" && area != "occlusion")
                return new ErrorResponse("INVALID_PARAM", $"Unknown area '{area}'. Use lighting, navmesh, or occlusion.");

            switch (action)
            {
                case "start": return Start(area);
                case "status": return Status(area);
                case "cancel": return Cancel(area);
                case "clear": return Clear(area);
                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action '{action}'. Valid: start, status, cancel, clear.");
            }
        }

        // Unity marks the editor-side scene NavMesh bake obsolete and points at
        // UnityEngine.AI.NavMeshBuilder, but that replacement only builds
        // NavMeshData objects at runtime (CollectSources / BuildNavMeshData /
        // UpdateNavMeshData / Cancel — measured on 6000.3.5f2); it has no
        // equivalent for baking or clearing the scene's built-in NavMesh. The
        // deprecated editor API is therefore the only path, suppressed here in
        // one place rather than at every call site. Likewise
        // Lightmapping.giWorkflowMode is deprecated with no non-obsolete
        // replacement — Lightmapping.lightingSettings throws when unassigned.
#pragma warning disable 618
        static bool SceneNavMeshIsBaking() => UnityEditor.AI.NavMeshBuilder.isRunning;

        static void SceneNavMeshBuildAsync() => UnityEditor.AI.NavMeshBuilder.BuildNavMeshAsync();

        static void SceneNavMeshCancel() => UnityEditor.AI.NavMeshBuilder.Cancel();

        static void SceneNavMeshClear() => UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();

        static string LightingWorkflowModeName() => Lightmapping.giWorkflowMode.ToString();
#pragma warning restore 618

        static bool IsBaking(string area)
        {
            switch (area)
            {
                case "lighting": return Lightmapping.isRunning;
                case "navmesh": return SceneNavMeshIsBaking();
                default: return StaticOcclusionCulling.isRunning;
            }
        }

        static bool HasBakedData(string area)
        {
            switch (area)
            {
                case "lighting":
                    return Lightmapping.lightingDataAsset != null
                           || UnityEngine.LightmapSettings.lightmaps.Length > 0;
                case "navmesh":
                    return UnityEngine.AI.NavMesh.CalculateTriangulation().vertices.Length > 0;
                default:
                    return StaticOcclusionCulling.umbraDataSize > 0;
            }
        }

        static object Start(string area)
        {
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
                return new ErrorResponse("SCENE_NOT_SAVED", "The active scene is untitled; baked data has nowhere durable to land. Save the scene first.");
            if (IsBaking(area))
                return new ErrorResponse("ALREADY_BAKING", $"A {area} bake is already running. Poll `bake status --area {area}` or cancel it first.");

            bool started;
            string workflowMode = null;
            switch (area)
            {
                case "lighting":
                    workflowMode = LightingWorkflowModeName();
                    started = Lightmapping.BakeAsync();
                    break;
                case "navmesh":
                    SceneNavMeshBuildAsync();
                    started = true;
                    break;
                default:
                    StaticOcclusionCulling.GenerateInBackground();
                    started = true;
                    break;
            }
            if (!started)
                return new ErrorResponse("BAKE_START_FAILED", $"Unity refused to start the {area} bake.");
            return new SuccessResponse(
                $"{area} bake started; poll `bake status --area {area}` until idle.",
                new StartResult { Area = area, Started = true, LightingWorkflowMode = workflowMode });
        }

        static object Status(string area)
        {
            bool baking = IsBaking(area);
            var result = new StatusResult
            {
                Area = area,
                State = baking ? "baking" : "idle",
                HasBakedData = HasBakedData(area),
            };
            if (area == "lighting" && baking)
                result.Progress = Lightmapping.buildProgress;
            if (area == "occlusion")
                result.DataSizeBytes = StaticOcclusionCulling.umbraDataSize;
            return new SuccessResponse($"{area}: {result.State}.", result);
        }

        static object Cancel(string area)
        {
            bool wasBaking = IsBaking(area);
            if (wasBaking)
            {
                switch (area)
                {
                    case "lighting": Lightmapping.Cancel(); break;
                    case "navmesh": SceneNavMeshCancel(); break;
                    default: StaticOcclusionCulling.Cancel(); break;
                }
            }
            return new SuccessResponse(
                wasBaking ? $"{area} bake cancelled." : $"No {area} bake was running.",
                new CancelResult { Area = area, WasBaking = wasBaking });
        }

        static object Clear(string area)
        {
            switch (area)
            {
                case "lighting":
                    Lightmapping.Clear();
                    Lightmapping.ClearLightingDataAsset();
                    break;
                case "navmesh":
                    SceneNavMeshClear();
                    break;
                default:
                    StaticOcclusionCulling.Clear();
                    break;
            }
            return new SuccessResponse(
                $"{area} baked data cleared.",
                new ClearResult { Area = area, Cleared = true });
        }
    }
}
