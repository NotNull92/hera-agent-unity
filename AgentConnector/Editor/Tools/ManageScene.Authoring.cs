using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeraAgent.Tools
{
    public static partial class ManageScene
    {
        public sealed class CreateParameters
        {
            [ToolParameter("New scene asset path under Assets/. The .unity extension is optional.", Required = true)]
            public string Path { get; set; }

            [ToolParameter(
                "Open mode.",
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"single\",\"additive\"]}")]
            public string Mode { get; set; }

            [ToolParameter(
                "Initial contents.",
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"empty\",\"default\"]}")]
            public string Template { get; set; }
        }

        public sealed class SetActiveParameters
        {
            [ToolParameter("Loaded scene path or name.", Required = true, Aliases = new[] { "name", "target" })]
            public string Path { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class CreateResult
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Mode { get; set; }
            public string Template { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SetActiveResult
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SaveAllResult
        {
            public bool Saved { get; set; }
            public string[] Scenes { get; set; }
        }

        [HeraAction(
            ParametersType = typeof(CreateParameters),
            ResultType = typeof(CreateResult),
            RiskClass = HeraRiskClass.Write)]
        public static object Create(JObject raw)
        {
            if (Application.isPlaying)
                return new ErrorResponse("SCENE_PLAY_MODE", "Scenes cannot be created while Unity is in Play Mode.");

            var p = new ToolParams(raw);
            var requestedPath = p.Get("path");
            if (!AssetPathGuard.TryPrepareNewAssetFile(
                    requestedPath, ".unity", appendExtension: true,
                    out var path, out var pathCode, out var pathError))
            {
                var code = pathCode == "ASSET_EXISTS"
                    ? "SCENE_EXISTS"
                    : pathCode == "PARENT_FOLDER_MISSING"
                        ? "SCENE_PARENT_MISSING"
                        : "SCENE_INVALID_PATH";
                return new ErrorResponse(code, pathError);
            }

            var mode = (p.Get("mode") ?? "single").ToLowerInvariant();
            NewSceneMode newSceneMode;
            switch (mode)
            {
                case "single":
                    newSceneMode = NewSceneMode.Single;
                    break;
                case "additive":
                    newSceneMode = NewSceneMode.Additive;
                    break;
                default:
                    return new ErrorResponse("INVALID_PARAM", $"Unknown mode: '{mode}'. Use single or additive.");
            }

            var template = (p.Get("template") ?? "empty").ToLowerInvariant();
            NewSceneSetup setup;
            switch (template)
            {
                case "empty":
                    setup = NewSceneSetup.EmptyScene;
                    break;
                case "default":
                    setup = NewSceneSetup.DefaultGameObjects;
                    break;
                default:
                    return new ErrorResponse("INVALID_PARAM", $"Unknown template: '{template}'. Use empty or default.");
            }

            if (newSceneMode == NewSceneMode.Single)
            {
                var dirty = DirtyLoadedScenes();
                if (dirty.Count > 0)
                    return new ErrorResponse("SCENE_DIRTY", $"Cannot replace dirty loaded scene(s): {string.Join(", ", dirty)}. Save them first or use additive mode.");
            }

            var scene = EditorSceneManager.NewScene(setup, newSceneMode);
            if (!EditorSceneManager.SaveScene(scene, path))
                return new ErrorResponse("SCENE_CREATE_FAILED", $"Failed to create scene at '{path}'.");

            AssetDatabase.Refresh();
            return new SuccessResponse($"Created scene: {scene.name}", new
            {
                name = scene.name,
                path = scene.path,
                mode,
                template,
            });
        }

        [HeraAction(
            ParametersType = typeof(SetActiveParameters),
            ResultType = typeof(SetActiveResult),
            RiskClass = HeraRiskClass.Write)]
        public static object SetActive(JObject raw)
        {
            var p = new ToolParams(raw);
            var target = p.Get("path") ?? p.Get("name") ?? p.Get("target");
            if (string.IsNullOrEmpty(target))
                return new ErrorResponse("MISSING_PARAM", "'path' required for set_active.");

            var scene = FindLoaded(target);
            if (!scene.IsValid() || !scene.isLoaded)
                return new ErrorResponse("SCENE_NOT_LOADED", $"Scene not loaded: '{target}'");
            if (!SceneManager.SetActiveScene(scene))
                return new ErrorResponse("SCENE_SET_ACTIVE_FAILED", $"Failed to set active scene: '{target}'");

            return new SuccessResponse($"Active scene: {scene.name}", new { name = scene.name, path = scene.path });
        }

        [HeraAction(
            ParametersType = typeof(object),
            ResultType = typeof(SaveAllResult),
            RiskClass = HeraRiskClass.Write)]
        public static object SaveAll(JObject raw)
        {
            if (Application.isPlaying)
                return new ErrorResponse("SCENE_PLAY_MODE", "Scenes cannot be saved while Unity is in Play Mode.");

            var dirtyPaths = new List<string>();
            var untitled = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !scene.isDirty) continue;
                if (string.IsNullOrEmpty(scene.path)) untitled.Add(scene.name);
                else dirtyPaths.Add(scene.path);
            }

            if (untitled.Count > 0)
                return new ErrorResponse("SCENE_UNSAVED", $"Cannot save all because dirty scene(s) have no asset path: {string.Join(", ", untitled)}.");
            if (dirtyPaths.Count == 0)
                return new SuccessResponse("No dirty scenes.", new { saved = false, scenes = Array.Empty<string>() });
            if (!EditorSceneManager.SaveOpenScenes())
                return new ErrorResponse("SCENE_SAVE_FAILED", "Failed to save one or more loaded scenes.");

            AssetDatabase.Refresh();
            return new SuccessResponse($"Saved {dirtyPaths.Count} scene(s).", new { saved = true, scenes = dirtyPaths });
        }

        private static List<string> DirtyLoadedScenes()
        {
            var dirty = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty)
                    dirty.Add(string.IsNullOrEmpty(scene.path) ? scene.name : scene.path);
            }
            return dirty;
        }
    }
}
