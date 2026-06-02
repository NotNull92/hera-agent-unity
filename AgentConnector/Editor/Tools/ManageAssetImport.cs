using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "manage_asset_import",
        Description = "Read or change an asset's import settings via its AssetImporter (TextureImporter, ModelImporter, AudioImporter, …). get dumps the importer's serialized properties (or one); set writes one and reimports. Property paths are raw SerializedProperty paths (m_TextureType, m_sRGBTexture, m_MipMapMode) — same convention as manage_components. get with no --property first to discover them.",
        Examples = new[]
        {
            "manage_asset_import get --path Assets/Tex/icon.png",
            "manage_asset_import get --path Assets/Tex/icon.png --property m_sRGBTexture",
            "manage_asset_import set --path Assets/Tex/icon.png --property m_sRGBTexture --value 0",
            "manage_asset_import set --path Assets/Tex/icon.png --property m_EnableMipMap --value false",
        },
        ExampleDescriptions = new[]
        {
            "Dump every import setting + its current value (importer type included)",
            "Read one import setting",
            "Set an int/enum import setting, then reimport the asset",
            "Set a bool import setting (accepts true/false/1/0)",
        })]
    public static class ManageAssetImport
    {
        public class Parameters
        {
            [ToolParameter("Action: get, set.", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Asset path (Assets/.../file.ext).", Required = true)]
            public string Path { get; set; }

            [ToolParameter("SerializedProperty path on the importer (m_TextureType, m_sRGBTexture, …). get: omit to dump all. set: required.")]
            public string Property { get; set; }

            [ToolParameter("Value for set. Scalars via --value; complex shapes via --params '{\"value\":...}'.")]
            public string Value { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var action = (p.GetRaw("args") as JArray)?[0]?.ToString() ?? p.Get("action");
            if (string.IsNullOrWhiteSpace(action))
                return new ErrorResponse("MISSING_PARAM", "'action' required: get or set.");

            var path = p.Get("path");
            if (string.IsNullOrWhiteSpace(path))
                return new ErrorResponse("MISSING_PARAM", "'path' required (the asset path, e.g. Assets/Tex/icon.png).");

            var importer = AssetImporter.GetAtPath(path);
            if (importer == null)
                return new ErrorResponse("ASSET_NOT_FOUND",
                    $"No imported asset at '{path}' (path must be under Assets/ and already imported).");

            switch (action.ToLowerInvariant())
            {
                case "get": return Get(importer, path, p.Get("property"));
                case "set": return Set(importer, path, p.Get("property"), p.GetRaw("value"));
                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action '{action}'. Valid: get, set.");
            }
        }

        private static object Get(AssetImporter importer, string path, string property)
        {
            using var so = new SerializedObject(importer);

            if (string.IsNullOrWhiteSpace(property))
            {
                var props = new Dictionary<string, object>();
                var iter = so.GetIterator();
                var enter = true;
                while (iter.NextVisible(enter))
                {
                    enter = false;
                    try { props[iter.name] = SerializedPropertyValue.Read(iter); }
                    catch (System.Exception ex) { props[iter.name] = new { read_error = ex.Message }; }
                }
                return new SuccessResponse($"{importer.GetType().Name} for {path}", new
                {
                    path,
                    importer_type = importer.GetType().Name,
                    properties = props,
                });
            }

            var prop = so.FindProperty(property);
            if (prop == null)
                return PropertyNotFound(so, importer, property);

            return new SuccessResponse($"{importer.GetType().Name}.{property}", new
            {
                path,
                importer_type = importer.GetType().Name,
                property,
                property_type = prop.propertyType.ToString(),
                value = SerializedPropertyValue.Read(prop),
            });
        }

        private static object Set(AssetImporter importer, string path, string property, JToken value)
        {
            if (string.IsNullOrWhiteSpace(property))
                return new ErrorResponse("MISSING_PARAM", "'property' required for set (e.g. m_sRGBTexture).");
            if (value == null)
                return new ErrorResponse("MISSING_PARAM", "'value' required for set.");

            using var so = new SerializedObject(importer);
            var prop = so.FindProperty(property);
            if (prop == null)
                return PropertyNotFound(so, importer, property);

            var propType = prop.propertyType.ToString();
            var (ok, err) = SerializedPropertyValue.Apply(prop, value);
            if (!ok)
                return new ErrorResponse("VALUE_COERCION_FAILED",
                    $"Failed to apply value to {property} ({propType}): {err}");

            so.ApplyModifiedProperties();
            importer.SaveAndReimport();

            // Re-read from a fresh importer — SaveAndReimport may have clamped or
            // normalized the value (and invalidated the old SerializedObject).
            var fresh = AssetImporter.GetAtPath(path);
            using var soFresh = new SerializedObject(fresh);
            var propFresh = soFresh.FindProperty(property);
            return new SuccessResponse($"Set {fresh.GetType().Name}.{property} and reimported {path}", new
            {
                path,
                importer_type = fresh.GetType().Name,
                property,
                property_type = propType,
                value = SerializedPropertyValue.Read(propFresh),
            });
        }

        private static ErrorResponse PropertyNotFound(SerializedObject so, AssetImporter importer, string property)
        {
            var names = new List<string>();
            var iter = so.GetIterator();
            var enter = true;
            while (iter.NextVisible(enter))
            {
                enter = false;
                names.Add(iter.name);
            }
            return new ErrorResponse("PROPERTY_NOT_FOUND",
                $"No SerializedProperty '{property}' on {importer.GetType().Name}.",
                data: new { available = names });
        }
    }
}
