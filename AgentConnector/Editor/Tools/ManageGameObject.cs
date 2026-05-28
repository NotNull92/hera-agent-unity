using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "manage_gameobject",
        Description = "GameObject CRUD: create, destroy, move, set_parent, set_active, set_name, get_transform. Target by instance_id or hierarchy path.",
        Examples = new[]
        {
            "manage_gameobject create --name Player",
            "manage_gameobject create --name Cube --primitive cube --position 0,1,0",
            "manage_gameobject set_parent --instance_id 12345 --parent /Root",
            "manage_gameobject get_transform --path /Root/Player",
        },
        ExampleDescriptions = new[]
        {
            "Create an empty GameObject in the active scene",
            "Create a primitive cube at world (0,1,0)",
            "Reparent an object under /Root (worldPositionStays default true)",
            "Read position/rotation/scale of /Root/Player",
        })]
    public static class ManageGameObject
    {
        public class Parameters
        {
            [ToolParameter("Action: create, destroy, move, set_parent, set_active, set_name, get_transform", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Target by InstanceID (all actions except create)")]
            public int? InstanceId { get; set; }

            [ToolParameter("Target by hierarchy path '/Root/Child' (alternative to instance_id)")]
            public string Path { get; set; }

            [ToolParameter("Name for create / set_name")]
            public string Name { get; set; }

            [ToolParameter("Primitive for create: cube, sphere, capsule, cylinder, plane, quad. Omit for empty GameObject.")]
            public string Primitive { get; set; }

            [ToolParameter("Parent for create / set_parent: int instance_id or hierarchy path. Pass 'none' or empty to unparent (set_parent only).")]
            public string Parent { get; set; }

            [ToolParameter("Position for create / move: 'x,y,z' or JSON [x,y,z] / {\"x\":..,\"y\":..,\"z\":..}")]
            public string Position { get; set; }

            [ToolParameter("Coordinate space for move: 'world' (default) or 'local'")]
            public string Space { get; set; }

            [ToolParameter("Active state for set_active: true / false")]
            public bool? Active { get; set; }

            [ToolParameter("worldPositionStays for set_parent (default true)")]
            public bool? WorldPositionStays { get; set; }
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
                return new ErrorResponse("'action' required: create, destroy, move, set_parent, set_active, set_name, get_transform");
            action = action.ToLowerInvariant();

            switch (action)
            {
                case "create": return Create(p);
                case "destroy": return Destroy(p);
                case "move": return Move(p);
                case "set_parent": return SetParent(p);
                case "set_active": return SetActive(p);
                case "set_name": return SetName(p);
                case "get_transform": return GetTransform(p);
                default:
                    return new ErrorResponse(
                        $"Unknown manage_gameobject action: '{action}'. Use create, destroy, move, set_parent, set_active, set_name, get_transform.");
            }
        }

        // ---- sub-actions ----

        private static object Create(ToolParams p)
        {
            string name = p.Get("name");
            string primitive = p.Get("primitive");

            GameObject go;
            if (!string.IsNullOrEmpty(primitive))
            {
                if (!TryParsePrimitive(primitive, out var prim))
                    return new ErrorResponse($"Unknown primitive: '{primitive}'. Use cube, sphere, capsule, cylinder, plane, quad.");
                go = GameObject.CreatePrimitive(prim);
                if (!string.IsNullOrEmpty(name)) go.name = name;
            }
            else
            {
                go = new GameObject(string.IsNullOrEmpty(name) ? "GameObject" : name);
            }

            var parentToken = p.GetRaw("parent");
            if (parentToken != null && parentToken.Type != JTokenType.Null && !IsEmptyOrNoneString(parentToken))
            {
                var (parent, parentErr) = ResolveParent(parentToken);
                if (parentErr != null)
                {
                    Object.DestroyImmediate(go);
                    return new ErrorResponse(parentErr);
                }
                if (parent != null) go.transform.SetParent(parent.transform, true);
            }

            var posToken = p.GetRaw("position");
            if (posToken != null && posToken.Type != JTokenType.Null)
            {
                if (!TryParseVector3(posToken, out var pos, out var posErr))
                {
                    Object.DestroyImmediate(go);
                    return new ErrorResponse($"Invalid 'position': {posErr}");
                }
                go.transform.position = pos;
            }

            Undo.RegisterCreatedObjectUndo(go, $"Hera Create {go.name}");
            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse($"Created GameObject: {go.name}", BuildShallow(go));
        }

        private static object Destroy(ToolParams p)
        {
            var (go, err) = ResolveTarget(p);
            if (err != null) return new ErrorResponse(err);

            var snapshot = BuildShallow(go);
            var scene = go.scene;

            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Undo.DestroyObjectImmediate(go);

            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
            return new SuccessResponse("Destroyed GameObject.", snapshot);
        }

        private static object Move(ToolParams p)
        {
            var (go, err) = ResolveTarget(p);
            if (err != null) return new ErrorResponse(err);

            var posToken = p.GetRaw("position");
            if (posToken == null || posToken.Type == JTokenType.Null)
                return new ErrorResponse("'position' required for move.");
            if (!TryParseVector3(posToken, out var pos, out var posErr))
                return new ErrorResponse($"Invalid 'position': {posErr}");

            var space = (p.Get("space") ?? "world").ToLowerInvariant();
            Undo.RecordObject(go.transform, "Hera Move");
            switch (space)
            {
                case "world": go.transform.position = pos; break;
                case "local": go.transform.localPosition = pos; break;
                default: return new ErrorResponse($"Unknown space: '{space}'. Use 'world' or 'local'.");
            }
            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse($"Moved {go.name}.", BuildShallow(go));
        }

        private static object SetParent(ToolParams p)
        {
            var (go, err) = ResolveTarget(p);
            if (err != null) return new ErrorResponse(err);

            var parentToken = p.GetRaw("parent");
            bool unparent = parentToken == null
                || parentToken.Type == JTokenType.Null
                || IsEmptyOrNoneString(parentToken);

            Transform newParent = null;
            if (!unparent)
            {
                var (parent, parentErr) = ResolveParent(parentToken);
                if (parentErr != null) return new ErrorResponse(parentErr);
                if (parent == go) return new ErrorResponse("Cannot parent a GameObject to itself.");
                if (IsAncestor(go.transform, parent.transform))
                    return new ErrorResponse("Cannot create a parenting cycle (target is an ancestor of the requested parent).");
                newParent = parent.transform;
            }

            bool worldPositionStays = p.GetBool("world_position_stays", true);

            // Undo.SetTransformParent behaves like SetParent(_, worldPositionStays:true).
            // For worldPositionStays:false, snapshot the local transform before the
            // reparent and restore it after so the local values are preserved.
            var oldLocalPos = go.transform.localPosition;
            var oldLocalRot = go.transform.localRotation;
            var oldLocalScale = go.transform.localScale;

            Undo.SetTransformParent(go.transform, newParent, "Hera SetParent");

            if (!worldPositionStays)
            {
                Undo.RecordObject(go.transform, "Hera SetParent (preserve local)");
                go.transform.localPosition = oldLocalPos;
                go.transform.localRotation = oldLocalRot;
                go.transform.localScale = oldLocalScale;
            }

            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse(
                unparent ? $"Unparented {go.name}." : $"Reparented {go.name} -> {newParent.name}.",
                BuildShallow(go));
        }

        private static object SetActive(ToolParams p)
        {
            var (go, err) = ResolveTarget(p);
            if (err != null) return new ErrorResponse(err);

            var activeToken = p.GetRaw("active");
            if (activeToken == null || activeToken.Type == JTokenType.Null)
                return new ErrorResponse("'active' required for set_active (true/false).");
            var active = ParamCoercion.CoerceBoolNullable(activeToken);
            if (active == null)
                return new ErrorResponse($"Invalid 'active': '{activeToken}'. Use true/false.");

            Undo.RecordObject(go, "Hera SetActive");
            go.SetActive(active.Value);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse($"Set {go.name}.active = {active.Value}.", BuildShallow(go));
        }

        private static object SetName(ToolParams p)
        {
            var (go, err) = ResolveTarget(p);
            if (err != null) return new ErrorResponse(err);

            string name = p.Get("name");
            if (string.IsNullOrEmpty(name))
                return new ErrorResponse("'name' required for set_name.");

            Undo.RecordObject(go, "Hera SetName");
            string old = go.name;
            go.name = name;
            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse($"Renamed '{old}' -> '{name}'.", BuildShallow(go));
        }

        private static object GetTransform(ToolParams p)
        {
            var (go, err) = ResolveTarget(p);
            if (err != null) return new ErrorResponse(err);
            return new SuccessResponse("OK", BuildShallow(go));
        }

        // ---- helpers ----

        private static (GameObject go, string err) ResolveTarget(ToolParams p)
        {
            var idToken = p.GetRaw("instance_id");
            if (idToken != null && idToken.Type != JTokenType.Null)
            {
                int? id = p.GetInt("instance_id");
                if (id == null) return (null, $"Invalid 'instance_id': '{idToken}'.");
                var obj = EditorUtility.InstanceIDToObject(id.Value);
                if (obj == null) return (null, $"No object found for instance_id={id.Value}.");
                GameObject go = obj as GameObject;
                if (go == null && obj is Component c) go = c.gameObject;
                if (go == null) return (null, $"instance_id={id.Value} is not a GameObject (type={obj.GetType().Name}).");
                return (go, null);
            }

            string path = p.Get("path") ?? p.Get("target");
            if (!string.IsNullOrEmpty(path))
            {
                var go = ResolveByPath(path);
                if (go == null) return (null, $"No GameObject at path: '{path}'.");
                return (go, null);
            }

            return (null, "Target required: pass 'instance_id' or 'path'.");
        }

        private static (GameObject go, string err) ResolveParent(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return (null, null);

            if (token.Type == JTokenType.Integer)
            {
                int id = token.Value<int>();
                var obj = EditorUtility.InstanceIDToObject(id);
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go == null) return (null, $"No GameObject for parent instance_id={id}.");
                return (go, null);
            }

            var s = token.ToString();
            if (string.IsNullOrEmpty(s)) return (null, null);
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
            {
                var obj = EditorUtility.InstanceIDToObject(parsedId);
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go == null) return (null, $"No GameObject for parent instance_id={parsedId}.");
                return (go, null);
            }

            var byPath = ResolveByPath(s);
            if (byPath == null) return (null, $"No GameObject for parent path: '{s}'.");
            return (byPath, null);
        }

        // GameObject.Find covers active objects across loaded scenes via "/Root/Child"
        // syntax. The fallback walk also matches inactive roots/children that Find
        // would skip — important because reparenting / set_active operates on
        // inactive subtrees too.
        private static GameObject ResolveByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var found = GameObject.Find(path);
            if (found != null) return found;

            string trimmed = path.TrimStart('/');
            if (string.IsNullOrEmpty(trimmed)) return null;
            var segments = trimmed.Split('/');

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name != segments[0]) continue;
                    var t = WalkPath(root.transform, segments, 1);
                    if (t != null) return t.gameObject;
                }
            }
            return null;
        }

        private static Transform WalkPath(Transform t, string[] segments, int index)
        {
            if (index >= segments.Length) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name != segments[index]) continue;
                var match = WalkPath(c, segments, index + 1);
                if (match != null) return match;
            }
            return null;
        }

        private static bool IsAncestor(Transform potentialAncestor, Transform t)
        {
            var cursor = t;
            while (cursor != null)
            {
                if (cursor == potentialAncestor) return true;
                cursor = cursor.parent;
            }
            return false;
        }

        private static bool TryParsePrimitive(string s, out PrimitiveType prim)
        {
            switch (s.ToLowerInvariant())
            {
                case "cube": prim = PrimitiveType.Cube; return true;
                case "sphere": prim = PrimitiveType.Sphere; return true;
                case "capsule": prim = PrimitiveType.Capsule; return true;
                case "cylinder": prim = PrimitiveType.Cylinder; return true;
                case "plane": prim = PrimitiveType.Plane; return true;
                case "quad": prim = PrimitiveType.Quad; return true;
                default: prim = default; return false;
            }
        }

        private static bool TryParseVector3(JToken token, out Vector3 v, out string err)
        {
            v = default;
            err = null;
            if (token == null || token.Type == JTokenType.Null)
            {
                err = "null token";
                return false;
            }

            if (token is JArray arr)
            {
                if (arr.Count != 3)
                {
                    err = $"array length must be 3 (got {arr.Count})";
                    return false;
                }
                try
                {
                    v = new Vector3(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>());
                    return true;
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                    return false;
                }
            }

            if (token is JObject obj)
            {
                try
                {
                    float x = obj["x"]?.Value<float>() ?? 0f;
                    float y = obj["y"]?.Value<float>() ?? 0f;
                    float z = obj["z"]?.Value<float>() ?? 0f;
                    v = new Vector3(x, y, z);
                    return true;
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                    return false;
                }
            }

            var s = token.ToString();
            var parts = s.Split(',');
            if (parts.Length != 3)
            {
                err = $"expected 3 comma-separated values, got {parts.Length}";
                return false;
            }
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float xf) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float yf) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float zf))
            {
                err = "failed to parse floats";
                return false;
            }
            v = new Vector3(xf, yf, zf);
            return true;
        }

        private static bool IsEmptyOrNoneString(JToken token)
        {
            if (token.Type != JTokenType.String) return false;
            var s = token.ToString();
            return string.IsNullOrEmpty(s) || s.Equals("none", StringComparison.OrdinalIgnoreCase);
        }

        private static object BuildShallow(GameObject go)
        {
            var t = go.transform;
            return new
            {
                instance_id = go.GetInstanceID(),
                name = go.name,
                path = GetHierarchyPath(t),
                scene = go.scene.name,
                scene_path = go.scene.path,
                active = go.activeInHierarchy,
                transform = new
                {
                    position = new { x = t.position.x, y = t.position.y, z = t.position.z },
                    rotation = new { x = t.eulerAngles.x, y = t.eulerAngles.y, z = t.eulerAngles.z },
                    scale = new { x = t.localScale.x, y = t.localScale.y, z = t.localScale.z },
                },
            };
        }

        private static string GetHierarchyPath(Transform t)
        {
            var stack = new Stack<string>();
            var cursor = t;
            while (cursor != null)
            {
                stack.Push(cursor.name);
                cursor = cursor.parent;
            }
            return "/" + string.Join("/", stack);
        }
    }
}
