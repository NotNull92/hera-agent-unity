using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;

namespace HeraAgent.Tools
{
    [HeraTool(
        Description = "Controls Unity editor state. Actions: play, stop, pause, set_active_tool, add_tag, remove_tag, add_layer, remove_layer, get_tags_layers, get_selection, set_selection.",
        Profiles = new[] { "core", "testing" },
        RiskClass = HeraRiskClass.Destructive,
        ContractMode = ToolContractMode.Strict)]
    [HeraActionContract("play", typeof(ManageEditor.EmptyParameters), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("stop", typeof(ManageEditor.EmptyParameters), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("pause", typeof(ManageEditor.EmptyParameters), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("set_active_tool", typeof(ManageEditor.SetActiveToolParameters), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("add_tag", typeof(ManageEditor.TagParameters), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("remove_tag", typeof(ManageEditor.TagParameters), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("add_layer", typeof(ManageEditor.LayerParameters), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("remove_layer", typeof(ManageEditor.LayerParameters), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("get_selection", typeof(ManageEditor.GetSelectionParameters), ResultType = typeof(ManageEditor.SelectionResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("get_tags_layers", typeof(ManageEditor.EmptyParameters), ResultType = typeof(ManageEditor.TagsLayersResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("set_selection", typeof(ManageEditor.SetSelectionParameters), ResultType = typeof(ManageEditor.SetSelectionResult), RiskClass = HeraRiskClass.Write)]
    public static class ManageEditor
    {
        private const int FirstUserLayerIndex = 8;
        private const int TotalLayerCount = 32;

        public sealed class EmptyParameters
        {
        }

        public sealed class SetActiveToolParameters
        {
            [ToolParameter(
                "Unity editor tool.",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"view\",\"move\",\"rotate\",\"scale\",\"rect\",\"transform\",\"custom\"]}")]
            public string ToolName { get; set; }
        }

        public sealed class TagParameters
        {
            [ToolParameter("Tag name.", Required = true)]
            public string TagName { get; set; }
        }

        public sealed class LayerParameters
        {
            [ToolParameter("Layer name.", Required = true)]
            public string LayerName { get; set; }
        }

        public sealed class GetSelectionParameters
        {
            [ToolParameter("Include each entry's global_id — a durable handle that survives domain reloads. Off by default to keep the payload small.")]
            public bool? Durable { get; set; }
        }

        public sealed class SetSelectionParameters
        {
            [ToolParameter(
                "Objects to select. Each entry is an instance_id integer, a scene hierarchy path, an Assets/ asset path, a guid:<32hex>[:<fileId>] asset handle, or a GlobalObjectId string. An empty array clears the selection.",
                Required = true,
                SchemaJson = "{\"type\":\"array\",\"items\":{\"type\":\"string\"}}")]
            public string[] Targets { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SelectionEntry
        {
            public int InstanceId { get; set; }
            public string Name { get; set; }
            public string Kind { get; set; }
            public string Path { get; set; }
            public string Type { get; set; }

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string GlobalId { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SelectionResult
        {
            public int Count { get; set; }
            public int ActiveInstanceId { get; set; }
            public SelectionEntry[] Objects { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class SetSelectionResult
        {
            public int Count { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class LayerEntry
        {
            public int Index { get; set; }
            public string Name { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class TagsLayersResult
        {
            public string[] Tags { get; set; }
            public LayerEntry[] Layers { get; set; }
        }

        public class Parameters
        {
            [ToolParameter(
                "Action to perform.",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"play\",\"stop\",\"pause\",\"set_active_tool\",\"add_tag\",\"remove_tag\",\"add_layer\",\"remove_layer\",\"get_tags_layers\",\"get_selection\",\"set_selection\"]}")]
            public string Action { get; set; }

            [ToolParameter("Tool name (required for set_active_tool action)")]
            public string ToolName { get; set; }

            [ToolParameter("Tag name (required for add_tag/remove_tag actions)")]
            public string TagName { get; set; }

            [ToolParameter("Layer name (required for add_layer/remove_layer actions)")]
            public string LayerName { get; set; }

            [ToolParameter("Selection targets (required for set_selection action)")]
            public string[] Targets { get; set; }
        }

        // Play/stop transitions trigger a domain reload that stops the HTTP
        // listener mid-response. Confirming "EnteredPlayMode" on this side
        // would never get to write a reply. The Go CLI polls the heartbeat
        // file instead for `--wait` (cmd/editor.go → waitForState), so this
        // handler returns synchronously the moment Unity accepts the request.
        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("MISSING_PARAM", "Parameters cannot be null.");

            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            if (!actionResult.IsSuccess)
                return new ErrorResponse("MISSING_PARAM", actionResult.ErrorMessage);

            string action = actionResult.Value.ToLowerInvariant();

            switch (action)
            {
                case "play":
                    if (!EditorApplication.isPlaying)
                    {
                        EditorApplication.isPlaying = true;
                        return new SuccessResponse("Entered play mode.");
                    }
                    return new SuccessResponse("Already in play mode.");

                case "pause":
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.isPaused = !EditorApplication.isPaused;
                        return new SuccessResponse(EditorApplication.isPaused ? "Game paused." : "Game resumed.");
                    }
                    return new ErrorResponse("NOT_IN_PLAY_MODE", "Cannot pause/resume: Not in play mode.");

                case "stop":
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.isPlaying = false;
                        return new SuccessResponse("Exited play mode.");
                    }
                    return new SuccessResponse("Already stopped (not in play mode).");

                case "set_active_tool":
                    var toolNameResult = p.GetRequired("tool_name", "'tool_name' parameter required.");
                    if (!toolNameResult.IsSuccess) return new ErrorResponse("MISSING_PARAM", toolNameResult.ErrorMessage);
                    if (Enum.TryParse<Tool>(toolNameResult.Value, true, out var targetTool) && targetTool != Tool.None && targetTool <= Tool.Custom)
                    {
                        UnityEditor.Tools.current = targetTool;
                        return new SuccessResponse($"Set active tool to '{targetTool}'.");
                    }
                    return new ErrorResponse("INVALID_PARAM", $"Could not parse '{toolNameResult.Value}' as a Unity Tool.");

                case "add_tag":
                    var addTagResult = p.GetRequired("tag_name", "'tag_name' parameter required.");
                    if (!addTagResult.IsSuccess) return new ErrorResponse("MISSING_PARAM", addTagResult.ErrorMessage);
                    if (InternalEditorUtility.tags.Contains(addTagResult.Value))
                        return new ErrorResponse("TAG_ALREADY_EXISTS", $"Tag '{addTagResult.Value}' already exists.");
                    InternalEditorUtility.AddTag(addTagResult.Value);
                    AssetDatabase.SaveAssets();
                    return new SuccessResponse($"Tag '{addTagResult.Value}' added.");

                case "remove_tag":
                    var removeTagResult = p.GetRequired("tag_name", "'tag_name' parameter required.");
                    if (!removeTagResult.IsSuccess) return new ErrorResponse("MISSING_PARAM", removeTagResult.ErrorMessage);
                    if (!InternalEditorUtility.tags.Contains(removeTagResult.Value))
                        return new ErrorResponse("TAG_NOT_FOUND", $"Tag '{removeTagResult.Value}' does not exist.");
                    InternalEditorUtility.RemoveTag(removeTagResult.Value);
                    AssetDatabase.SaveAssets();
                    return new SuccessResponse($"Tag '{removeTagResult.Value}' removed.");

                case "add_layer":
                case "remove_layer":
                    return ManageLayer(action, p);

                case "get_tags_layers":
                    return GetTagsLayers();

                case "get_selection":
                    return GetSelection(p.GetBool("durable"));

                case "set_selection":
                    return SetSelection(p);

                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action: '{action}'.");
            }
        }

        private static object GetTagsLayers()
        {
            var layers = new List<LayerEntry>();
            for (int i = 0; i < TotalLayerCount; i++)
            {
                var name = UnityEngine.LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                    layers.Add(new LayerEntry { Index = i, Name = name });
            }
            var tags = InternalEditorUtility.tags;
            return new SuccessResponse(
                $"{tags.Length} tag(s), {layers.Count} named layer(s).",
                new TagsLayersResult { Tags = tags, Layers = layers.ToArray() });
        }

        private static object GetSelection(bool durable)
        {
            var objects = Selection.objects;
            var entries = new SelectionEntry[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                bool isAsset = EditorUtility.IsPersistent(obj);
                string path = null;
                if (isAsset)
                {
                    path = AssetDatabase.GetAssetPath(obj);
                }
                else
                {
                    var t = (obj as UnityEngine.GameObject)?.transform ?? (obj as UnityEngine.Component)?.transform;
                    if (t != null) path = HierarchyPath.Build(t);
                }
                entries[i] = new SelectionEntry
                {
                    InstanceId = EntityIdCompat.IdOf(obj),
                    Name = obj.name,
                    Kind = isAsset ? "asset" : "scene",
                    Path = path,
                    Type = obj.GetType().Name,
                    GlobalId = durable ? ObjectIdentity.DurableIdOf(obj) : null,
                };
            }
            return new SuccessResponse(
                $"{entries.Length} object(s) selected.",
                new SelectionResult
                {
                    Count = entries.Length,
                    ActiveInstanceId = Selection.activeObject == null ? 0 : EntityIdCompat.IdOf(Selection.activeObject),
                    Objects = entries,
                });
        }

        private static object SetSelection(ToolParams p)
        {
            var raw = p.GetRaw("targets");
            if (raw == null || raw.Type != JTokenType.Array)
                return new ErrorResponse("MISSING_PARAM", "'targets' parameter required (array; empty clears the selection).");

            var targets = (JArray)raw;
            var resolved = new UnityEngine.Object[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                string target = targets[i]?.ToString();
                if (string.IsNullOrEmpty(target))
                    return new ErrorResponse("INVALID_PARAM", $"targets[{i}] is empty.");

                UnityEngine.Object obj;
                if (ObjectIdentity.IsDurableForm(target))
                {
                    if (!ObjectIdentity.TryResolve(target, out obj, out var durableErr))
                        return new ErrorResponse("OBJECT_NOT_FOUND", $"targets[{i}]: {durableErr}");
                }
                else if (int.TryParse(target, out var id))
                {
                    obj = EntityIdCompat.ToObject(id);
                    if (obj == null)
                        return new ErrorResponse("OBJECT_NOT_FOUND", $"targets[{i}]: no object for instance_id={id}.");
                }
                else if (target.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    obj = AssetDatabase.LoadMainAssetAtPath(target);
                    if (obj == null)
                        return new ErrorResponse("OBJECT_NOT_FOUND", $"targets[{i}]: no asset at '{target}'.");
                }
                else
                {
                    obj = HierarchyPath.Find(target);
                    if (obj == null)
                        return new ErrorResponse("TARGET_NOT_FOUND", $"targets[{i}]: no GameObject at path '{target}'.");
                }
                resolved[i] = obj;
            }

            Selection.objects = resolved;
            return new SuccessResponse(
                resolved.Length == 0 ? "Selection cleared." : $"Selected {resolved.Length} object(s).",
                new SetSelectionResult { Count = resolved.Length });
        }

        private static object ManageLayer(string action, ToolParams p)
        {
            var nameResult = p.GetRequired("layer_name", "'layer_name' parameter required.");
            if (!nameResult.IsSuccess) return new ErrorResponse("MISSING_PARAM", nameResult.ErrorMessage);

            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
                return new ErrorResponse("TAGMANAGER_ACCESS_FAILED", "Could not access TagManager asset.");

            using var tagManager = new SerializedObject(tagManagerAssets[0]);
            var layersProp = tagManager.FindProperty("layers");
            if (layersProp == null || !layersProp.isArray)
                return new ErrorResponse("TAGMANAGER_PROPERTY_NOT_FOUND", "Could not find 'layers' property.");

            if (action == "add_layer")
            {
                int firstEmpty = -1;
                for (int i = 0; i < TotalLayerCount; i++)
                {
                    var sp = layersProp.GetArrayElementAtIndex(i);
                    if (sp != null && nameResult.Value.Equals(sp.stringValue, StringComparison.OrdinalIgnoreCase))
                        return new ErrorResponse("LAYER_ALREADY_EXISTS", $"Layer '{nameResult.Value}' already exists at index {i}.");
                    if (firstEmpty == -1 && i >= FirstUserLayerIndex && (sp == null || string.IsNullOrEmpty(sp.stringValue)))
                        firstEmpty = i;
                }
                if (firstEmpty == -1) return new ErrorResponse("LAYER_SLOTS_FULL", "No empty layer slots available.");
                layersProp.GetArrayElementAtIndex(firstEmpty).stringValue = nameResult.Value;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return new SuccessResponse($"Layer '{nameResult.Value}' added to slot {firstEmpty}.");
            }
            else
            {
                for (int i = FirstUserLayerIndex; i < TotalLayerCount; i++)
                {
                    var sp = layersProp.GetArrayElementAtIndex(i);
                    if (sp != null && nameResult.Value.Equals(sp.stringValue, StringComparison.OrdinalIgnoreCase))
                    {
                        sp.stringValue = string.Empty;
                        tagManager.ApplyModifiedProperties();
                        AssetDatabase.SaveAssets();
                        return new SuccessResponse($"Layer '{nameResult.Value}' removed from slot {i}.");
                    }
                }
                return new ErrorResponse("LAYER_NOT_FOUND", $"User layer '{nameResult.Value}' not found.");
            }
        }

    }
}
