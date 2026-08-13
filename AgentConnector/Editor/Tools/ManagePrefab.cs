using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraActionContract("create", typeof(ManagePrefab.CreateParameters), ResultType = typeof(ManagePrefab.PrefabResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("instantiate", typeof(ManagePrefab.InstantiateParameters), ResultType = typeof(ManagePrefab.InstanceResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("add_component", typeof(ManagePrefab.ComponentParameters), ResultType = typeof(ManagePrefab.PrefabResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("remove_component", typeof(ManagePrefab.ComponentParameters), ResultType = typeof(ManagePrefab.PrefabResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("list_overrides", typeof(ManagePrefab.ListOverridesParameters), ResultType = typeof(ManagePrefab.OverridesResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("apply", typeof(ManagePrefab.TargetParameters), ResultType = typeof(ManagePrefab.InstanceActionResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("revert", typeof(ManagePrefab.TargetParameters), ResultType = typeof(ManagePrefab.InstanceActionResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("unpack", typeof(ManagePrefab.UnpackParameters), ResultType = typeof(ManagePrefab.UnpackResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraArgumentGroup(
        ToolArgumentGroupMode.ExactlyOne,
        "source",
        "instance_id",
        Action = "create",
        Path = "/source",
        Expected = "source or instance_id")]
    [HeraTool(
        Name = "manage_prefab",
        Description = "Prefab asset and instance operations. Asset side: create (save a scene GameObject as a prefab — saving from a prefab instance produces a Variant, reported as asset_type), instantiate, add_component / remove_component (headless edit via PrefabUtility.LoadPrefabContents — no prefab stage, no scene side effects; --child targets a descendant instead of the root). Instance side: list_overrides shows how a scene instance differs from its asset, apply pushes those differences into the asset, revert discards them, unpack breaks the link. Instance actions resolve --target to its outermost prefab instance root and report which root they used.",
        Examples = new[]
        {
            "manage_prefab create --source /Player --path Assets/Prefabs/Player.prefab",
            "manage_prefab instantiate --path Assets/Prefabs/Player.prefab --parent /Spawns",
            "manage_prefab add_component --path Assets/Prefabs/Player.prefab --component Rigidbody",
            "manage_prefab add_component --path Assets/Prefabs/Player.prefab --child /Player/Arm --component BoxCollider",
            "manage_prefab list_overrides --target /Player",
            "manage_prefab apply --target /Player",
            "manage_prefab revert --target /Player",
            "manage_prefab unpack --target /Player --mode outermost",
        },
        ExampleDescriptions = new[]
        {
            "Save a scene GameObject (by path or --instance_id) as a new prefab asset",
            "Instantiate a prefab into the active scene, optionally under a parent",
            "Add a component to the prefab root (headless edit, persisted to the asset)",
            "Add a component to a descendant inside the prefab asset",
            "Show how a scene instance differs from its prefab asset",
            "Write the instance's overrides into the prefab asset (affects every instance)",
            "Discard the instance's overrides",
            "Break the prefab link — outermost keeps nested instances, completely does not",
        },
        Profiles = new[] { "scene", "assets" },
        RiskClass = HeraRiskClass.Destructive,
        ContractMode = ToolContractMode.Strict)]
    public static class ManagePrefab
    {
        public class PathParameters
        {
            [ToolParameter("Prefab asset path under Assets/, or a durable handle for an existing prefab (create needs a plain path).", Required = true)]
            public string Path { get; set; }
        }

        public sealed class CreateParameters : PathParameters
        {
            [ToolParameter("Source scene GameObject hierarchy path.")]
            public string Source { get; set; }

            [ToolParameter("Source scene GameObject InstanceID.")]
            public int? InstanceId { get; set; }
        }

        public sealed class InstantiateParameters : PathParameters
        {
            [ToolParameter(
                "Optional parent hierarchy path or InstanceID.",
                SchemaJson = "{\"oneOf\":[{\"type\":\"string\"},{\"type\":\"integer\"}]}")]
            public JToken Parent { get; set; }
        }

        public sealed class ComponentParameters : PathParameters
        {
            [ToolParameter("Component type name.", Required = true)]
            public string Component { get; set; }

            [ToolParameter("Descendant inside the prefab, as a hierarchy path (e.g. /Player/Arm). Defaults to the prefab root.")]
            public string Child { get; set; }
        }

        public class TargetParameters
        {
            [ToolParameter(
                "Scene prefab instance: hierarchy path, instance_id, or durable handle. Resolved to its outermost prefab instance root.",
                Required = true)]
            public string Target { get; set; }
        }

        public sealed class ListOverridesParameters : TargetParameters
        {
            [ToolParameter("Include Unity's default overrides — the instance root's own Transform and name — which the Inspector's Overrides dropdown hides. Off by default.")]
            public bool? IncludeDefault { get; set; }
        }

        public sealed class UnpackParameters : TargetParameters
        {
            [ToolParameter(
                "Unpack depth. outermost keeps nested prefab instances connected; completely unpacks them too.",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"outermost\",\"completely\"]}")]
            public string Mode { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public class PrefabResult
        {
            public string Path { get; set; }
            public string Root { get; set; }
            public string Component { get; set; }
            public string[] Components { get; set; }

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string AssetType { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class InstanceResult
        {
            public int InstanceId { get; set; }
            public string Name { get; set; }
            public string Path { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class OverrideEntry
        {
            public string Path { get; set; }
            public string Type { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class AddedGameObjectEntry
        {
            public string Path { get; set; }
            public int SiblingIndex { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class RemovedGameObjectEntry
        {
            public string ParentPath { get; set; }
            public string Name { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class OverridesResult
        {
            public string InstanceRoot { get; set; }
            public string AssetPath { get; set; }
            public string AssetType { get; set; }
            public string Status { get; set; }
            public bool HasOverrides { get; set; }
            public bool IncludeDefault { get; set; }
            public OverrideEntry[] ObjectOverrides { get; set; }
            public OverrideEntry[] AddedComponents { get; set; }
            public OverrideEntry[] RemovedComponents { get; set; }
            public AddedGameObjectEntry[] AddedGameobjects { get; set; }
            public RemovedGameObjectEntry[] RemovedGameobjects { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class InstanceActionResult
        {
            public string InstanceRoot { get; set; }
            public string AssetPath { get; set; }
            public bool HasOverrides { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class UnpackResult
        {
            public string InstanceRoot { get; set; }
            public string Mode { get; set; }
            public string[] RemainingInstanceRoots { get; set; }
        }

        public class Parameters
        {
            [ToolParameter("Action: create, instantiate, add_component, remove_component.", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Prefab asset path (Assets/.../Name.prefab). Output for create; source for the others.", Required = true)]
            public string Path { get; set; }

            [ToolParameter("create: source scene GameObject by hierarchy path '/Root/Child' (alternative to --instance_id).")]
            public string Source { get; set; }

            [ToolParameter("create: source scene GameObject by InstanceID (alternative to --source).")]
            public int InstanceId { get; set; }

            [ToolParameter("add_component / remove_component: component type name (e.g. Rigidbody, BoxCollider).")]
            public string Component { get; set; }

            [ToolParameter("add_component / remove_component: descendant inside the prefab, as a hierarchy path. Defaults to the prefab root.")]
            public string Child { get; set; }

            [ToolParameter("instantiate: parent for the new instance — hierarchy path or InstanceID. Optional.")]
            public string Parent { get; set; }

            [ToolParameter("list_overrides / apply / revert / unpack: the scene prefab instance — hierarchy path, InstanceID, or durable handle.")]
            public string Target { get; set; }

            [ToolParameter("list_overrides: include the instance root's own Transform and name, which Unity treats as default overrides.")]
            public bool IncludeDefault { get; set; }

            [ToolParameter("unpack: 'outermost' or 'completely'.")]
            public string Mode { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var action = (p.GetRaw("args") as JArray)?[0]?.ToString() ?? p.Get("action");
            if (string.IsNullOrWhiteSpace(action))
                return new ErrorResponse("MISSING_PARAM",
                    "'action' required: create, instantiate, add_component, remove_component, list_overrides, apply, revert, or unpack.");

            switch (action.ToLowerInvariant())
            {
                // Instance actions address a scene object, not an asset file, so
                // they never take the prefab path.
                case "list_overrides": return ListOverrides(p);
                case "apply": return ApplyOrRevert(p, apply: true);
                case "revert": return ApplyOrRevert(p, apply: false);
                case "unpack": return Unpack(p);
            }

            var path = p.Get("path");
            if (string.IsNullOrWhiteSpace(path))
                return new ErrorResponse("MISSING_PARAM", "'path' required (the prefab asset path, e.g. Assets/Prefabs/X.prefab).");
            // create names a file that does not exist yet, so it keeps the plain
            // path rule; every other action names an existing asset and accepts
            // a durable handle for it.
            if (action.Equals("create", System.StringComparison.OrdinalIgnoreCase))
            {
                if (ObjectIdentity.IsDurableForm(path))
                    return new ErrorResponse("INVALID_PATH",
                        $"'{path}' is a handle for an existing asset; create needs an Assets/ path for the new prefab.");
                if (!AssetPathGuard.TryNormalizeAssetFile(path, out path, out var createErr))
                    return new ErrorResponse("INVALID_PATH", createErr);
            }
            else if (!AssetPathGuard.TryNormalizeExistingAssetFile(
                         path, out path, out _, out var pathCode, out var pathErr))
            {
                return new ErrorResponse(pathCode, pathErr);
            }

            switch (action.ToLowerInvariant())
            {
                case "create": return Create(p, path);
                case "instantiate": return Instantiate(p, path);
                case "add_component": return EditComponent(path, p.Get("component"), p.Get("child"), add: true);
                case "remove_component": return EditComponent(path, p.Get("component"), p.Get("child"), add: false);
                default:
                    return new ErrorResponse("UNKNOWN_ACTION",
                        $"Unknown action '{action}'. Valid: create, instantiate, add_component, remove_component, list_overrides, apply, revert, unpack.");
            }
        }

        private static object Create(ToolParams p, string path)
        {
            var (go, err) = ResolveSceneGo(p);
            if (err != null) return err;
            if (!AssetPathGuard.TryPrepareNewAssetFile(
                    path, ".prefab", appendExtension: false,
                    out path, out var pathCode, out var pathErr))
                return new ErrorResponse(pathCode, pathErr);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out var success);
            if (!success || prefab == null)
                return new ErrorResponse("PREFAB_SAVE_FAILED", $"Unity could not save '{go.name}' as a prefab at '{path}'.");

            // Saving from a prefab instance produces a Variant, not a regular
            // prefab. Report it rather than leaving the caller to discover the
            // inheritance later.
            return new SuccessResponse($"Saved {go.name} as prefab at {path}", new
            {
                path,
                root = prefab.name,
                components = GameObjectComponents.GetNames(prefab),
                asset_type = PrefabUtility.GetPrefabAssetType(prefab).ToString(),
            });
        }

        private static object Instantiate(ToolParams p, string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                return new ErrorResponse("PREFAB_NOT_FOUND", $"No prefab asset at '{path}'.");

            GameObject parent = null;
            var parentToken = p.GetRaw("parent");
            if (parentToken != null && parentToken.Type != JTokenType.Null && !string.IsNullOrWhiteSpace(parentToken.ToString()))
            {
                var (resolvedParent, perr) = ResolveByPathOrId(parentToken.ToString());
                if (perr != null) return perr;
                parent = resolvedParent;
            }

            var inst = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (inst == null)
                return new ErrorResponse("INSTANTIATE_FAILED", $"Unity could not instantiate '{path}'.");
            if (parent != null)
                inst.transform.SetParent(parent.transform, worldPositionStays: true);

            EditorUtility.SetDirty(inst);
            return new SuccessResponse($"Instantiated {asset.name}", new
            {
                instance_id = EntityIdCompat.IdOf(inst),
                name = inst.name,
                path = HierarchyPath.Build(inst.transform),
            });
        }

        private static object EditComponent(string path, string componentName, string child, bool add)
        {
            if (string.IsNullOrWhiteSpace(componentName))
                return new ErrorResponse("MISSING_PARAM", "'component' required (e.g. Rigidbody).");

            var type = ComponentTypeResolver.Resolve(componentName);
            if (type == null)
                return new ErrorResponse("COMPONENT_TYPE_NOT_FOUND",
                    $"No Component type '{componentName}'.",
                    data: new { did_you_mean = ComponentTypeResolver.SuggestSimilar(componentName) },
                    suggestions: null);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return new ErrorResponse("PREFAB_NOT_FOUND", $"No prefab asset at '{path}'.");

            // Headless edit: load the prefab into an isolated scene, mutate the
            // root, save, unload — all within this one call. No PrefabStage,
            // no open-scene side effects, so it fits the stateless model.
            var root = PrefabUtility.LoadPrefabContents(path);
            string editedPath;
            try
            {
                var target = root;
                if (!string.IsNullOrWhiteSpace(child))
                {
                    target = FindInContents(root, child);
                    if (target == null)
                        return new ErrorResponse("CHILD_NOT_FOUND",
                            $"No GameObject at '{child}' inside prefab '{path}'.",
                            new { available = ContentsPaths(root) });
                }
                editedPath = HierarchyPath.Build(target.transform);

                if (add)
                {
                    target.AddComponent(type);
                }
                else
                {
                    var comp = target.GetComponent(type);
                    if (comp == null)
                        return new ErrorResponse("COMPONENT_NOT_FOUND",
                            $"'{editedPath}' has no {type.Name} to remove.");
                    UnityEngine.Object.DestroyImmediate(comp, allowDestroyingAssets: true);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var savedTarget = string.IsNullOrWhiteSpace(child) ? saved : FindInContents(saved, editedPath);
            return new SuccessResponse(
                $"{(add ? "Added" : "Removed")} {type.Name} {(add ? "to" : "from")} {editedPath}",
                new
                {
                    path,
                    root = editedPath,
                    component = type.Name,
                    components = GameObjectComponents.GetNames(savedTarget ?? saved),
                });
        }

        // ---- instance overrides ----

        private static object ListOverrides(ToolParams p)
        {
            var (root, err) = ResolveInstanceRoot(p);
            if (err != null) return err;

            bool includeDefault = p.GetBool("include_default");
            var objectOverrides = new List<object>();
            foreach (var o in PrefabUtility.GetObjectOverrides(root, includeDefault))
                objectOverrides.Add(Entry(o.instanceObject));

            var addedComponents = new List<object>();
            foreach (var a in PrefabUtility.GetAddedComponents(root))
                addedComponents.Add(Entry(a.instanceComponent));

            var removedComponents = new List<object>();
            foreach (var r in PrefabUtility.GetRemovedComponents(root))
                removedComponents.Add(new
                {
                    path = PathOf(r.containingInstanceGameObject),
                    type = r.assetComponent == null ? null : r.assetComponent.GetType().Name,
                });

            var addedGameObjects = new List<object>();
            foreach (var a in PrefabUtility.GetAddedGameObjects(root))
                addedGameObjects.Add(new { path = PathOf(a.instanceGameObject), sibling_index = a.siblingIndex });

            var removedGameObjects = new List<object>();
            foreach (var r in PrefabUtility.GetRemovedGameObjects(root))
                removedGameObjects.Add(new
                {
                    parent_path = PathOf(r.parentOfRemovedGameObjectInInstance),
                    name = r.assetGameObject == null ? null : r.assetGameObject.name,
                });

            var response = new SuccessResponse(
                $"{PathOf(root)}: {objectOverrides.Count} object override(s), "
                + $"{addedComponents.Count} added / {removedComponents.Count} removed component(s), "
                + $"{addedGameObjects.Count} added / {removedGameObjects.Count} removed GameObject(s).",
                new
                {
                    instance_root = PathOf(root),
                    asset_path = AssetPathOf(root),
                    asset_type = PrefabUtility.GetPrefabAssetType(root).ToString(),
                    status = PrefabUtility.GetPrefabInstanceStatus(root).ToString(),
                    has_overrides = PrefabUtility.HasPrefabInstanceAnyOverrides(root, includeDefault),
                    include_default = includeDefault,
                    object_overrides = objectOverrides,
                    added_components = addedComponents,
                    removed_components = removedComponents,
                    added_gameobjects = addedGameObjects,
                    removed_gameobjects = removedGameObjects,
                });
            if (!includeDefault)
                response.agent_hint = "Unity hides the instance root's own Transform and name here, so an empty list does not mean the instance matches its asset. Pass --include_default to see those too.";
            return response;
        }

        private static object ApplyOrRevert(ToolParams p, bool apply)
        {
            var (root, err) = ResolveInstanceRoot(p);
            if (err != null) return err;

            var assetPath = AssetPathOf(root);
            if (apply)
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
            else
                PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);

            return new SuccessResponse(
                apply
                    ? $"Applied {PathOf(root)} overrides to {assetPath}."
                    : $"Reverted {PathOf(root)} to {assetPath}.",
                new
                {
                    instance_root = PathOf(root),
                    asset_path = assetPath,
                    has_overrides = PrefabUtility.HasPrefabInstanceAnyOverrides(root, false),
                });
        }

        private static object Unpack(ToolParams p)
        {
            var mode = p.Get("mode", "").Trim().ToLowerInvariant();
            if (mode != "outermost" && mode != "completely")
                return new ErrorResponse("MISSING_PARAM",
                    "'mode' required: 'outermost' keeps nested prefab instances connected, 'completely' unpacks them too. There is no safe default.");

            var (root, err) = ResolveInstanceRoot(p);
            if (err != null) return err;

            var rootPath = PathOf(root);
            var newRoots = PrefabUtility.UnpackPrefabInstanceAndReturnNewOutermostRoots(
                root,
                mode == "completely" ? PrefabUnpackMode.Completely : PrefabUnpackMode.OutermostRoot);

            var remaining = new List<string>();
            foreach (var r in newRoots)
                if (r != null) remaining.Add(PathOf(r));

            return new SuccessResponse(
                $"Unpacked {rootPath} ({mode}); {remaining.Count} nested prefab instance root(s) remain.",
                new
                {
                    instance_root = rootPath,
                    mode,
                    remaining_instance_roots = remaining,
                });
        }

        // Apply/revert/unpack all require the outermost instance root; Unity
        // throws when handed a child. Resolve to that root and report it, so a
        // destructive call never silently acts on something other than what the
        // caller named.
        private static (GameObject root, ErrorResponse err) ResolveInstanceRoot(ToolParams p)
        {
            var target = p.Get("target");
            if (string.IsNullOrWhiteSpace(target))
                return (null, new ErrorResponse("MISSING_PARAM",
                    "'target' required: the scene prefab instance, as a hierarchy path, instance_id, or durable handle."));

            var (transform, resolveErr) = TargetResolver.ResolveTransform(target);
            if (transform == null)
                return (null, resolveErr ?? new ErrorResponse("TARGET_NOT_FOUND", $"No GameObject for '{target}'."));

            var go = transform.gameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return (null, new ErrorResponse("NOT_A_PREFAB_INSTANCE",
                    $"'{PathOf(go)}' is not part of a prefab instance."));

            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (root == null)
                return (null, new ErrorResponse("NOT_A_PREFAB_INSTANCE",
                    $"'{PathOf(go)}' has no outermost prefab instance root."));
            return (root, null);
        }

        private static object Entry(UnityEngine.Object instanceObject)
        {
            var go = instanceObject as GameObject ?? (instanceObject as Component)?.gameObject;
            return new
            {
                path = go == null ? null : PathOf(go),
                type = instanceObject == null ? null : instanceObject.GetType().Name,
            };
        }

        private static string PathOf(GameObject go) =>
            go == null ? null : HierarchyPath.Build(go.transform);

        private static string AssetPathOf(GameObject instanceRoot)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
            return source == null ? null : AssetDatabase.GetAssetPath(source);
        }

        private static GameObject FindInContents(GameObject contentsRoot, string hierarchyPath)
        {
            foreach (var t in contentsRoot.GetComponentsInChildren<Transform>(true))
                if (HierarchyPath.Build(t) == hierarchyPath)
                    return t.gameObject;
            return null;
        }

        private static string[] ContentsPaths(GameObject contentsRoot)
        {
            var all = contentsRoot.GetComponentsInChildren<Transform>(true);
            var paths = new List<string>(all.Length);
            foreach (var t in all)
            {
                if (paths.Count >= 50) break;
                paths.Add(HierarchyPath.Build(t));
            }
            return paths.ToArray();
        }

        // ---- helpers ----

        // Both resolvers route through TargetResolver so --source and --parent
        // accept the same forms as the rest of Hera and, unlike GameObject.Find,
        // can see inactive objects.
        private static (GameObject go, ErrorResponse err) ResolveSceneGo(ToolParams p)
        {
            var idToken = p.GetRaw("instance_id");
            if (idToken != null && idToken.Type != JTokenType.Null)
            {
                var id = p.GetInt("instance_id");
                if (id == null)
                    return (null, new ErrorResponse("INVALID_INSTANCE_ID", $"Invalid 'instance_id': '{idToken}'."));
                var obj = EntityIdCompat.ToObject(id.Value);
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go == null)
                    return (null, new ErrorResponse("SOURCE_NOT_FOUND", $"No GameObject for instance_id={id.Value}."));
                return (go, null);
            }

            var src = p.Get("source");
            if (!string.IsNullOrEmpty(src))
                return ResolveByPathOrId(src);

            return (null, new ErrorResponse("MISSING_PARAM",
                "create needs a source GameObject: pass --source '/Root/Child' or --instance_id."));
        }

        private static (GameObject go, ErrorResponse err) ResolveByPathOrId(string s)
        {
            if (string.IsNullOrEmpty(s)) return (null, null);
            var (transform, err) = TargetResolver.ResolveTransform(s);
            return transform == null ? (null, err) : (transform.gameObject, null);
        }
    }
}
