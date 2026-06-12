using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "manage_prefab",
        Description = "Prefab asset operations: create (save a scene GameObject as a prefab), instantiate (drop a prefab into the active scene), add_component / remove_component (edit the prefab asset headlessly via PrefabUtility.LoadPrefabContents — no prefab stage, no scene side effects). Component edits target the prefab root.",
        Examples = new[]
        {
            "manage_prefab create --source /Player --path Assets/Prefabs/Player.prefab",
            "manage_prefab instantiate --path Assets/Prefabs/Player.prefab --parent /Spawns",
            "manage_prefab add_component --path Assets/Prefabs/Player.prefab --component Rigidbody",
            "manage_prefab remove_component --path Assets/Prefabs/Player.prefab --component BoxCollider",
        },
        ExampleDescriptions = new[]
        {
            "Save a scene GameObject (by path or --instance_id) as a new prefab asset",
            "Instantiate a prefab into the active scene, optionally under a parent",
            "Add a component to the prefab root (headless edit, persisted to the asset)",
            "Remove a component from the prefab root",
        })]
    public static class ManagePrefab
    {
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

            [ToolParameter("instantiate: parent for the new instance — hierarchy path or InstanceID. Optional.")]
            public string Parent { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var action = (p.GetRaw("args") as JArray)?[0]?.ToString() ?? p.Get("action");
            if (string.IsNullOrWhiteSpace(action))
                return new ErrorResponse("MISSING_PARAM", "'action' required: create, instantiate, add_component, or remove_component.");

            var path = p.Get("path");
            if (string.IsNullOrWhiteSpace(path))
                return new ErrorResponse("MISSING_PARAM", "'path' required (the prefab asset path, e.g. Assets/Prefabs/X.prefab).");

            switch (action.ToLowerInvariant())
            {
                case "create": return Create(p, path);
                case "instantiate": return Instantiate(p, path);
                case "add_component": return EditComponent(path, p.Get("component"), add: true);
                case "remove_component": return EditComponent(path, p.Get("component"), add: false);
                default:
                    return new ErrorResponse("UNKNOWN_ACTION",
                        $"Unknown action '{action}'. Valid: create, instantiate, add_component, remove_component.");
            }
        }

        private static object Create(ToolParams p, string path)
        {
            var (go, err) = ResolveSceneGo(p);
            if (err != null) return new ErrorResponse("SOURCE_NOT_FOUND", err);

            var dir = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                return new ErrorResponse("PARENT_FOLDER_MISSING",
                    $"Parent folder '{dir}' does not exist. Create it first (it must be under Assets/).");

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out var success);
            if (!success || prefab == null)
                return new ErrorResponse("PREFAB_SAVE_FAILED", $"Unity could not save '{go.name}' as a prefab at '{path}'.");

            return new SuccessResponse($"Saved {go.name} as prefab at {path}", new
            {
                path,
                root = prefab.name,
                components = ComponentNames(prefab),
            });
        }

        private static object Instantiate(ToolParams p, string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                return new ErrorResponse("PREFAB_NOT_FOUND", $"No prefab asset at '{path}'.");

            var inst = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (inst == null)
                return new ErrorResponse("INSTANTIATE_FAILED", $"Unity could not instantiate '{path}'.");

            var parentToken = p.GetRaw("parent");
            if (parentToken != null && parentToken.Type != JTokenType.Null)
            {
                var (parent, perr) = ResolveByPathOrId(parentToken.ToString());
                if (perr != null)
                {
                    UnityEngine.Object.DestroyImmediate(inst);
                    return new ErrorResponse("PARENT_NOT_FOUND", perr);
                }
                inst.transform.SetParent(parent.transform, worldPositionStays: true);
            }

            EditorUtility.SetDirty(inst);
            return new SuccessResponse($"Instantiated {asset.name}", new
            {
                instance_id = EntityIdCompat.IdOf(inst),
                name = inst.name,
                path = HierarchyPath.Build(inst.transform),
            });
        }

        private static object EditComponent(string path, string componentName, bool add)
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
            try
            {
                if (add)
                {
                    root.AddComponent(type);
                }
                else
                {
                    var comp = root.GetComponent(type);
                    if (comp == null)
                        return new ErrorResponse("COMPONENT_NOT_FOUND",
                            $"Prefab root '{root.name}' has no {type.Name} to remove.");
                    UnityEngine.Object.DestroyImmediate(comp, allowDestroyingAssets: true);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return new SuccessResponse(
                $"{(add ? "Added" : "Removed")} {type.Name} {(add ? "to" : "from")} {saved.name}",
                new
                {
                    path,
                    component = type.Name,
                    components = ComponentNames(saved),
                });
        }

        // ---- helpers ----

        private static string[] ComponentNames(GameObject go)
        {
            return go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => c.GetType().Name)
                .ToArray();
        }

        private static (GameObject go, string err) ResolveSceneGo(ToolParams p)
        {
            var idToken = p.GetRaw("instance_id");
            if (idToken != null && idToken.Type != JTokenType.Null)
            {
                var id = p.GetInt("instance_id");
                if (id == null) return (null, $"Invalid 'instance_id': '{idToken}'.");
                var obj = EntityIdCompat.ToObject(id.Value);
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go == null) return (null, $"No GameObject for instance_id={id.Value}.");
                return (go, null);
            }

            var src = p.Get("source");
            if (!string.IsNullOrEmpty(src))
            {
                var found = GameObject.Find(src);
                if (found == null) return (null, $"No GameObject at path: '{src}'.");
                return (found, null);
            }

            return (null, "create needs a source GameObject: pass --source '/Root/Child' or --instance_id.");
        }

        private static (GameObject go, string err) ResolveByPathOrId(string s)
        {
            if (string.IsNullOrEmpty(s)) return (null, null);
            if (int.TryParse(s, out var id))
            {
                var obj = EntityIdCompat.ToObject(id);
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                return go == null ? (null, $"No GameObject for instance_id={id}.") : (go, null);
            }
            var found = GameObject.Find(s);
            return found == null ? (null, $"No GameObject at path: '{s}'.") : (found, null);
        }
    }
}
