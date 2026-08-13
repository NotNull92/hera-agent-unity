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
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"lighting\",\"navmesh\",\"navmesh_surfaces\",\"occlusion\"]}")]
            public string Area { get; set; }

            [ToolParameter("navmesh_surfaces only: restrict the operation to the NavMeshSurface components on this object and its children. Hierarchy path, instance_id, or durable handle. Defaults to every surface in the loaded scenes.")]
            public string Target { get; set; }
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

            [ToolParameter("Bake area: lighting, navmesh (built-in scene NavMesh), navmesh_surfaces (AI Navigation package components), or occlusion.", Required = true)]
            public string Area { get; set; }

            [ToolParameter("navmesh_surfaces only: restrict to the surfaces under one object.")]
            public string Target { get; set; }
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
            if (area != "lighting" && area != "navmesh" && area != "navmesh_surfaces" && area != "occlusion")
                return new ErrorResponse("INVALID_PARAM",
                    $"Unknown area '{area}'. Use lighting, navmesh, navmesh_surfaces, or occlusion.");

            if (area == "navmesh_surfaces")
                return Surfaces.Handle(action, p);

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

        // The AI Navigation package is a registry package, not a built-in
        // module, so everything here is reflection-only: the Connector has to
        // compile and run in projects that do not have it. Both types used are
        // public API — NavMeshSurface, and the NavMeshAssetManager
        // ScriptableSingleton the package's own Bake button drives — so this is
        // not reaching into package internals.
        static class Surfaces
        {
            const string PackageId = "com.unity.ai.navigation";

            static readonly System.Type SurfaceType =
                FindType("Unity.AI.Navigation.NavMeshSurface");
            static readonly System.Type ManagerType =
                FindType("Unity.AI.Navigation.Editor.NavMeshAssetManager");

            static System.Type FindType(string fullName)
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null) return type;
                }
                return null;
            }

            internal static object Handle(string action, ToolParams p)
            {
                if (SurfaceType == null || ManagerType == null)
                {
                    return new ErrorResponse("PACKAGE_NOT_INSTALLED",
                        $"This project has no {PackageId}, so it has no NavMeshSurface components to bake.",
                        suggestions: new System.Collections.Generic.List<string> { $"manage_packages add {PackageId}" });
                }

                if (action == "cancel")
                {
                    // Measured on 2.0.14: the package exposes no cancel for a
                    // surface bake. Reporting success would be a lie, and
                    // cancelling the built-in bake instead would stop something
                    // the caller never started.
                    return new ErrorResponse("CANCEL_UNSUPPORTED",
                        $"{PackageId} provides no way to cancel a surface bake; let it finish and poll `bake status --area navmesh_surfaces`.");
                }

                var collected = Collect(p, out var targetError);
                if (targetError != null) return targetError;

                switch (action)
                {
                    case "status": return Status(collected);
                    case "start": return Start(collected);
                    case "clear": return ClearSurfaces(collected);
                    default:
                        return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action '{action}'.");
                }
            }

            static UnityEngine.Object[] Collect(ToolParams p, out ErrorResponse err)
            {
                err = null;
                var target = p.Get("target");
                if (string.IsNullOrWhiteSpace(target))
                {
                    // 6000.5 deprecates the sort-mode overloads; earlier
                    // buckets do not have the two-argument one.
#if UNITY_6000_5_OR_NEWER
                    return UnityEngine.Object.FindObjectsByType(
                        SurfaceType,
                        UnityEngine.FindObjectsInactive.Include);
#else
                    return UnityEngine.Object.FindObjectsByType(
                        SurfaceType,
                        UnityEngine.FindObjectsInactive.Include,
                        UnityEngine.FindObjectsSortMode.None);
#endif
                }

                var (transform, resolveError) = TargetResolver.ResolveTransform(target);
                if (transform == null)
                {
                    err = resolveError ?? new ErrorResponse("TARGET_NOT_FOUND", $"No GameObject for '{target}'.");
                    return null;
                }

                var scoped = transform.GetComponentsInChildren(SurfaceType, true);
                var objects = new UnityEngine.Object[scoped.Length];
                for (int i = 0; i < scoped.Length; i++) objects[i] = scoped[i];
                return objects;
            }

            static object ManagerInstance() =>
                ManagerType.GetProperty("instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.FlattenHierarchy)?.GetValue(null);

            static bool IsBaking(object manager, UnityEngine.Object surface) =>
                (bool)ManagerType.GetMethod("IsSurfaceBaking", new[] { SurfaceType })
                    .Invoke(manager, new object[] { surface });

            // GetValue hands back an object, and `object != null` is reference
            // comparison — a NavMeshData destroyed by ClearSurfaces stays
            // reference-non-null and would keep counting as baked data. The
            // cast restores Unity's overloaded comparison, which reports a
            // destroyed object as null.
            static bool HasData(UnityEngine.Object surface)
            {
                var data = SurfaceType.GetProperty("navMeshData")?.GetValue(surface);
                return (UnityEngine.Object)data != null;
            }

            static object Status(UnityEngine.Object[] surfaces)
            {
                var manager = ManagerInstance();
                int baking = 0;
                int withData = 0;
                foreach (var surface in surfaces)
                {
                    if (manager != null && IsBaking(manager, surface)) baking++;
                    if (HasData(surface)) withData++;
                }
                return new SuccessResponse(
                    $"navmesh_surfaces: {(baking > 0 ? "baking" : "idle")}, {surfaces.Length} surface(s), {withData} with data.",
                    new
                    {
                        area = "navmesh_surfaces",
                        state = baking > 0 ? "baking" : "idle",
                        surfaces = surfaces.Length,
                        baking,
                        with_data = withData,
                    });
            }

            static object Start(UnityEngine.Object[] surfaces)
            {
                if (surfaces.Length == 0)
                {
                    return new ErrorResponse("NO_SURFACES",
                        "No NavMeshSurface components in the loaded scenes (or under --target). Add one, or bake the built-in mesh with --area navmesh.");
                }

                var scene = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(scene.path))
                {
                    return new ErrorResponse("SCENE_NOT_SAVED",
                        "The active scene is untitled; a surface bake writes NavMeshData assets beside the scene and has nowhere to put them. Save the scene first.");
                }

                var manager = ManagerInstance();
                if (manager == null)
                {
                    return new ErrorResponse("PACKAGE_NOT_INSTALLED",
                        $"{PackageId} is present but its NavMeshAssetManager did not load.");
                }

                foreach (var surface in surfaces)
                {
                    if (!IsBaking(manager, surface)) continue;
                    return new ErrorResponse("ALREADY_BAKING",
                        "A surface bake is already running. Poll `bake status --area navmesh_surfaces`.");
                }

                ManagerType.GetMethod("StartBakingSurfaces", new[] { typeof(UnityEngine.Object[]) })
                    .Invoke(manager, new object[] { surfaces });

                return new SuccessResponse(
                    $"navmesh_surfaces bake started for {surfaces.Length} surface(s); poll `bake status --area navmesh_surfaces` until idle.",
                    new { area = "navmesh_surfaces", started = true, surfaces = surfaces.Length });
            }

            static object ClearSurfaces(UnityEngine.Object[] surfaces)
            {
                var manager = ManagerInstance();
                if (manager == null)
                {
                    return new ErrorResponse("PACKAGE_NOT_INSTALLED",
                        $"{PackageId} is present but its NavMeshAssetManager did not load.");
                }

                ManagerType.GetMethod("ClearSurfaces", new[] { typeof(UnityEngine.Object[]) })
                    .Invoke(manager, new object[] { surfaces });

                return new SuccessResponse(
                    $"Cleared baked data on {surfaces.Length} surface(s).",
                    new { area = "navmesh_surfaces", cleared = true, surfaces = surfaces.Length });
            }
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
