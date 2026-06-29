using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "manage_gameobject",
        Description = "GameObject CRUD: create, destroy, duplicate, move, set_parent, set_active, set_name, get_transform. Target by instance_id or hierarchy path.",
        Examples = new[]
        {
            "manage_gameobject create --name Player",
            "manage_gameobject create --name Cube --primitive cube --position 0,1,0",
            "manage_gameobject duplicate --path /Enemies/Goblin --count 5 --name Goblin",
            "manage_gameobject set_parent --instance_id 12345 --parent /Root",
            "manage_gameobject get_transform --path /Root/Player",
        },
        ExampleDescriptions = new[]
        {
            "Create an empty GameObject in the active scene",
            "Create a primitive cube at world (0,1,0)",
            "Duplicate 5x (Editor-fidelity: keeps prefab link + overrides)",
            "Reparent an object under /Root (worldPositionStays default true)",
            "Read position/rotation/scale of /Root/Player",
        })]
    public static class ManageGameObject
    {
        public class Parameters
        {
            [ToolParameter("Action: create, destroy, duplicate, move, set_parent, set_active, set_name, get_transform", Required = true)]
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

            [ToolParameter("Number of copies for duplicate (default 1, max 100)")]
            public int? Count { get; set; }
        }

        // ---- sub-actions ----

        [HeraAction]
        public static object Create(JObject raw)
        {
            var p = new ToolParams(raw);
            string name = p.Get("name");
            string primitive = p.Get("primitive");

            GameObject go;
            if (!string.IsNullOrEmpty(primitive))
            {
                if (!TryParsePrimitive(primitive, out var prim))
                    return new ErrorResponse("UNKNOWN_PRIMITIVE", $"Unknown primitive: '{primitive}'. Use cube, sphere, capsule, cylinder, plane, quad.");
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
                    UnityEngine.Object.DestroyImmediate(go);
                    return parentErr;
                }
                if (parent != null) go.transform.SetParent(parent.transform, true);
            }

            var posToken = p.GetRaw("position");
            if (posToken != null && posToken.Type != JTokenType.Null)
            {
                if (!TryParseVector3(posToken, out var pos, out var posErr))
                {
                    UnityEngine.Object.DestroyImmediate(go);
                    return new ErrorResponse("INVALID_PARAM", $"Invalid 'position': {posErr}");
                }
                go.transform.position = pos;
            }

            Undo.RegisterCreatedObjectUndo(go, $"Hera Create {go.name}");
            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse($"Created GameObject: {go.name}", BuildShallow(go));
        }

        [HeraAction]
        public static object Duplicate(JObject raw)
        {
            var p = new ToolParams(raw);
            var (src, err) = TargetResolver.ResolveGameObject(p, altPathKey: "target");
            if (err != null) return err;

            int count = p.GetInt("count") ?? 1;
            if (count < 1)
                return new ErrorResponse("INVALID_PARAM", "'count' must be >= 1.");
            if (count > 100)
                return new ErrorResponse("INVALID_PARAM", "'count' is capped at 100 per call.");

            string newName = p.Get("name");

            // Optional reparent target for the clones.
            var parentToken = p.GetRaw("parent");
            bool hasParentOverride = parentToken != null
                && parentToken.Type != JTokenType.Null
                && !IsEmptyOrNoneString(parentToken);
            Transform overrideParent = null;
            if (hasParentOverride)
            {
                var (parent, parentErr) = ResolveParent(parentToken);
                if (parentErr != null) return parentErr;
                overrideParent = parent != null ? parent.transform : null;
            }

            // Use Unity's own duplicate (the Ctrl+D path) so prefab connections,
            // property overrides and nested children survive — Object.Instantiate
            // would produce a disconnected copy instead. It drives the editor
            // Selection and clobbers the copy/paste buffer, so snapshot and
            // restore the prior selection around the loop.
            var scene = src.scene;
            int srcRawId = src.GetInstanceID();
            var prevSelection = Selection.objects;
            int undoGroup = Undo.GetCurrentGroup();
            var clones = new List<object>();
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Selection.objects = new UnityEngine.Object[] { src };
                    Unsupported.DuplicateGameObjectsUsingPasteboard();

                    var clone = Selection.activeGameObject;
                    if (clone == null || clone.GetInstanceID() == srcRawId)
                        return new ErrorResponse("DUPLICATE_FAILED",
                            "[Hera] I asked Unity to duplicate the object but no new GameObject appeared.");

                    if (hasParentOverride)
                        Undo.SetTransformParent(clone.transform, overrideParent, "Hera Duplicate (reparent)");

                    if (!string.IsNullOrEmpty(newName))
                    {
                        Undo.RecordObject(clone, "Hera Duplicate (rename)");
                        clone.name = count > 1 ? $"{newName} ({i + 1})" : newName;
                    }

                    clones.Add(new
                    {
                        instance_id = EntityIdCompat.IdOf(clone),
                        name = clone.name,
                        path = HierarchyPath.Build(clone.transform),
                    });
                }
            }
            finally
            {
                Selection.objects = prevSelection;
            }

            // Collapse duplicate + rename + reparent into a single Undo step.
            Undo.CollapseUndoOperations(undoGroup);
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);

            return new SuccessResponse($"Duplicated {src.name} x{clones.Count}.", new
            {
                source = new { instance_id = EntityIdCompat.IdOf(src), name = src.name },
                count = clones.Count,
                clones,
            });
        }

        [HeraAction]
        public static object Destroy(JObject raw)
        {
            var p = new ToolParams(raw);
            var (go, err) = TargetResolver.ResolveGameObject(p, altPathKey: "target");
            if (err != null) return err;

            var snapshot = BuildShallow(go);
            var scene = go.scene;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                Undo.DestroyObjectImmediate(go);

            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
            return new SuccessResponse("Destroyed GameObject.", snapshot);
        }

        [HeraAction]
        public static object Move(JObject raw)
        {
            var p = new ToolParams(raw);
            var (go, err) = TargetResolver.ResolveGameObject(p, altPathKey: "target");
            if (err != null) return err;

            var posToken = p.GetRaw("position");
            if (posToken == null || posToken.Type == JTokenType.Null)
                return new ErrorResponse("MISSING_PARAM", "'position' required for move.");
            if (!TryParseVector3(posToken, out var pos, out var posErr))
                return new ErrorResponse("INVALID_PARAM", $"Invalid 'position': {posErr}");

            var space = (p.Get("space") ?? "world").ToLowerInvariant();
            Undo.RecordObject(go.transform, "Hera Move");
            switch (space)
            {
                case "world": go.transform.position = pos; break;
                case "local": go.transform.localPosition = pos; break;
                default: return new ErrorResponse("INVALID_PARAM", $"Unknown space: '{space}'. Use 'world' or 'local'.");
            }
            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse($"Moved {go.name}.", BuildShallow(go));
        }

        [HeraAction]
        public static object SetParent(JObject raw)
        {
            var p = new ToolParams(raw);
            var (go, err) = TargetResolver.ResolveGameObject(p, altPathKey: "target");
            if (err != null) return err;

            var parentToken = p.GetRaw("parent");
            bool unparent = parentToken == null
                || parentToken.Type == JTokenType.Null
                || IsEmptyOrNoneString(parentToken);

            Transform newParent = null;
            if (!unparent)
            {
                var (parent, parentErr) = ResolveParent(parentToken);
                if (parentErr != null) return parentErr;
                if (parent == go) return new ErrorResponse("PARENTING_SELF", "Cannot parent a GameObject to itself.");
                if (IsAncestor(go.transform, parent.transform))
                    return new ErrorResponse("PARENTING_CYCLE", "Cannot create a parenting cycle (target is an ancestor of the requested parent).");
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

        [HeraAction]
        public static object SetActive(JObject raw)
        {
            var p = new ToolParams(raw);
            var (go, err) = TargetResolver.ResolveGameObject(p, altPathKey: "target");
            if (err != null) return err;

            var activeToken = p.GetRaw("active");
            if (activeToken == null || activeToken.Type == JTokenType.Null)
                return new ErrorResponse("MISSING_PARAM", "'active' required for set_active (true/false).");
            var active = ParamCoercion.CoerceBoolNullable(activeToken);
            if (active == null)
                return new ErrorResponse("INVALID_PARAM", $"Invalid 'active': '{activeToken}'. Use true/false.");

            Undo.RecordObject(go, "Hera SetActive");
            go.SetActive(active.Value);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse($"Set {go.name}.active = {active.Value}.", BuildShallow(go));
        }

        [HeraAction]
        public static object SetName(JObject raw)
        {
            var p = new ToolParams(raw);
            var (go, err) = TargetResolver.ResolveGameObject(p, altPathKey: "target");
            if (err != null) return err;

            string name = p.Get("name");
            if (string.IsNullOrEmpty(name))
                return new ErrorResponse("MISSING_PARAM", "'name' required for set_name.");

            Undo.RecordObject(go, "Hera SetName");
            string old = go.name;
            go.name = name;
            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse($"Renamed '{old}' -> '{name}'.", BuildShallow(go));
        }

        [HeraAction]
        public static object GetTransform(JObject raw)
        {
            var p = new ToolParams(raw);
            var (go, err) = TargetResolver.ResolveGameObject(p, altPathKey: "target");
            if (err != null) return err;
            return new SuccessResponse("OK", BuildShallow(go));
        }

        // ---- helpers ----

        private static (GameObject go, ErrorResponse err) ResolveParent(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return (null, null);

            if (token.Type == JTokenType.Integer)
            {
                int id = token.Value<int>();
                var obj = EntityIdCompat.ToObject(id);
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go == null) return (null, new ErrorResponse("OBJECT_NOT_FOUND", $"No GameObject for parent instance_id={id}."));
                return (go, null);
            }

            var s = token.ToString();
            if (string.IsNullOrEmpty(s)) return (null, null);
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
            {
                var obj = EntityIdCompat.ToObject(parsedId);
                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go == null) return (null, new ErrorResponse("OBJECT_NOT_FOUND", $"No GameObject for parent instance_id={parsedId}."));
                return (go, null);
            }

            var byPath = HierarchyPath.Find(s);
            if (byPath == null) return (null, new ErrorResponse("TARGET_NOT_FOUND", $"No GameObject for parent path: '{s}'."));
            return (byPath, null);
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
                instance_id = EntityIdCompat.IdOf(go),
                name = go.name,
                path = HierarchyPath.Build(t),
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
    }
}
