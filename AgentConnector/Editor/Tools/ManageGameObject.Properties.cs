using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace HeraAgent.Tools
{
    public static partial class ManageGameObject
    {
        public sealed class SetTransformParameters : TargetParameters
        {
            [ToolParameter("Position in the selected coordinate space.", SchemaJson = Vector3Schema)]
            public JToken Position { get; set; }

            [ToolParameter("Euler rotation in degrees in the selected coordinate space.", SchemaJson = Vector3Schema)]
            public JToken Rotation { get; set; }

            [ToolParameter("Local scale.", SchemaJson = Vector3Schema)]
            public JToken Scale { get; set; }

            [ToolParameter(
                "Coordinate space for position and rotation.",
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"world\",\"local\"]}")]
            public string Space { get; set; }
        }

        public sealed class SetTagParameters : TargetParameters
        {
            [ToolParameter("Existing project tag.", Required = true)]
            public string Tag { get; set; }
        }

        public sealed class SetLayerParameters : TargetParameters
        {
            [ToolParameter(
                "Existing layer name or numeric index from 0 to 31.",
                Required = true,
                SchemaJson = "{\"oneOf\":[{\"type\":\"integer\",\"minimum\":0,\"maximum\":31},{\"type\":\"string\",\"minLength\":1}]}")]
            public JToken Layer { get; set; }
        }

        [HeraAction(
            ParametersType = typeof(SetTransformParameters),
            ResultType = typeof(GameObjectResult),
            RiskClass = HeraRiskClass.Write)]
        public static object SetTransform(JObject raw)
        {
            var p = new ToolParams(raw);
            var (go, targetError) = TargetResolver.ResolveGameObject(p, altPathKey: "target");
            if (targetError != null) return targetError;

            var positionToken = p.GetRaw("position");
            var rotationToken = p.GetRaw("rotation");
            var scaleToken = p.GetRaw("scale");
            var hasPosition = positionToken != null && positionToken.Type != JTokenType.Null;
            var hasRotation = rotationToken != null && rotationToken.Type != JTokenType.Null;
            var hasScale = scaleToken != null && scaleToken.Type != JTokenType.Null;
            if (!hasPosition && !hasRotation && !hasScale)
                return new ErrorResponse("MISSING_PARAM", "set_transform requires position, rotation, or scale.");

            Vector3 position = default;
            Vector3 rotation = default;
            Vector3 scale = default;
            if (hasPosition && !TryParseVector3(positionToken, out position, out var positionError))
                return new ErrorResponse("INVALID_PARAM", $"Invalid 'position': {positionError}");
            if (hasRotation && !TryParseVector3(rotationToken, out rotation, out var rotationError))
                return new ErrorResponse("INVALID_PARAM", $"Invalid 'rotation': {rotationError}");
            if (hasScale && !TryParseVector3(scaleToken, out scale, out var scaleError))
                return new ErrorResponse("INVALID_PARAM", $"Invalid 'scale': {scaleError}");

            var space = (p.Get("space") ?? "local").ToLowerInvariant();
            if (space != "world" && space != "local")
                return new ErrorResponse("INVALID_PARAM", $"Unknown space: '{space}'. Use world or local.");

            Undo.RecordObject(go.transform, "Hera SetTransform");
            if (space == "world")
            {
                if (hasPosition) go.transform.position = position;
                if (hasRotation) go.transform.eulerAngles = rotation;
            }
            else
            {
                if (hasPosition) go.transform.localPosition = position;
                if (hasRotation) go.transform.localEulerAngles = rotation;
            }
            if (hasScale) go.transform.localScale = scale;

            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse($"Updated transform for {go.name}.", BuildShallow(go));
        }

        [HeraAction(
            ParametersType = typeof(SetTagParameters),
            ResultType = typeof(GameObjectResult),
            RiskClass = HeraRiskClass.Write)]
        public static object SetTag(JObject raw)
        {
            var p = new ToolParams(raw);
            var (go, targetError) = TargetResolver.ResolveGameObject(p, altPathKey: "target");
            if (targetError != null) return targetError;

            var tag = p.Get("tag");
            if (string.IsNullOrEmpty(tag))
                return new ErrorResponse("MISSING_PARAM", "'tag' required for set_tag.");
            if (Array.IndexOf(InternalEditorUtility.tags, tag) < 0)
                return new ErrorResponse("TAG_NOT_FOUND", $"Tag '{tag}' does not exist in the project.");

            Undo.RecordObject(go, "Hera SetTag");
            go.tag = tag;
            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse($"Set {go.name}.tag = {tag}.", BuildShallow(go));
        }

        [HeraAction(
            ParametersType = typeof(SetLayerParameters),
            ResultType = typeof(GameObjectResult),
            RiskClass = HeraRiskClass.Write)]
        public static object SetLayer(JObject raw)
        {
            var p = new ToolParams(raw);
            var (go, targetError) = TargetResolver.ResolveGameObject(p, altPathKey: "target");
            if (targetError != null) return targetError;

            var layerToken = p.GetRaw("layer");
            if (layerToken == null || layerToken.Type == JTokenType.Null)
                return new ErrorResponse("MISSING_PARAM", "'layer' required for set_layer.");

            int layer;
            if (layerToken.Type == JTokenType.Integer)
            {
                layer = layerToken.Value<int>();
            }
            else
            {
                var value = layerToken.ToString();
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out layer))
                    layer = LayerMask.NameToLayer(value);
            }
            if (layer < 0 || layer > 31)
                return new ErrorResponse("LAYER_NOT_FOUND", $"Layer '{layerToken}' does not name an existing layer or index from 0 to 31.");

            Undo.RecordObject(go, "Hera SetLayer");
            go.layer = layer;
            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse($"Set {go.name}.layer = {layer}.", BuildShallow(go));
        }
    }
}
