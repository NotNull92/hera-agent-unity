using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
// `using UnityEditor;` brings the legacy AssetStore PackageInfo type but more
// importantly leaves `Object` ambiguous once `using System;` joins it; alias
// to the engine type so bare `Object` here resolves unambiguously.
using Object = UnityEngine.Object;

namespace HeraAgent
{
    /// <summary>
    /// JSON ↔ SerializedProperty bridge for manage_components and any future
    /// tool that wants to read or set typed Unity object properties without
    /// hand-rolling per-type code paths.
    ///
    /// Read returns a shape suitable for direct JSON serialization. Apply
    /// accepts the same shapes the user would naturally write — scalars,
    /// arrays for vectors, objects with x/y/z keys, enum names or indices,
    /// and either an InstanceID, an asset path, or a {instance_id|asset_path}
    /// envelope for ObjectReference fields.
    /// </summary>
    public static class SerializedPropertyValue
    {
        public static object Read(SerializedProperty p)
        {
            if (p == null) return null;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.LayerMask:
                    return p.intValue;
                case SerializedPropertyType.Boolean:
                    return p.boolValue;
                case SerializedPropertyType.Float:
                    return p.floatValue;
                case SerializedPropertyType.String:
                    return p.stringValue;
                case SerializedPropertyType.Character:
                    return ((char)p.intValue).ToString();
                case SerializedPropertyType.Color:
                    var c = p.colorValue;
                    return new { r = c.r, g = c.g, b = c.b, a = c.a };
                case SerializedPropertyType.Vector2:
                    return new { x = p.vector2Value.x, y = p.vector2Value.y };
                case SerializedPropertyType.Vector3:
                    return new { x = p.vector3Value.x, y = p.vector3Value.y, z = p.vector3Value.z };
                case SerializedPropertyType.Vector4:
                    return new { x = p.vector4Value.x, y = p.vector4Value.y, z = p.vector4Value.z, w = p.vector4Value.w };
                case SerializedPropertyType.Vector2Int:
                    return new { x = p.vector2IntValue.x, y = p.vector2IntValue.y };
                case SerializedPropertyType.Vector3Int:
                    return new { x = p.vector3IntValue.x, y = p.vector3IntValue.y, z = p.vector3IntValue.z };
                case SerializedPropertyType.Quaternion:
                    return new { x = p.quaternionValue.x, y = p.quaternionValue.y, z = p.quaternionValue.z, w = p.quaternionValue.w };
                case SerializedPropertyType.Rect:
                    var r = p.rectValue;
                    return new { x = r.x, y = r.y, width = r.width, height = r.height };
                case SerializedPropertyType.Bounds:
                    var b = p.boundsValue;
                    return new
                    {
                        center = new { x = b.center.x, y = b.center.y, z = b.center.z },
                        extents = new { x = b.extents.x, y = b.extents.y, z = b.extents.z },
                    };
                case SerializedPropertyType.Enum:
                    int idx = p.enumValueIndex;
                    var names = p.enumDisplayNames;
                    string name = idx >= 0 && idx < names.Length ? names[idx] : null;
                    return new { value = idx, name };
                case SerializedPropertyType.ObjectReference:
                    var obj = p.objectReferenceValue;
                    if (obj == null) return null;
                    return new
                    {
                        instance_id = EntityIdCompat.IdOf(obj),
                        type = obj.GetType().Name,
                        name = obj.name,
                    };
                default:
                    return new { unsupported_property_type = p.propertyType.ToString() };
            }
        }

        public static (bool ok, string err) Apply(SerializedProperty p, JToken token)
        {
            if (p == null) return (false, "property is null");
            if (token == null) return (false, "value token is null");

            try
            {
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.LayerMask:
                        p.intValue = token.Value<int>();
                        return (true, null);

                    case SerializedPropertyType.Boolean:
                        var b = ParamCoercion.CoerceBoolNullable(token);
                        if (b == null) return (false, $"invalid bool value: '{token}'");
                        p.boolValue = b.Value;
                        return (true, null);

                    case SerializedPropertyType.Float:
                        p.floatValue = token.Value<float>();
                        return (true, null);

                    case SerializedPropertyType.String:
                        p.stringValue = token.ToString();
                        return (true, null);

                    case SerializedPropertyType.Character:
                        var s = token.ToString();
                        if (string.IsNullOrEmpty(s)) return (false, "character value cannot be empty");
                        p.intValue = s[0];
                        return (true, null);

                    case SerializedPropertyType.Color:
                        if (!TryParseColor(token, out var col, out var colErr)) return (false, colErr);
                        p.colorValue = col;
                        return (true, null);

                    case SerializedPropertyType.Vector2:
                        if (!TryParseFloats(token, 2, out var v2, out var v2Err)) return (false, v2Err);
                        p.vector2Value = new Vector2(v2[0], v2[1]);
                        return (true, null);

                    case SerializedPropertyType.Vector3:
                        if (!TryParseFloats(token, 3, out var v3, out var v3Err)) return (false, v3Err);
                        p.vector3Value = new Vector3(v3[0], v3[1], v3[2]);
                        return (true, null);

                    case SerializedPropertyType.Vector4:
                        if (!TryParseFloats(token, 4, out var v4, out var v4Err)) return (false, v4Err);
                        p.vector4Value = new Vector4(v4[0], v4[1], v4[2], v4[3]);
                        return (true, null);

                    case SerializedPropertyType.Quaternion:
                        if (!TryParseFloats(token, 4, out var q, out var qErr)) return (false, qErr);
                        p.quaternionValue = new Quaternion(q[0], q[1], q[2], q[3]);
                        return (true, null);

                    case SerializedPropertyType.Vector2Int:
                        if (!TryParseInts(token, 2, out var v2i, out var v2iErr)) return (false, v2iErr);
                        p.vector2IntValue = new Vector2Int(v2i[0], v2i[1]);
                        return (true, null);

                    case SerializedPropertyType.Vector3Int:
                        if (!TryParseInts(token, 3, out var v3i, out var v3iErr)) return (false, v3iErr);
                        p.vector3IntValue = new Vector3Int(v3i[0], v3i[1], v3i[2]);
                        return (true, null);

                    case SerializedPropertyType.Enum:
                        if (token.Type == JTokenType.Integer)
                        {
                            p.enumValueIndex = token.Value<int>();
                            return (true, null);
                        }
                        var enumName = token.ToString();
                        var names = p.enumDisplayNames;
                        for (int i = 0; i < names.Length; i++)
                        {
                            if (string.Equals(names[i], enumName, StringComparison.OrdinalIgnoreCase))
                            {
                                p.enumValueIndex = i;
                                return (true, null);
                            }
                        }
                        return (false, $"unknown enum value '{enumName}'. Valid: [{string.Join(", ", names)}]");

                    case SerializedPropertyType.ObjectReference:
                        var (obj, refErr) = ResolveReference(token);
                        if (refErr != null) return (false, refErr);
                        p.objectReferenceValue = obj;
                        return (true, null);

                    default:
                        return (false, $"unsupported property type: {p.propertyType}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"failed to apply value: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves a JSON token into a Unity Object reference. Accepted forms:
        /// <list type="bullet">
        /// <item>JSON null → unset reference</item>
        /// <item>integer → InstanceID</item>
        /// <item>string of digits → InstanceID; otherwise treated as an asset path (Assets/...)</item>
        /// <item>object {"instance_id": N} or {"asset_path": "..."}</item>
        /// </list>
        /// </summary>
        public static (Object obj, string err) ResolveReference(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return (null, null);

            if (token.Type == JTokenType.Integer)
            {
                var obj = EntityIdCompat.ToObject(token.Value<int>());
                return obj == null ? (null, $"no object for instance_id={token.Value<int>()}") : (obj, null);
            }

            if (token.Type == JTokenType.String)
            {
                var s = token.ToString();
                if (string.IsNullOrEmpty(s)) return (null, null);
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
                {
                    var obj = EntityIdCompat.ToObject(parsedId);
                    return obj == null ? (null, $"no object for instance_id={parsedId}") : (obj, null);
                }
                var asset = AssetDatabase.LoadAssetAtPath<Object>(s);
                return asset == null ? (null, $"no asset at path: '{s}'") : (asset, null);
            }

            if (token is JObject jo)
            {
                if (jo["instance_id"] != null)
                {
                    int id = jo["instance_id"].Value<int>();
                    var obj = EntityIdCompat.ToObject(id);
                    return obj == null ? (null, $"no object for instance_id={id}") : (obj, null);
                }
                if (jo["asset_path"] != null)
                {
                    var path = jo["asset_path"].ToString();
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                    return asset == null ? (null, $"no asset at path: '{path}'") : (asset, null);
                }
                return (null, "reference object needs 'instance_id' or 'asset_path' key");
            }

            return (null, $"unsupported reference token type: {token.Type}");
        }

        // ---- value parsers ----

        // public so value-typed tools that don't go through a SerializedProperty
        // (e.g. manage_material setting Material.SetVector/SetColor by shader
        // property type) can reuse the exact same parse forms — array, {x,y,z},
        // and comma string — instead of re-implementing them.
        public static bool TryParseFloats(JToken token, int expected, out float[] result, out string err)
        {
            result = null; err = null;

            if (token is JArray arr)
            {
                if (arr.Count != expected) { err = $"array length must be {expected} (got {arr.Count})"; return false; }
                result = new float[expected];
                for (int i = 0; i < expected; i++) result[i] = arr[i].Value<float>();
                return true;
            }

            if (token is JObject obj)
            {
                result = new float[expected];
                string[] keys = { "x", "y", "z", "w" };
                for (int i = 0; i < expected; i++)
                {
                    var v = obj[keys[i]];
                    result[i] = v != null ? v.Value<float>() : 0f;
                }
                return true;
            }

            // String like "1,2,3"
            var s = token.ToString();
            var parts = s.Split(',');
            if (parts.Length != expected) { err = $"expected {expected} comma-separated values, got {parts.Length}"; return false; }
            result = new float[expected];
            for (int i = 0; i < expected; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
                {
                    err = $"failed to parse float at index {i}: '{parts[i]}'";
                    return false;
                }
            }
            return true;
        }

        static bool TryParseInts(JToken token, int expected, out int[] result, out string err)
        {
            result = null; err = null;

            if (token is JArray arr)
            {
                if (arr.Count != expected) { err = $"array length must be {expected} (got {arr.Count})"; return false; }
                result = new int[expected];
                for (int i = 0; i < expected; i++) result[i] = arr[i].Value<int>();
                return true;
            }

            if (token is JObject obj)
            {
                result = new int[expected];
                string[] keys = { "x", "y", "z", "w" };
                for (int i = 0; i < expected; i++)
                {
                    var v = obj[keys[i]];
                    result[i] = v != null ? v.Value<int>() : 0;
                }
                return true;
            }

            var s = token.ToString();
            var parts = s.Split(',');
            if (parts.Length != expected) { err = $"expected {expected} comma-separated values, got {parts.Length}"; return false; }
            result = new int[expected];
            for (int i = 0; i < expected; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i]))
                {
                    err = $"failed to parse int at index {i}: '{parts[i]}'";
                    return false;
                }
            }
            return true;
        }

        public static bool TryParseColor(JToken token, out Color result, out string err)
        {
            result = default; err = null;

            // [r,g,b,a] or [r,g,b]
            if (token is JArray arr)
            {
                if (arr.Count != 3 && arr.Count != 4) { err = $"color array length must be 3 or 4 (got {arr.Count})"; return false; }
                result = new Color(
                    arr[0].Value<float>(),
                    arr[1].Value<float>(),
                    arr[2].Value<float>(),
                    arr.Count == 4 ? arr[3].Value<float>() : 1f);
                return true;
            }

            // {r,g,b,a}
            if (token is JObject obj)
            {
                float r = obj["r"]?.Value<float>() ?? 0f;
                float g = obj["g"]?.Value<float>() ?? 0f;
                float bb = obj["b"]?.Value<float>() ?? 0f;
                float a = obj["a"]?.Value<float>() ?? 1f;
                result = new Color(r, g, bb, a);
                return true;
            }

            // "#RRGGBB" or "#RRGGBBAA"
            var s = token.ToString();
            if (ColorUtility.TryParseHtmlString(s, out result)) return true;

            // "r,g,b" or "r,g,b,a"
            var parts = s.Split(',');
            if (parts.Length != 3 && parts.Length != 4) { err = $"expected 3 or 4 comma-separated values or #hex, got '{s}'"; return false; }
            float[] vals = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out vals[i]))
                {
                    err = $"failed to parse color component at index {i}: '{parts[i]}'";
                    return false;
                }
            }
            result = parts.Length == 4
                ? new Color(vals[0], vals[1], vals[2], vals[3])
                : new Color(vals[0], vals[1], vals[2], 1f);
            return true;
        }
    }
}
