using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraActionSafety("remove_curve", Destructive = true, MayReloadDomain = true)]
    [HeraActionSafety("add_layer", MayReloadDomain = true)]
    [HeraActionContract("remove_curve", typeof(ManageAnimation.RemoveCurveParameters), ResultType = typeof(ManageAnimation.RemoveCurveResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("add_layer", typeof(ManageAnimation.AddLayerParameters), ResultType = typeof(ManageAnimation.AddLayerResult), RiskClass = HeraRiskClass.Write)]
    public static partial class ManageAnimation
    {
        public sealed class RemoveCurveParameters : PathParameters
        {
            [ToolParameter("Animated component type.", Required = true)]
            public string Type { get; set; }

            [ToolParameter("Animated property path.", Required = true)]
            public string Property { get; set; }

            [ToolParameter("GameObject path relative to the Animator root.")]
            public string RelativePath { get; set; }
        }

        public sealed class AddLayerParameters : PathParameters
        {
            [ToolParameter("Animator layer name.", Required = true)]
            public string Name { get; set; }

            [ToolParameter(
                "Default layer weight (default 1).",
                SchemaJson = "{\"type\":\"number\",\"minimum\":0,\"maximum\":1}")]
            public float? Weight { get; set; }

            [ToolParameter(
                "Layer blending mode.",
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"override\",\"additive\"]}")]
            public string Blending { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class RemoveCurveResult : AssetResult
        {
            public string RelativePath { get; set; }
            public string Type { get; set; }
            public string Property { get; set; }
            public int KeysRemoved { get; set; }
            public int TotalBindings { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class AddLayerResult : AssetResult
        {
            public string Name { get; set; }
            public int Index { get; set; }
            public float Weight { get; set; }
            public string Blending { get; set; }
            public int Layers { get; set; }
        }

        private static object RemoveCurve(ToolParams p)
        {
            var clip = LoadClip(
                p.Get("path"), "ASSET_NOT_FOUND",
                "No AnimationClip at that path (expects an existing .anim).", out var clipError);
            if (clip == null) return clipError;

            var typeName = p.Get("type");
            var type = ComponentTypeResolver.Resolve(typeName);
            if (type == null)
                return new ErrorResponse("UNKNOWN_COMPONENT_TYPE",
                    $"Could not resolve animated type '{typeName}'.",
                    data: new { did_you_mean = ComponentTypeResolver.SuggestSimilar(typeName) });

            var property = p.Get("property");
            if (string.IsNullOrEmpty(property))
                return new ErrorResponse("MISSING_PARAM", "'property' required for remove_curve.");

            var relativePath = p.Get("relative_path", "") ?? "";
            var binding = EditorCurveBinding.FloatCurve(relativePath, type, property);
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
                return new ErrorResponse("CURVE_NOT_FOUND", $"No curve for path='{relativePath}', type='{type.Name}', property='{property}'.");

            var keysRemoved = curve.length;
            AnimationUtility.SetEditorCurve(clip, binding, null);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return new SuccessResponse("Curve removed", new
            {
                path = AssetDatabase.GetAssetPath(clip),
                relative_path = relativePath,
                type = type.FullName,
                property,
                keys_removed = keysRemoved,
                total_bindings = AnimationUtility.GetCurveBindings(clip).Length,
            });
        }

        private static object AddLayer(ToolParams p)
        {
            var controller = LoadController(p.Get("path"), out var controllerError);
            if (controller == null) return controllerError;

            var name = p.Get("name");
            if (string.IsNullOrWhiteSpace(name))
                return new ErrorResponse("MISSING_PARAM", "'name' required for add_layer.");
            if (Array.Exists(controller.layers, layer => layer.name == name))
                return new ErrorResponse("LAYER_EXISTS", $"Layer '{name}' already exists on this controller.");

            var weight = p.GetFloat("weight", 1f) ?? 1f;
            if (weight < 0f || weight > 1f)
                return new ErrorResponse("INVALID_PARAM", "'weight' must be between 0 and 1.");
            if (!Enum.TryParse(p.Get("blending", "override"), true, out AnimatorLayerBlendingMode blending))
                return new ErrorResponse("INVALID_PARAM", "'blending' must be override or additive.");

            controller.AddLayer(name);
            var layers = controller.layers;
            var index = layers.Length - 1;
            layers[index].defaultWeight = weight;
            layers[index].blendingMode = blending;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new SuccessResponse("Layer added", new
            {
                path = AssetDatabase.GetAssetPath(controller),
                name,
                index,
                weight,
                blending = blending.ToString().ToLowerInvariant(),
                layers = layers.Length,
            });
        }
    }
}
