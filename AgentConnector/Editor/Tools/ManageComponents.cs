using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
// Alias to dodge CS0104 between System.Object and UnityEngine.Object once
// `using System;` is in scope (Unity API author trap — see AGENT.md §4.14).
using Object = UnityEngine.Object;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "manage_components",
        Description = "Component CRUD on a target GameObject: add, remove, list, get (all properties or one), set (one property). Property paths are raw SerializedProperty paths (m_Name, m_LocalScale.x, m_Materials.Array.data[0]). Reference fields accept an InstanceID, an asset path, or a {instance_id|asset_path} envelope. Establishes the property-set pattern reused by every future manage_* tool (material, animation, vfx, scriptable objects, prefab properties).",
        Examples = new[]
        {
            "manage_components add --path /Player --type Rigidbody",
            "manage_components list --instance_id 12345",
            "manage_components get --path /Player --type Rigidbody",
            "manage_components get --path /Player --type Transform --property m_LocalScale",
            "manage_components set --path /Player --type Rigidbody --property m_Mass --value 5",
            "manage_components set --path /Player --type MeshRenderer --property m_Materials.Array.data[0] --value Assets/Mat.mat",
            "manage_components remove --component_id -67890",
        },
        ExampleDescriptions = new[]
        {
            "Attach a Rigidbody to /Player",
            "List every component on a GameObject",
            "Dump every visible serialized property of the Rigidbody",
            "Read just one property (Vector3 returned as {x,y,z})",
            "Set a scalar property",
            "Set an ObjectReference using an asset path",
            "Remove by component InstanceID — survives renames and duplicate types",
        })]
    public static class ManageComponents
    {
        public class Parameters
        {
            [ToolParameter("Action: add, remove, list, get, set", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Target GameObject by InstanceID (preferred)")]
            public int? InstanceId { get; set; }

            [ToolParameter("Target GameObject by hierarchy path '/Root/Child' (alternative to instance_id)")]
            public string Path { get; set; }

            [ToolParameter("Component type — short name 'Rigidbody' or fully-qualified 'UnityEngine.Rigidbody'. Required for add; required for remove/get/set unless component_id is given.")]
            public string Type { get; set; }

            [ToolParameter("When a GameObject has multiple components of the same type, pick by index (default 0). Ignored when component_id is given.")]
            public int? Index { get; set; }

            [ToolParameter("Target the component directly by its InstanceID — bypasses GameObject + type + index resolution. Survives renames and reparenting.")]
            public int? ComponentId { get; set; }

            [ToolParameter("SerializedProperty path (m_Name, m_LocalScale.x, m_Materials.Array.data[0]). For get: omit to dump every visible property. For set: required.")]
            public string Property { get; set; }

            [ToolParameter("Value for set. Scalars via --value; complex shapes (arrays, objects, reference envelopes) via --params '{\"value\":...}'.")]
            public string Value { get; set; }
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
                return new ErrorResponse("'action' required: add, remove, list, get, set");
            action = action.ToLowerInvariant();

            switch (action)
            {
                case "add": return Add(p);
                case "remove": return Remove(p);
                case "list": return List(p);
                case "get": return Get(p);
                case "set": return Set(p);
                default:
                    return new ErrorResponse(
                        $"Unknown manage_components action: '{action}'. Use add, remove, list, get, set.");
            }
        }

        // ---- sub-actions ----

        static object Add(ToolParams p)
        {
            var (go, goErr) = ResolveGameObjectTarget(p);
            if (goErr != null) return new ErrorResponse(goErr);

            string typeName = p.Get("type");
            if (string.IsNullOrEmpty(typeName))
                return new ErrorResponse("'type' required for add.");

            var type = ComponentTypeResolver.Resolve(typeName);
            if (type == null)
            {
                var similar = ComponentTypeResolver.SuggestSimilar(typeName);
                return new ErrorResponse(
                    "UNKNOWN_COMPONENT_TYPE",
                    $"Component type not found: '{typeName}'.",
                    data: similar.Count > 0 ? new { did_you_mean = similar } : null);
            }

            if (type == typeof(Transform))
                return new ErrorResponse("Transform is added automatically with every GameObject and cannot be added again.");

            Component comp;
            try
            {
                comp = Undo.AddComponent(go, type);
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"AddComponent failed: {ex.Message}");
            }
            if (comp == null)
                return new ErrorResponse($"AddComponent returned null — '{type.Name}' may forbid duplicates on this GameObject (e.g. via DisallowMultipleComponent).");

            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse(
                $"Added {type.Name} to {go.name}.",
                new
                {
                    instance_id = go.GetInstanceID(),
                    component = BuildComponentShape(comp, includeProperties: false),
                });
        }

        static object Remove(ToolParams p)
        {
            var (comp, go, err) = ResolveComponentTarget(p);
            if (err != null) return new ErrorResponse(err);

            if (comp is Transform)
                return new ErrorResponse("Transform cannot be removed.");

            var snapshot = BuildComponentShape(comp, includeProperties: false);

            try
            {
                if (Application.isPlaying) Object.Destroy(comp);
                else Undo.DestroyObjectImmediate(comp);
            }
            catch (Exception ex)
            {
                return new ErrorResponse($"Remove failed: {ex.Message}");
            }

            EditorSceneManager.MarkSceneDirty(go.scene);
            return new SuccessResponse(
                "Removed component.",
                new
                {
                    instance_id = go.GetInstanceID(),
                    removed = snapshot,
                });
        }

        static object List(ToolParams p)
        {
            var (go, goErr) = ResolveGameObjectTarget(p);
            if (goErr != null) return new ErrorResponse(goErr);

            var comps = go.GetComponents<Component>();
            var list = new List<object>(comps.Length);
            foreach (var c in comps)
            {
                if (c == null) continue; // missing script
                list.Add(BuildComponentShape(c, includeProperties: false));
            }
            return new SuccessResponse(
                $"{list.Count} components on {go.name}.",
                new
                {
                    instance_id = go.GetInstanceID(),
                    components = list,
                });
        }

        static object Get(ToolParams p)
        {
            var (comp, go, err) = ResolveComponentTarget(p);
            if (err != null) return new ErrorResponse(err);

            string propertyPath = p.Get("property");
            using var so = new SerializedObject(comp);

            if (string.IsNullOrEmpty(propertyPath))
            {
                return new SuccessResponse(
                    $"Read {comp.GetType().Name} on {go.name}.",
                    new
                    {
                        instance_id = go.GetInstanceID(),
                        component = BuildComponentShape(comp, so, includeProperties: true),
                    });
            }

            var prop = so.FindProperty(propertyPath);
            if (prop == null)
                return new ErrorResponse(
                    "PROPERTY_NOT_FOUND",
                    $"No SerializedProperty at '{propertyPath}' on {comp.GetType().Name}.",
                    data: new { available = EnumerateTopLevelPropertyNames(so) });

            return new SuccessResponse(
                $"Read {comp.GetType().Name}.{propertyPath}.",
                new
                {
                    instance_id = go.GetInstanceID(),
                    component_id = comp.GetInstanceID(),
                    type = comp.GetType().FullName,
                    property = propertyPath,
                    property_type = prop.propertyType.ToString(),
                    value = SerializedPropertyValue.Read(prop),
                });
        }

        static object Set(ToolParams p)
        {
            var (comp, go, err) = ResolveComponentTarget(p);
            if (err != null) return new ErrorResponse(err);

            string propertyPath = p.Get("property");
            if (string.IsNullOrEmpty(propertyPath))
                return new ErrorResponse("'property' required for set.");

            var valueToken = p.GetRaw("value");
            if (valueToken == null)
                return new ErrorResponse("'value' required for set.");

            using var so = new SerializedObject(comp);
            var prop = so.FindProperty(propertyPath);
            if (prop == null)
                return new ErrorResponse(
                    "PROPERTY_NOT_FOUND",
                    $"No SerializedProperty at '{propertyPath}' on {comp.GetType().Name}.",
                    data: new { available = EnumerateTopLevelPropertyNames(so) });

            Undo.RecordObject(comp, $"Hera Set {comp.GetType().Name}.{propertyPath}");
            var (ok, applyErr) = SerializedPropertyValue.Apply(prop, valueToken);
            if (!ok)
                return new ErrorResponse(
                    "VALUE_COERCION_FAILED",
                    $"Failed to apply value to {comp.GetType().Name}.{propertyPath} ({prop.propertyType}): {applyErr}");

            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(go.scene);

            // Re-read via a fresh SerializedObject so the returned value reflects
            // what Unity actually accepted (clamps, normalization, etc.).
            using var soFresh = new SerializedObject(comp);
            var propFresh = soFresh.FindProperty(propertyPath);
            return new SuccessResponse(
                $"Set {comp.GetType().Name}.{propertyPath}.",
                new
                {
                    instance_id = go.GetInstanceID(),
                    component_id = comp.GetInstanceID(),
                    type = comp.GetType().FullName,
                    property = propertyPath,
                    property_type = prop.propertyType.ToString(),
                    value = SerializedPropertyValue.Read(propFresh),
                });
        }

        // ---- helpers ----

        static (Component comp, GameObject go, string err) ResolveComponentTarget(ToolParams p)
        {
            var idToken = p.GetRaw("component_id");
            if (idToken != null && idToken.Type != JTokenType.Null)
            {
                int? id = p.GetInt("component_id");
                if (id == null) return (null, null, $"Invalid 'component_id': '{idToken}'.");
                var obj = EditorUtility.InstanceIDToObject(id.Value);
                if (obj == null) return (null, null, $"No object for component_id={id.Value}.");
                var comp = obj as Component;
                if (comp == null) return (null, null, $"instance_id={id.Value} is not a Component (type={obj.GetType().Name}).");
                return (comp, comp.gameObject, null);
            }

            var (go, goErr) = ResolveGameObjectTarget(p);
            if (goErr != null) return (null, null, goErr);

            string typeName = p.Get("type");
            if (string.IsNullOrEmpty(typeName))
                return (null, go, "Target required: pass 'component_id' or ('type' + GameObject target).");

            var type = ComponentTypeResolver.Resolve(typeName);
            if (type == null)
            {
                var similar = ComponentTypeResolver.SuggestSimilar(typeName);
                var hint = similar.Count > 0 ? $". Did you mean: {string.Join(", ", similar)}?" : "";
                return (null, go, $"Component type not found: '{typeName}'{hint}");
            }

            int index = p.GetInt("index", 0) ?? 0;
            var components = go.GetComponents(type);
            if (components.Length == 0)
                return (null, go, $"GameObject '{go.name}' has no {type.Name} component.");
            if (index < 0 || index >= components.Length)
                return (null, go, $"index {index} out of range — '{go.name}' has {components.Length} {type.Name} component(s).");

            return (components[index], go, null);
        }

        static (GameObject go, string err) ResolveGameObjectTarget(ToolParams p)
        {
            var idToken = p.GetRaw("instance_id");
            if (idToken != null && idToken.Type != JTokenType.Null)
            {
                int? id = p.GetInt("instance_id");
                if (id == null) return (null, $"Invalid 'instance_id': '{idToken}'.");
                var obj = EditorUtility.InstanceIDToObject(id.Value);
                if (obj == null) return (null, $"No object for instance_id={id.Value}.");
                GameObject go = obj as GameObject;
                if (go == null && obj is Component c) go = c.gameObject;
                if (go == null) return (null, $"instance_id={id.Value} is not a GameObject (type={obj.GetType().Name}).");
                return (go, null);
            }

            string path = p.Get("path");
            if (!string.IsNullOrEmpty(path))
            {
                var go = FindByPath(path);
                if (go == null) return (null, $"No GameObject at path: '{path}'.");
                return (go, null);
            }

            return (null, "GameObject target required: pass 'instance_id' or 'path'.");
        }

        // Mirrors ManageGameObject.ResolveByPath — covers inactive subtrees that
        // GameObject.Find skips. When a third tool needs the same walk this
        // moves to Core (currently second consumer, kept private here to avoid
        // premature extraction).
        static GameObject FindByPath(string path)
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

        static Transform WalkPath(Transform t, string[] segments, int index)
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

        static object BuildComponentShape(Component comp, bool includeProperties)
        {
            if (!includeProperties)
            {
                return new
                {
                    component_id = comp.GetInstanceID(),
                    type = comp.GetType().FullName,
                    type_short = comp.GetType().Name,
                    enabled = TryGetEnabled(comp),
                };
            }
            using var so = new SerializedObject(comp);
            return BuildComponentShape(comp, so, includeProperties: true);
        }

        static object BuildComponentShape(Component comp, SerializedObject so, bool includeProperties)
        {
            var properties = new Dictionary<string, object>();
            if (includeProperties)
            {
                var iter = so.GetIterator();
                bool enter = true;
                while (iter.NextVisible(enter))
                {
                    enter = false;
                    try { properties[iter.name] = SerializedPropertyValue.Read(iter); }
                    catch (Exception ex) { properties[iter.name] = new { read_error = ex.Message }; }
                }
            }
            return new
            {
                component_id = comp.GetInstanceID(),
                type = comp.GetType().FullName,
                type_short = comp.GetType().Name,
                enabled = TryGetEnabled(comp),
                properties = (object)properties,
            };
        }

        static bool? TryGetEnabled(Component comp)
        {
            if (comp is Behaviour beh) return beh.enabled;
            if (comp is Renderer rnd) return rnd.enabled;
            if (comp is Collider col) return col.enabled;
            return null;
        }

        static List<string> EnumerateTopLevelPropertyNames(SerializedObject so)
        {
            var names = new List<string>();
            var iter = so.GetIterator();
            bool enter = true;
            while (iter.NextVisible(enter))
            {
                enter = false;
                names.Add(iter.name);
            }
            return names;
        }
    }
}
