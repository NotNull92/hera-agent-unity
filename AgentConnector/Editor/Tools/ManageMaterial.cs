using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "manage_material",
        Description = "Material asset CRUD: create (with a shader), get (shader + property values), set (one shader property), set_shader (swap the shader). Property names are shader property names (_BaseColor, _Metallic, _MainTex) — run describe_shader first to discover them. Values reuse the manage_components forms: '1,0,0,1' or '#RRGGBB' for colors, a number for floats, 'x,y,z,w' for vectors, and an asset path or InstanceID for textures.",
        Examples = new[]
        {
            "manage_material create --path Assets/Mats/Player.mat --shader \"Universal Render Pipeline/Lit\"",
            "manage_material get --path Assets/Mats/Player.mat",
            "manage_material set --path Assets/Mats/Player.mat --property _BaseColor --value 1,0,0,1",
            "manage_material set --path Assets/Mats/Player.mat --property _MainTex --value Assets/Tex/skin.png",
            "manage_material set_shader --path Assets/Mats/Player.mat --shader Unlit/Color",
        },
        ExampleDescriptions = new[]
        {
            "Create a material asset bound to a shader",
            "Dump the material's shader and every property value",
            "Set a color property (also accepts #hex or [r,g,b,a])",
            "Set a texture property by asset path (or InstanceID)",
            "Swap the material's shader, keeping matching property values",
        })]
    public static class ManageMaterial
    {
        public class Parameters
        {
            [ToolParameter("Action: create, get, set, set_shader.", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Material asset path (Assets/.../Name.mat).", Required = true)]
            public string Path { get; set; }

            [ToolParameter("Shader name. Required for create and set_shader (e.g. 'Universal Render Pipeline/Lit').")]
            public string Shader { get; set; }

            [ToolParameter("Shader property name for set / single get (e.g. _BaseColor, _Metallic, _MainTex).")]
            public string Property { get; set; }

            [ToolParameter("Value for set. Color: '1,0,0,1' or '#RRGGBB'. Float: a number. Vector: 'x,y,z,w'. Texture: asset path or InstanceID.")]
            public string Value { get; set; }

            [ToolParameter("get (all properties): max properties returned. Default 60.")]
            public int Limit { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var action = (p.GetRaw("args") as JArray)?[0]?.ToString() ?? p.Get("action");
            if (string.IsNullOrWhiteSpace(action))
                return new ErrorResponse("MISSING_PARAM", "'action' required: create, get, set, or set_shader.");

            var path = p.Get("path");
            if (string.IsNullOrWhiteSpace(path))
                return new ErrorResponse("MISSING_PARAM", "'path' required (the material asset path, e.g. Assets/Mats/X.mat).");
            if (!AssetPathGuard.TryNormalizeAssetFile(path, out path, out var pathErr))
                return new ErrorResponse("INVALID_PATH", pathErr);

            switch (action.ToLowerInvariant())
            {
                case "create": return Create(path, p.Get("shader"));
                case "get": return Get(path, p.Get("property"), p.GetInt("limit") ?? 60);
                case "set": return Set(path, p.Get("property"), p.GetRaw("value"));
                case "set_shader": return SetShader(path, p.Get("shader"));
                default:
                    return new ErrorResponse("UNKNOWN_ACTION",
                        $"Unknown action '{action}'. Valid: create, get, set, set_shader.");
            }
        }

        private static object Create(string path, string shaderName)
        {
            if (!AssetPathGuard.TryPrepareNewAssetFile(
                    path, ".mat", appendExtension: false,
                    out path, out var pathCode, out var pathErr))
                return new ErrorResponse(pathCode, pathErr);
            if (string.IsNullOrWhiteSpace(shaderName))
                return new ErrorResponse("MISSING_PARAM", "'shader' required for create. Run describe_shader --list to find one.");
            var shader = Shader.Find(shaderName);
            if (shader == null)
                return new ErrorResponse("SHADER_NOT_FOUND",
                    $"No shader named '{shaderName}'.",
                    suggestions: new List<string> { $"describe_shader --list --filter {shaderName}" });

            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Created material at {path}", new
            {
                path,
                shader = shader.name,
                property_count = shader.GetPropertyCount(),
            });
        }

        private static object Get(string path, string property, int limit)
        {
            if (limit <= 0) limit = 60;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
                return new ErrorResponse("MATERIAL_NOT_FOUND", $"No material asset at '{path}'.");
            var shader = mat.shader;

            if (!string.IsNullOrWhiteSpace(property))
            {
                if (!mat.HasProperty(property))
                    return PropertyNotFound(mat, property);
                return new SuccessResponse($"{mat.name}.{property}", new
                {
                    path,
                    shader = shader.name,
                    name = property,
                    type = PropType(shader, property)?.ToString(),
                    value = ReadValue(mat, property, PropType(shader, property)),
                });
            }

            var count = shader.GetPropertyCount();
            var shown = count < limit ? count : limit;
            var props = new List<object>(shown);
            for (var i = 0; i < shown; i++)
            {
                var name = shader.GetPropertyName(i);
                var type = shader.GetPropertyType(i);
                props.Add(new
                {
                    name,
                    type = type.ToString(),
                    value = ReadValue(mat, name, type),
                });
            }

            return new SuccessResponse($"{mat.name} ({shader.name}, {count} properties)", new
            {
                path,
                shader = shader.name,
                property_count = count,
                truncated = count > shown,
                properties = props,
            });
        }

        private static object Set(string path, string property, JToken value)
        {
            if (string.IsNullOrWhiteSpace(property))
                return new ErrorResponse("MISSING_PARAM", "'property' required for set (e.g. _BaseColor).");
            if (value == null)
                return new ErrorResponse("MISSING_PARAM", "'value' required for set.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
                return new ErrorResponse("MATERIAL_NOT_FOUND", $"No material asset at '{path}'.");
            if (!mat.HasProperty(property))
                return PropertyNotFound(mat, property);

            var type = PropType(mat.shader, property);
            var (ok, err) = ApplyValue(mat, property, type, value);
            if (!ok)
                return new ErrorResponse("VALUE_PARSE_ERROR", $"Could not set {property}: {err}");

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Set {mat.name}.{property}", new
            {
                path,
                name = property,
                type = type?.ToString(),
                value = ReadValue(mat, property, type),
            });
        }

        private static object SetShader(string path, string shaderName)
        {
            if (string.IsNullOrWhiteSpace(shaderName))
                return new ErrorResponse("MISSING_PARAM", "'shader' required for set_shader.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
                return new ErrorResponse("MATERIAL_NOT_FOUND", $"No material asset at '{path}'.");
            var shader = Shader.Find(shaderName);
            if (shader == null)
                return new ErrorResponse("SHADER_NOT_FOUND",
                    $"No shader named '{shaderName}'.",
                    suggestions: new List<string> { $"describe_shader --list --filter {shaderName}" });

            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            return new SuccessResponse($"Set {mat.name} shader to {shader.name}", new
            {
                path,
                shader = shader.name,
                property_count = shader.GetPropertyCount(),
            });
        }

        // ---- helpers ----

        private static ShaderPropertyType? PropType(Shader shader, string property)
        {
            var count = shader.GetPropertyCount();
            for (var i = 0; i < count; i++)
                if (shader.GetPropertyName(i) == property)
                    return shader.GetPropertyType(i);
            return null;
        }

        private static object ReadValue(Material mat, string property, ShaderPropertyType? type)
        {
            switch (type)
            {
                case ShaderPropertyType.Color:
                    var c = mat.GetColor(property);
                    return new { r = c.r, g = c.g, b = c.b, a = c.a };
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    return mat.GetFloat(property);
                case ShaderPropertyType.Vector:
                    var v = mat.GetVector(property);
                    return new { x = v.x, y = v.y, z = v.z, w = v.w };
                case ShaderPropertyType.Texture:
                    var tex = mat.GetTexture(property);
                    return tex == null ? null : (object)new { instance_id = EntityIdCompat.IdOf(tex), name = tex.name, type = tex.GetType().Name };
                case ShaderPropertyType.Int:
                    return mat.GetInteger(property);
                default:
                    return null;
            }
        }

        private static (bool ok, string err) ApplyValue(Material mat, string property, ShaderPropertyType? type, JToken value)
        {
            switch (type)
            {
                case ShaderPropertyType.Color:
                    if (!SerializedPropertyValue.TryParseColor(value, out var col, out var colErr)) return (false, colErr);
                    mat.SetColor(property, col);
                    return (true, null);
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    mat.SetFloat(property, value.Value<float>());
                    return (true, null);
                case ShaderPropertyType.Vector:
                    if (!SerializedPropertyValue.TryParseFloats(value, 4, out var vec, out var vecErr)) return (false, vecErr);
                    mat.SetVector(property, new Vector4(vec[0], vec[1], vec[2], vec[3]));
                    return (true, null);
                case ShaderPropertyType.Texture:
                    var (obj, refErr) = SerializedPropertyValue.ResolveReference(value);
                    if (refErr != null) return (false, refErr);
                    var tex = obj as Texture;
                    if (obj != null && tex == null) return (false, $"asset is {obj.GetType().Name}, not a Texture");
                    mat.SetTexture(property, tex);
                    return (true, null);
                case ShaderPropertyType.Int:
                    mat.SetInteger(property, value.Value<int>());
                    return (true, null);
                default:
                    return (false, $"unsupported property type: {type}");
            }
        }

        private static ErrorResponse PropertyNotFound(Material mat, string property)
        {
            return new ErrorResponse("SHADER_PROPERTY_NOT_FOUND",
                $"Shader '{mat.shader.name}' has no property '{property}'.",
                suggestions: new List<string> { $"describe_shader \"{mat.shader.name}\"" });
        }
    }
}
