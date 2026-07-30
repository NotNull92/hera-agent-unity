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
        Description = "Scene operations: info, load, save, list, close.",
        ContractMode = ToolContractMode.Strict)]
    public static class ManageScene
    {
        public sealed class InfoParameters
        {
        }

        public sealed class ListParameters
        {
        }

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

        public sealed class InfoResult
        {
            public ActiveSceneSummary Active { get; set; }
            public LoadedSceneSummary[] Loaded { get; set; }
        }

        public sealed class ActiveSceneSummary
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public bool IsDirty { get; set; }
        }

        public sealed class LoadedSceneSummary
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public bool IsLoaded { get; set; }
            public bool IsDirty { get; set; }
            public int RootCount { get; set; }
        }

        public sealed class SceneListEntry
        {
            public int Index { get; set; }
            public string Path { get; set; }
            public bool Enabled { get; set; }
        }

        public sealed class LoadResult
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Mode { get; set; }
        }

        public sealed class SaveResult
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public bool Saved { get; set; }
        }

        public sealed class CloseResult
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }

        public class Parameters
        {
            [ToolParameter("Action: info, load, save, list, close", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Scene path or name (used by load, save, close)")]
            public string Path { get; set; }

            [ToolParameter("Load mode for 'load': single (default), additive, additive_without_loading")]
            public string Mode { get; set; }
        }

        [HeraAction(
            ParametersType = typeof(InfoParameters),
            ResultType = typeof(InfoResult))]
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

        [HeraAction(
            ParametersType = typeof(LoadParameters),
            ResultType = typeof(LoadResult))]
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
            ResultType = typeof(SaveResult))]
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
            ParametersType = typeof(ListParameters),
            ResultType = typeof(SceneListEntry[]))]
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
            ResultType = typeof(CloseResult))]
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
