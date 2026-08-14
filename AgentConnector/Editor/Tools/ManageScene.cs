using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "scene",
        Description = "Scene operations: info, create, load, save, save_all, list, set_active, close, hierarchy (bounded GameObject tree dump).",
        Profiles = new[] { "core", "scene" },
        RiskClass = HeraRiskClass.Destructive,
        ContractMode = ToolContractMode.Strict)]
    public static partial class ManageScene
    {
        public sealed class LoadParameters
        {
            [ToolParameter(
                "Scene path or name.",
                Required = true,
                Aliases = new[] { "name", "target" })]
            public string Path { get; set; }

            [ToolParameter(
                "Load mode.",
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"single\",\"additive\",\"additive_without_loading\",\"additivewithoutloading\"]}")]
            public string Mode { get; set; }
        }

        public sealed class SaveParameters
        {
            [ToolParameter(
                "Loaded scene path or name. Omit to save the active scene.",
                Aliases = new[] { "name", "target" })]
            public string Path { get; set; }
        }

        public sealed class CloseParameters
        {
            [ToolParameter(
                "Loaded scene path or name.",
                Required = true,
                Aliases = new[] { "name", "target" })]
            public string Path { get; set; }
        }

        public sealed class HierarchyParameters
        {
            [ToolParameter("Root to scope the dump: instance_id integer or hierarchy path. Omit for every loaded scene.")]
            public string Root { get; set; }

            [ToolParameter(
                "Maximum tree depth below each root. 0 (default) = unlimited.",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":0}")]
            public int Depth { get; set; }

            [ToolParameter(
                "Maximum nodes in the result (default 500, cap 5000). The result reports truncated=true when hit.",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":5000}")]
            public int MaxNodes { get; set; }

            [ToolParameter("Include each node's short component type names.")]
            public bool Components { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class InfoResult
        {
            public ActiveSceneSummary Active { get; set; }
            public LoadedSceneSummary[] Loaded { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class ActiveSceneSummary
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public bool IsDirty { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class LoadedSceneSummary
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public bool IsLoaded { get; set; }
            public bool IsDirty { get; set; }
            public int RootCount { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SceneListEntry
        {
            public int Index { get; set; }
            public string Path { get; set; }
            public bool Enabled { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class LoadResult
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Mode { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SaveResult
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public bool Saved { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class CloseResult
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }

        public class Parameters
        {
            [ToolParameter("Action: info, create, load, save, save_all, list, set_active, close, hierarchy", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Scene path or name (used by load, save, close)")]
            public string Path { get; set; }

            [ToolParameter("Load mode for 'load': single (default), additive, additive_without_loading")]
            public string Mode { get; set; }
        }

        [HeraAction(
            ParametersType = typeof(object),
            ResultType = typeof(InfoResult),
            RiskClass = HeraRiskClass.ReadOnly)]
        public static object Info(JObject raw)
        {
            var active = SceneManager.GetActiveScene();
            var loaded = new List<object>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                loaded.Add(new
                {
                    name = s.name,
                    path = s.path,
                    isLoaded = s.isLoaded,
                    isDirty = s.isDirty,
                    rootCount = s.rootCount,
                });
            }
            return new SuccessResponse("OK", new
            {
                active = new { name = active.name, path = active.path, isDirty = active.isDirty },
                loaded = loaded,
            });
        }

        // Tree nodes self-reference (children), which the compiled-schema
        // generator cannot express, so this action keeps a generic output.
        [HeraAction(
            ParametersType = typeof(HierarchyParameters),
            RiskClass = HeraRiskClass.ReadOnly)]
        public static object Hierarchy(JObject raw)
        {
            var p = new ToolParams(raw);
            int depth = p.GetInt("depth") ?? 0;
            int maxNodes = Math.Min(Math.Max(p.GetInt("max_nodes") ?? 500, 1), 5000);
            bool includeComponents = p.GetBool("components");
            var budget = new NodeBudget { Remaining = maxNodes };

            string root = p.Get("root");
            if (!string.IsNullOrEmpty(root))
            {
                var (t, err) = TargetResolver.ResolveTransform(root);
                if (t == null)
                    return err ?? new ErrorResponse("TARGET_NOT_FOUND", $"No GameObject for root '{root}'.");
                var node = BuildNode(t, depth, 1, includeComponents, budget);
                return new SuccessResponse(
                    $"{maxNodes - budget.Remaining} node(s){(budget.Truncated ? " (truncated)" : "")}.",
                    new { root = node, node_count = maxNodes - budget.Remaining, truncated = budget.Truncated });
            }

            var scenes = new List<object>();
            var active = SceneManager.GetActiveScene();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                var roots = new List<object>();
                foreach (var go in s.GetRootGameObjects())
                {
                    if (budget.Remaining <= 0) { budget.Truncated = true; break; }
                    roots.Add(BuildNode(go.transform, depth, 1, includeComponents, budget));
                }
                scenes.Add(new
                {
                    name = s.name,
                    path = s.path,
                    is_active = s == active,
                    is_dirty = s.isDirty,
                    roots,
                });
            }
            return new SuccessResponse(
                $"{maxNodes - budget.Remaining} node(s) across {scenes.Count} scene(s){(budget.Truncated ? " (truncated)" : "")}.",
                new { scenes, node_count = maxNodes - budget.Remaining, truncated = budget.Truncated });
        }

        private sealed class NodeBudget
        {
            public int Remaining;
            public bool Truncated;
        }

        private static object BuildNode(
            UnityEngine.Transform t, int maxDepth, int currentDepth, bool includeComponents, NodeBudget budget)
        {
            budget.Remaining--;
            var go = t.gameObject;

            string[] componentNames = null;
            if (includeComponents)
            {
                var comps = go.GetComponents<UnityEngine.Component>();
                componentNames = new string[comps.Length];
                for (int i = 0; i < comps.Length; i++)
                    componentNames[i] = comps[i] == null ? "(missing)" : comps[i].GetType().Name;
            }

            // A depth cut is the caller's request, not truncation; only the
            // node budget sets the truncated flag.
            var children = new List<object>();
            if (maxDepth == 0 || currentDepth < maxDepth)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    if (budget.Remaining <= 0) { budget.Truncated = true; break; }
                    children.Add(BuildNode(t.GetChild(i), maxDepth, currentDepth + 1, includeComponents, budget));
                }
            }

            if (includeComponents)
            {
                return new
                {
                    instance_id = EntityIdCompat.IdOf(go),
                    name = go.name,
                    active = go.activeSelf,
                    components = componentNames,
                    children,
                };
            }
            return new
            {
                instance_id = EntityIdCompat.IdOf(go),
                name = go.name,
                active = go.activeSelf,
                children,
            };
        }

        [HeraAction(
            ParametersType = typeof(LoadParameters),
            ResultType = typeof(LoadResult),
            RiskClass = HeraRiskClass.Write)]
        public static object Load(JObject raw)
        {
            var p = new ToolParams(raw);
            var argsToken = p.GetRaw("args") as JArray;
            string target = p.Get("path") ?? p.Get("name") ?? p.Get("target")
                ?? (argsToken != null && argsToken.Count >= 2 ? argsToken[1].ToString() : null);
            string mode = p.Get("mode");
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("MISSING_PARAM", "'path' or positional scene path required for load.");

            var path = ResolvePath(target);
            if (path == null)
                return new ErrorResponse("SCENE_NOT_FOUND", $"Scene not found: '{target}'");

            var loadMode = OpenSceneMode.Single;
            if (!string.IsNullOrEmpty(mode))
            {
                switch (mode.ToLowerInvariant())
                {
                    case "single": loadMode = OpenSceneMode.Single; break;
                    case "additive": loadMode = OpenSceneMode.Additive; break;
                    case "additive_without_loading":
                    case "additivewithoutloading":
                        loadMode = OpenSceneMode.AdditiveWithoutLoading; break;
                    default:
                        return new ErrorResponse("INVALID_PARAM", $"Unknown mode: '{mode}'. Use single, additive, additive_without_loading.");
                }
            }

            if (loadMode == OpenSceneMode.Single)
            {
                var active = SceneManager.GetActiveScene();
                if (active.isDirty)
                    return new ErrorResponse("SCENE_DIRTY", $"Active scene '{active.name}' has unsaved changes. Save it first or use --mode additive.");
            }

            var scene = EditorSceneManager.OpenScene(path, loadMode);
            return new SuccessResponse($"Loaded scene: {scene.name}", new
            {
                name = scene.name,
                path = scene.path,
                mode = loadMode.ToString(),
            });
        }

        [HeraAction(
            ParametersType = typeof(SaveParameters),
            ResultType = typeof(SaveResult),
            RiskClass = HeraRiskClass.Write)]
        public static object Save(JObject raw)
        {
            var p = new ToolParams(raw);
            var argsToken = p.GetRaw("args") as JArray;
            string target = p.Get("path") ?? p.Get("name") ?? p.Get("target")
                ?? (argsToken != null && argsToken.Count >= 2 ? argsToken[1].ToString() : null);
            Scene scene;
            if (string.IsNullOrEmpty(target))
            {
                scene = SceneManager.GetActiveScene();
            }
            else
            {
                scene = FindLoaded(target);
                if (!scene.IsValid())
                    return new ErrorResponse("SCENE_NOT_LOADED", $"Scene not loaded: '{target}'");
            }

            if (!scene.isDirty)
            {
                return new SuccessResponse($"Scene clean: {scene.name}", new
                {
                    name = scene.name,
                    path = scene.path,
                    saved = false,
                });
            }

            bool ok = EditorSceneManager.SaveScene(scene);
            if (!ok)
                return new ErrorResponse("SCENE_SAVE_FAILED", $"Failed to save scene: {scene.name}");
            return new SuccessResponse($"Saved scene: {scene.name}", new
            {
                name = scene.name,
                path = scene.path,
                saved = true,
            });
        }

        [HeraAction(
            ParametersType = typeof(object),
            ResultType = typeof(SceneListEntry[]),
            RiskClass = HeraRiskClass.ReadOnly)]
        public static object List(JObject raw)
        {
            var registered = EditorBuildSettings.scenes;
            var list = new List<object>();
            for (int i = 0; i < registered.Length; i++)
            {
                var s = registered[i];
                list.Add(new
                {
                    index = i,
                    path = s.path,
                    enabled = s.enabled,
                });
            }
            return new SuccessResponse("OK", list);
        }

        [HeraAction(
            ParametersType = typeof(CloseParameters),
            ResultType = typeof(CloseResult),
            RiskClass = HeraRiskClass.Destructive)]
        public static object Close(JObject raw)
        {
            var p = new ToolParams(raw);
            var argsToken = p.GetRaw("args") as JArray;
            string target = p.Get("path") ?? p.Get("name") ?? p.Get("target")
                ?? (argsToken != null && argsToken.Count >= 2 ? argsToken[1].ToString() : null);
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("MISSING_PARAM", "'path' or positional scene name required for close.");

            var scene = FindLoaded(target);
            if (!scene.IsValid())
                return new ErrorResponse("SCENE_NOT_LOADED", $"Scene not loaded: '{target}'");

            if (SceneManager.sceneCount <= 1)
                return new ErrorResponse("SCENE_CLOSE_FORBIDDEN", "Cannot close the only loaded scene.");

            if (scene.isDirty)
                return new ErrorResponse("SCENE_DIRTY", $"Scene '{scene.name}' has unsaved changes. Save first.");

            // Snapshot identity before CloseScene; the Scene struct is invalidated by it.
            var capturedName = scene.name;
            var capturedPath = scene.path;
            bool ok = EditorSceneManager.CloseScene(scene, true);
            if (!ok)
                return new ErrorResponse("SCENE_CLOSE_FAILED", $"Failed to close scene: {capturedName}");
            return new SuccessResponse($"Closed scene: {capturedName}", new
            {
                name = capturedName,
                path = capturedPath,
            });
        }

        private static string ResolvePath(string target)
        {
            if (target.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && File.Exists(target))
                return target;
            if (File.Exists(target))
                return target;

            var bareName = System.IO.Path.GetFileNameWithoutExtension(target);
            var guids = AssetDatabase.FindAssets($"{bareName} t:Scene");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) continue;
                var matchName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.Equals(matchName, bareName, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return null;
        }

        private static Scene FindLoaded(string target)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name == target || s.path == target) return s;
            }
            return default;
        }
    }
}
