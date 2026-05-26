using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HeraAgent.Tools
{
    [HeraTool(Name = "scene", Description = "Scene operations: info, load, save, list, close.")]
    public static class ManageScene
    {
        public class Parameters
        {
            [ToolParameter("Action: info, load, save, list, close", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Scene path or name (used by load, save, close)")]
            public string Path { get; set; }

            [ToolParameter("Load mode for 'load': single (default), additive, additive_without_loading")]
            public string Mode { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            if (parameters == null)
                return new ErrorResponse("Parameters cannot be null.");

            var p = new ToolParams(parameters);
            var argsToken = p.GetRaw("args") as JArray;

            string action = p.Get("action")
                ?? (argsToken != null && argsToken.Count >= 1 ? argsToken[0].ToString() : null);
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' required: info, load, save, list, close");
            action = action.ToLowerInvariant();

            string target = p.Get("path") ?? p.Get("name") ?? p.Get("target")
                ?? (argsToken != null && argsToken.Count >= 2 ? argsToken[1].ToString() : null);

            switch (action)
            {
                case "info": return Info();
                case "load": return Load(target, p.Get("mode"));
                case "save": return Save(target);
                case "list": return List();
                case "close": return Close(target);
                default: return new ErrorResponse($"Unknown scene action: '{action}'. Use info, load, save, list, close.");
            }
        }

        private static object Info()
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

        private static object Load(string target, string mode)
        {
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'path' or positional scene path required for load.");

            var path = ResolvePath(target);
            if (path == null)
                return new ErrorResponse($"Scene not found: '{target}'");

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
                        return new ErrorResponse($"Unknown mode: '{mode}'. Use single, additive, additive_without_loading.");
                }
            }

            if (loadMode == OpenSceneMode.Single)
            {
                var active = SceneManager.GetActiveScene();
                if (active.isDirty)
                    return new ErrorResponse($"Active scene '{active.name}' has unsaved changes. Save it first or use --mode additive.");
            }

            var scene = EditorSceneManager.OpenScene(path, loadMode);
            return new SuccessResponse($"Loaded scene: {scene.name}", new
            {
                name = scene.name,
                path = scene.path,
                mode = loadMode.ToString(),
            });
        }

        private static object Save(string target)
        {
            Scene scene;
            if (string.IsNullOrEmpty(target))
            {
                scene = SceneManager.GetActiveScene();
            }
            else
            {
                scene = FindLoaded(target);
                if (!scene.IsValid())
                    return new ErrorResponse($"Scene not loaded: '{target}'");
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
                return new ErrorResponse($"Failed to save scene: {scene.name}");
            return new SuccessResponse($"Saved scene: {scene.name}", new
            {
                name = scene.name,
                path = scene.path,
                saved = true,
            });
        }

        private static object List()
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

        private static object Close(string target)
        {
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("'path' or positional scene name required for close.");

            var scene = FindLoaded(target);
            if (!scene.IsValid())
                return new ErrorResponse($"Scene not loaded: '{target}'");

            if (SceneManager.sceneCount <= 1)
                return new ErrorResponse("Cannot close the only loaded scene.");

            if (scene.isDirty)
                return new ErrorResponse($"Scene '{scene.name}' has unsaved changes. Save first.");

            // Snapshot identity before CloseScene; the Scene struct is invalidated by it.
            var capturedName = scene.name;
            var capturedPath = scene.path;
            bool ok = EditorSceneManager.CloseScene(scene, true);
            if (!ok)
                return new ErrorResponse($"Failed to close scene: {capturedName}");
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
