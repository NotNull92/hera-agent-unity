using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraActionSafety("create_clip", MayReloadDomain = true)]
    [HeraActionSafety("set_curve", MayReloadDomain = true)]
    [HeraActionSafety("create_controller", MayReloadDomain = true)]
    [HeraActionSafety("add_parameter", MayReloadDomain = true)]
    [HeraActionSafety("add_state", MayReloadDomain = true)]
    [HeraActionSafety("add_transition", MayReloadDomain = true)]
    [HeraTool(
        Name = "manage_animation",
        Description = "Author animation assets without exec boilerplate: create_clip / set_curve build an AnimationClip (.anim) and its float curves; create_controller / add_parameter / add_state / add_transition build an AnimatorController (.controller) state machine on its base layer. Paths are constrained to Assets/.",
        Destructive = true,
        MayReloadDomain = true,
        Examples = new[]
        {
            "manage_animation create_clip --path Assets/Anim/Bob.anim --frame_rate 60 --loop true",
            "manage_animation set_curve --path Assets/Anim/Bob.anim --type Transform --property localPosition.y --params '{\"keys\":[{\"time\":0,\"value\":0},{\"time\":0.5,\"value\":0.3},{\"time\":1,\"value\":0}]}'",
            "manage_animation create_controller --path Assets/Anim/Player.controller",
            "manage_animation add_parameter --path Assets/Anim/Player.controller --name Speed --type float",
            "manage_animation add_state --path Assets/Anim/Player.controller --name Run --motion Assets/Anim/Bob.anim --default true",
            "manage_animation add_transition --path Assets/Anim/Player.controller --from Idle --to Run --params '{\"conditions\":[{\"parameter\":\"Speed\",\"mode\":\"Greater\",\"threshold\":0.1}]}'",
        },
        ExampleDescriptions = new[]
        {
            "Create a looping 60fps AnimationClip",
            "Set a float curve on a clip (keyframes via --params)",
            "Create an AnimatorController (base layer + state machine)",
            "Add a typed parameter (float/int/bool/trigger)",
            "Add a state with a motion clip and make it the layer default",
            "Add a transition between states with a condition",
        })]
    public static class ManageAnimation
    {
        public class Parameters
        {
            [ToolParameter("Action: create_clip, set_curve, create_controller, add_parameter, add_state, add_transition.", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Asset path under Assets/. create_clip -> .anim, create_controller -> .controller (appended if omitted); the other actions target an existing asset by path.", Required = false)]
            public string Path { get; set; }

            [ToolParameter("create_clip: sampling rate in fps (default 60).", Required = false)]
            public float FrameRate { get; set; }

            [ToolParameter("create_clip: loop the clip (default false).", Required = false)]
            public bool Loop { get; set; }

            [ToolParameter("set_curve: animated component type — short 'Transform' or fully-qualified. add_parameter: float | int | bool | trigger.", Required = false)]
            public string Type { get; set; }

            [ToolParameter("set_curve: animated property path, e.g. localPosition.y, m_Color.a.", Required = false)]
            public string Property { get; set; }

            [ToolParameter("set_curve: GameObject path relative to the Animator root (default \"\" = the root object).", Required = false)]
            public string RelativePath { get; set; }

            [ToolParameter("add_parameter / add_state: the parameter or state name.", Required = false)]
            public string Name { get; set; }

            [ToolParameter("add_state: motion clip asset path (optional).", Required = false)]
            public string Motion { get; set; }

            [ToolParameter("add_state: make this the base-layer default state.", Required = false)]
            public bool Default { get; set; }

            [ToolParameter("add_transition: source state name.", Required = false)]
            public string From { get; set; }

            [ToolParameter("add_transition: destination state name.", Required = false)]
            public string To { get; set; }

            [ToolParameter("Complex payloads via --params: set_curve 'keys' [{time,value[,in_tangent,out_tangent]}]; add_parameter 'default'; add_transition 'conditions' [{parameter,mode,threshold}], 'has_exit_time', 'duration'.", Required = false)]
            public object Params { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params ?? new JObject());
            var action = (p.GetRaw("args") as JArray)?[0]?.ToString() ?? p.Get("action");
            if (string.IsNullOrWhiteSpace(action))
                return new ErrorResponse("MISSING_PARAM", "'action' required: create_clip, set_curve, create_controller, add_parameter, add_state, or add_transition.");

            switch (action.ToLowerInvariant())
            {
                case "create_clip": return CreateClip(p);
                case "set_curve": return SetCurve(p);
                case "create_controller": return CreateController(p);
                case "add_parameter": return AddParameter(p);
                case "add_state": return AddState(p);
                case "add_transition": return AddTransition(p);
                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action '{action}'. Valid: create_clip, set_curve, create_controller, add_parameter, add_state, add_transition.");
            }
        }

        // ---- AnimationClip ----

        private static object CreateClip(ToolParams p)
        {
            if (!TryPrepareNewAsset(p.Get("path"), ".anim", out var path, out var err))
                return err;

            var clip = new AnimationClip { frameRate = p.GetFloat("frame_rate", 60f) ?? 60f };
            var loop = p.GetBool("loop");
            if (loop)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new SuccessResponse("Clip created", new
            {
                path,
                guid = AssetDatabase.AssetPathToGUID(path),
                frame_rate = clip.frameRate,
                loop,
            });
        }

        private static object SetCurve(ToolParams p)
        {
            var clip = LoadClip(
                p.Get("path"), "ASSET_NOT_FOUND",
                "No AnimationClip at that path (expects an existing .anim).", out var clipErr);
            if (clip == null) return clipErr;

            var typeName = p.Get("type");
            var type = ComponentTypeResolver.Resolve(typeName);
            if (type == null)
                return new ErrorResponse("UNKNOWN_COMPONENT_TYPE",
                    $"Could not resolve animated type '{typeName}'.",
                    data: new { did_you_mean = ComponentTypeResolver.SuggestSimilar(typeName) });

            var property = p.Get("property");
            if (string.IsNullOrEmpty(property))
                return new ErrorResponse("MISSING_PARAM", "'property' required for set_curve (e.g. localPosition.y).");

            if (!(p.GetRaw("keys") is JArray keysToken) || keysToken.Count == 0)
                return new ErrorResponse("MISSING_PARAM", "'keys' required for set_curve: a JSON array of {time,value[,in_tangent,out_tangent]} via --params.");

            var frames = new List<Keyframe>(keysToken.Count);
            foreach (var k in keysToken)
            {
                if (!(k is JObject key) || key["time"] == null || key["value"] == null)
                    return new ErrorResponse("INVALID_KEY", "Each key needs numeric 'time' and 'value'.");
                var frame = new Keyframe(key["time"].Value<float>(), key["value"].Value<float>());
                if (key["in_tangent"] != null) frame.inTangent = key["in_tangent"].Value<float>();
                if (key["out_tangent"] != null) frame.outTangent = key["out_tangent"].Value<float>();
                frames.Add(frame);
            }

            var curve = new AnimationCurve(frames.ToArray());
            var relativePath = p.Get("relative_path", "") ?? "";
            clip.SetCurve(relativePath, type, property, curve);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return new SuccessResponse("Curve set", new
            {
                path = AssetDatabase.GetAssetPath(clip),
                relative_path = relativePath,
                type = type.FullName,
                property,
                keys = frames.Count,
                total_bindings = AnimationUtility.GetCurveBindings(clip).Length,
            });
        }

        // ---- AnimatorController ----

        private static object CreateController(ToolParams p)
        {
            if (!TryPrepareNewAsset(p.Get("path"), ".controller", out var path, out var err))
                return err;

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (ctrl == null)
                return new ErrorResponse("ASSET_CREATE_FAILED", $"Unity could not create an AnimatorController at '{path}'.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new SuccessResponse("Controller created", new
            {
                path,
                guid = AssetDatabase.AssetPathToGUID(path),
                layers = ctrl.layers.Length,
            });
        }

        private static object AddParameter(ToolParams p)
        {
            var ctrl = LoadController(p.Get("path"), out var err);
            if (ctrl == null) return err;

            var name = p.Get("name");
            if (string.IsNullOrEmpty(name))
                return new ErrorResponse("MISSING_PARAM", "'name' required for add_parameter.");
            if (Array.Exists(ctrl.parameters, x => x.name == name))
                return new ErrorResponse("PARAMETER_EXISTS", $"Parameter '{name}' already exists on this controller.");

            if (!TryParseParameterType(p.Get("type"), out var pType, out var typeErr))
                return typeErr;

            var param = new AnimatorControllerParameter { name = name, type = pType };
            var def = p.GetRaw("default");
            if (def != null && def.Type != JTokenType.Null)
            {
                switch (pType)
                {
                    case AnimatorControllerParameterType.Float: param.defaultFloat = def.Value<float>(); break;
                    case AnimatorControllerParameterType.Int: param.defaultInt = def.Value<int>(); break;
                    case AnimatorControllerParameterType.Bool: param.defaultBool = def.Value<bool>(); break;
                }
            }
            ctrl.AddParameter(param);
            AssetDatabase.SaveAssets();

            return new SuccessResponse("Parameter added", new
            {
                path = AssetDatabase.GetAssetPath(ctrl),
                name,
                type = pType.ToString(),
                parameters = ctrl.parameters.Length,
            });
        }

        private static object AddState(ToolParams p)
        {
            var ctrl = LoadController(p.Get("path"), out var err);
            if (ctrl == null) return err;

            var name = p.Get("name");
            if (string.IsNullOrEmpty(name))
                return new ErrorResponse("MISSING_PARAM", "'name' required for add_state.");

            var sm = ctrl.layers[0].stateMachine;
            if (Array.Exists(sm.states, s => s.state.name == name))
                return new ErrorResponse("STATE_EXISTS", $"State '{name}' already exists on the base layer.");

            var motionPath = p.Get("motion");
            string motionAssigned = null;
            AnimationClip motion = null;
            if (!string.IsNullOrEmpty(motionPath))
            {
                motion = LoadClip(
                    motionPath, "MOTION_NOT_FOUND",
                    $"No AnimationClip at motion path '{motionPath}'.", out var motionErr);
                if (motion == null) return motionErr;
                motionAssigned = AssetDatabase.GetAssetPath(motion);
            }

            var isDefault = p.GetBool("default");
            var state = sm.AddState(name);
            if (motion != null)
                state.motion = motion;
            if (isDefault)
                sm.defaultState = state;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            return new SuccessResponse("State added", new
            {
                path = AssetDatabase.GetAssetPath(ctrl),
                name,
                motion = motionAssigned,
                is_default = isDefault,
                states = sm.states.Length,
            });
        }

        private static object AddTransition(ToolParams p)
        {
            var ctrl = LoadController(p.Get("path"), out var err);
            if (ctrl == null) return err;

            var sm = ctrl.layers[0].stateMachine;
            var from = FindState(sm, p.Get("from"));
            var to = FindState(sm, p.Get("to"));
            if (from == null)
                return new ErrorResponse("STATE_NOT_FOUND", $"Source state '{p.Get("from")}' not found on the base layer.");
            if (to == null)
                return new ErrorResponse("STATE_NOT_FOUND", $"Destination state '{p.Get("to")}' not found on the base layer.");

            if (!TryPrepareConditions(ctrl, p.GetRaw("conditions"), out var conditions, out var conditionErr))
                return conditionErr;

            var transition = from.AddTransition(to);
            transition.hasExitTime = p.GetBool("has_exit_time");
            var duration = p.GetFloat("duration");
            if (duration.HasValue)
                transition.duration = duration.Value;

            var added = new List<object>();
            foreach (var condition in conditions)
            {
                transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
                added.Add(new { parameter = condition.parameter, mode = condition.mode.ToString(), threshold = condition.threshold });
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            return new SuccessResponse("Transition added", new
            {
                path = AssetDatabase.GetAssetPath(ctrl),
                from = from.name,
                to = to.name,
                has_exit_time = transition.hasExitTime,
                conditions = added,
            });
        }

        // ---- helpers ----

        private static AnimatorController LoadController(string rawPath, out ErrorResponse err)
        {
            err = null;
            if (!AssetPathGuard.TryNormalizeAssetFile(rawPath, out var path, out var pathErr))
            {
                err = new ErrorResponse("INVALID_PATH", pathErr);
                return null;
            }
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null)
                err = new ErrorResponse("ASSET_NOT_FOUND", "No AnimatorController at that path (expects an existing .controller).");
            return ctrl;
        }

        private static AnimationClip LoadClip(string rawPath, string missingCode, string missingMessage, out ErrorResponse err)
        {
            err = null;
            if (!AssetPathGuard.TryNormalizeAssetFile(rawPath, out var path, out var pathErr))
            {
                err = new ErrorResponse("INVALID_PATH", pathErr);
                return null;
            }
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
                err = new ErrorResponse(missingCode, missingMessage);
            return clip;
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var child in sm.states)
                if (child.state.name == name)
                    return child.state;
            return null;
        }

        private static bool TryParseParameterType(string raw, out AnimatorControllerParameterType type, out ErrorResponse err)
        {
            err = null;
            switch ((raw ?? "").ToLowerInvariant())
            {
                case "float": type = AnimatorControllerParameterType.Float; return true;
                case "int": type = AnimatorControllerParameterType.Int; return true;
                case "bool": type = AnimatorControllerParameterType.Bool; return true;
                case "trigger": type = AnimatorControllerParameterType.Trigger; return true;
                default:
                    type = AnimatorControllerParameterType.Float;
                    err = new ErrorResponse("MISSING_PARAM", "'type' required for add_parameter: float, int, bool, or trigger.");
                    return false;
            }
        }

        // Validate a destination for a new asset: Assets/-contained, correct
        // extension, non-existing, with an existing parent folder.
        private static bool TryPrepareNewAsset(string rawPath, string extension, out string path, out ErrorResponse err)
        {
            err = null;
            if (AssetPathGuard.TryPrepareNewAssetFile(
                    rawPath, extension, appendExtension: true,
                    out path, out var errorCode, out var error))
                return true;
            err = new ErrorResponse(errorCode, error);
            return false;
        }

        private static bool TryPrepareConditions(
            AnimatorController controller,
            JToken rawConditions,
            out List<(string parameter, AnimatorConditionMode mode, float threshold)> conditions,
            out ErrorResponse err)
        {
            conditions = new List<(string parameter, AnimatorConditionMode mode, float threshold)>();
            err = null;
            if (rawConditions == null || rawConditions.Type == JTokenType.Null)
                return true;
            if (!(rawConditions is JArray tokens))
            {
                err = new ErrorResponse("INVALID_CONDITION", "'conditions' must be an array of condition objects.");
                return false;
            }

            foreach (var token in tokens)
            {
                if (!(token is JObject condition) || string.IsNullOrWhiteSpace(condition["parameter"]?.ToString()))
                {
                    err = new ErrorResponse("INVALID_CONDITION", "Each condition needs a non-empty 'parameter' (and optional 'mode' / 'threshold').");
                    return false;
                }
                var parameter = condition["parameter"].ToString();
                if (!Array.Exists(controller.parameters, p => p.name == parameter))
                {
                    err = new ErrorResponse("PARAMETER_NOT_FOUND", $"AnimatorController has no parameter '{parameter}'.");
                    return false;
                }
                var modeText = condition["mode"]?.ToString() ?? "If";
                if (!Enum.TryParse<AnimatorConditionMode>(modeText, true, out var mode))
                {
                    err = new ErrorResponse("INVALID_CONDITION", $"Unknown condition mode '{modeText}'. Valid: If, IfNot, Greater, Less, Equals, NotEqual.");
                    return false;
                }

                float threshold;
                try { threshold = condition["threshold"]?.Value<float>() ?? 0f; }
                catch (Exception)
                {
                    err = new ErrorResponse("INVALID_CONDITION", $"Condition threshold for '{parameter}' must be numeric.");
                    return false;
                }
                conditions.Add((parameter, mode, threshold));
            }
            return true;
        }
    }
}
