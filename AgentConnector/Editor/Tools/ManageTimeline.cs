using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "manage_timeline",
        Description = "Create and inspect Timeline assets, then add validated tracks and clips. Uses reflection so com.unity.timeline remains optional; unavailable projects receive PACKAGE_NOT_INSTALLED.",
        Profiles = new[] { "scene" },
        RiskClass = HeraRiskClass.Write,
        ContractMode = ToolContractMode.Strict)]
    public static class ManageTimeline
    {
        private const string TimelineAssembly = "Unity.Timeline";
        private const string TimelineTypeName = "UnityEngine.Timeline.TimelineAsset, Unity.Timeline";
        private const string TrackTypeName = "UnityEngine.Timeline.TrackAsset, Unity.Timeline";

        public sealed class CreateParameters
        {
            [ToolParameter("New Timeline asset path under Assets/. The .playable extension is optional.", Required = true)]
            public string Path { get; set; }

            [ToolParameter(
                "Timeline frame rate (default 60).",
                SchemaJson = "{\"type\":\"number\",\"exclusiveMinimum\":0}")]
            public float? FrameRate { get; set; }
        }

        public class PathParameters
        {
            [ToolParameter("Timeline asset path under Assets/, or a durable asset handle.", Required = true)]
            public string Path { get; set; }
        }

        public sealed class GetParameters : PathParameters
        {
            [ToolParameter(
                "Maximum combined tracks and clips returned (default 100, max 500).",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":500}")]
            public int? Limit { get; set; }
        }

        public sealed class AddTrackParameters : PathParameters
        {
            [ToolParameter(
                "Track type.",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"Animation\",\"Audio\",\"Activation\",\"Control\",\"Playable\",\"Signal\",\"Marker\",\"Group\"]}")]
            public string Type { get; set; }

            [ToolParameter("Track display name. Defaults to the type name.")]
            public string Name { get; set; }

            [ToolParameter("Optional parent track name.")]
            public string Parent { get; set; }
        }

        public sealed class AddClipParameters : PathParameters
        {
            [ToolParameter("Exact target track name.", Required = true)]
            public string Track { get; set; }

            [ToolParameter("Optional source asset path or durable asset handle.")]
            public string Asset { get; set; }

            [ToolParameter("Clip display name.")]
            public string Name { get; set; }

            [ToolParameter("Clip start time in seconds.", Required = true, SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public double? Start { get; set; }

            [ToolParameter("Clip duration in seconds.", Required = true, SchemaJson = "{\"type\":\"number\",\"exclusiveMinimum\":0}")]
            public double? Duration { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class TimelineSummary
        {
            public string Path { get; set; }
            public string Guid { get; set; }
            public float FrameRate { get; set; }
            public double Duration { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class TrackMutationResult
        {
            public string Path { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public string Parent { get; set; }
            public int Index { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class ClipMutationResult
        {
            public string Path { get; set; }
            public string Track { get; set; }
            public string Name { get; set; }
            public double Start { get; set; }
            public double Duration { get; set; }
            public string Asset { get; set; }
        }

        [HeraAction(
            ParametersType = typeof(CreateParameters),
            ResultType = typeof(TimelineSummary),
            RiskClass = HeraRiskClass.Write)]
        public static object Create(JObject raw)
        {
            var timelineType = TimelineType();
            if (timelineType == null) return PackageMissing();

            var p = new ToolParams(raw);
            if (!AssetPathGuard.TryPrepareNewAssetFile(
                    p.Get("path"), ".playable", appendExtension: true,
                    out var path, out var pathCode, out var pathError))
                return new ErrorResponse(pathCode, pathError);

            var frameRate = p.GetFloat("frame_rate", 60f) ?? 60f;
            if (frameRate <= 0f)
                return new ErrorResponse("INVALID_PARAM", "'frame_rate' must be greater than zero.");

            try
            {
                var timeline = ScriptableObject.CreateInstance(timelineType);
                if (timeline == null)
                    return new ErrorResponse("TIMELINE_CREATE_FAILED", "Unity could not create a TimelineAsset instance.");
                SetFrameRate(timeline, timelineType, frameRate);
                AssetDatabase.CreateAsset(timeline, path);
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path);
                return new SuccessResponse("Timeline created", new
                {
                    path,
                    guid = AssetDatabase.AssetPathToGUID(path),
                    frame_rate = GetFrameRate(timeline, timelineType),
                    duration = GetDouble(timeline, "duration"),
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse("TIMELINE_CREATE_FAILED", ReflectionMessage(exception));
            }
        }

        [HeraAction(
            ParametersType = typeof(GetParameters),
            RiskClass = HeraRiskClass.ReadOnly)]
        public static object Get(JObject raw)
        {
            var p = new ToolParams(raw);
            var (timeline, path, error) = LoadTimeline(p.Get("path"));
            if (error != null) return error;

            var limit = Mathf.Clamp(p.GetInt("limit", 100).Value, 1, 500);
            var remaining = limit;
            var truncated = false;
            var tracks = new List<object>();
            foreach (var track in AllTracks(timeline))
            {
                if (remaining == 0)
                {
                    truncated = true;
                    break;
                }
                remaining--;
                var clips = new List<object>();
                foreach (var clip in GetItems(track, "GetClips"))
                {
                    if (remaining == 0)
                    {
                        truncated = true;
                        break;
                    }
                    remaining--;
                    var source = GetProperty(clip, "asset") as UnityEngine.Object;
                    clips.Add(new
                    {
                        name = GetProperty(clip, "displayName") as string,
                        start = GetDouble(clip, "start"),
                        duration = GetDouble(clip, "duration"),
                        asset = source != null ? AssetDatabase.GetAssetPath(source) : null,
                    });
                }

                var parent = GetProperty(track, "parent") as UnityEngine.Object;
                tracks.Add(new
                {
                    name = ((UnityEngine.Object)track).name,
                    type = FriendlyTrackType(track.GetType()),
                    parent = parent != null && !TimelineType().IsInstanceOfType(parent) ? parent.name : null,
                    muted = GetBool(track, "muted"),
                    locked = GetBool(track, "locked"),
                    clips,
                });
            }

            var timelineType = timeline.GetType();
            return new SuccessResponse($"Timeline: {tracks.Count} track(s).", new
            {
                path,
                guid = AssetDatabase.AssetPathToGUID(path),
                frame_rate = GetFrameRate(timeline, timelineType),
                duration = GetDouble(timeline, "duration"),
                tracks,
                returned = limit - remaining,
                truncated,
            });
        }

        [HeraAction(
            ParametersType = typeof(AddTrackParameters),
            ResultType = typeof(TrackMutationResult),
            RiskClass = HeraRiskClass.Write)]
        public static object AddTrack(JObject raw)
        {
            var p = new ToolParams(raw);
            var (timeline, path, error) = LoadTimeline(p.Get("path"));
            if (error != null) return error;

            var requestedType = p.Get("type");
            var trackType = ResolveTrackType(requestedType);
            if (trackType == null)
                return new ErrorResponse("TIMELINE_TRACK_TYPE_NOT_FOUND", $"Track type '{requestedType}' is unavailable. Use Animation, Audio, Activation, Control, Playable, Signal, Marker, or Group.");

            var name = p.Get("name") ?? requestedType;
            if (string.IsNullOrWhiteSpace(name))
                return new ErrorResponse("MISSING_PARAM", "'name' or 'type' required for add_track.");
            var existing = FindTracks(timeline, name);
            if (existing.Count > 0)
                return new ErrorResponse("TIMELINE_TRACK_EXISTS", $"Track '{name}' already exists.");

            object parent = null;
            var parentName = p.Get("parent");
            if (!string.IsNullOrEmpty(parentName))
            {
                var parents = FindTracks(timeline, parentName);
                if (parents.Count == 0)
                    return new ErrorResponse("TIMELINE_TRACK_NOT_FOUND", $"Parent track '{parentName}' was not found.");
                if (parents.Count > 1)
                    return new ErrorResponse("TIMELINE_TRACK_AMBIGUOUS", $"More than one track is named '{parentName}'.");
                parent = parents[0];
            }

            try
            {
                var method = timeline.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(candidate =>
                    {
                        if (candidate.Name != "CreateTrack" || candidate.IsGenericMethod) return false;
                        var parameters = candidate.GetParameters();
                        return parameters.Length == 3 && parameters[0].ParameterType == typeof(Type);
                    });
                if (method == null)
                    return new ErrorResponse("TIMELINE_API_UNAVAILABLE", "TimelineAsset.CreateTrack(Type, TrackAsset, string) is unavailable.");

                var created = method.Invoke(timeline, new[] { (object)trackType, parent, name }) as UnityEngine.Object;
                if (created == null)
                    return new ErrorResponse("TIMELINE_TRACK_CREATE_FAILED", $"Unity did not create track '{name}'.");
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();
                return new SuccessResponse("Timeline track added", new
                {
                    path,
                    name = created.name,
                    type = FriendlyTrackType(created.GetType()),
                    parent = parentName,
                    index = AllTracks(timeline).IndexOf(created),
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse("TIMELINE_TRACK_CREATE_FAILED", ReflectionMessage(exception));
            }
        }

        [HeraAction(
            ParametersType = typeof(AddClipParameters),
            ResultType = typeof(ClipMutationResult),
            RiskClass = HeraRiskClass.Write)]
        public static object AddClip(JObject raw)
        {
            var p = new ToolParams(raw);
            var (timeline, path, error) = LoadTimeline(p.Get("path"));
            if (error != null) return error;

            var trackName = p.Get("track");
            if (string.IsNullOrWhiteSpace(trackName))
                return new ErrorResponse("MISSING_PARAM", "'track' required for add_clip.");
            var tracks = FindTracks(timeline, trackName);
            if (tracks.Count == 0)
                return new ErrorResponse("TIMELINE_TRACK_NOT_FOUND", $"Track '{trackName}' was not found.");
            if (tracks.Count > 1)
                return new ErrorResponse("TIMELINE_TRACK_AMBIGUOUS", $"More than one track is named '{trackName}'.");

            var startToken = p.GetRaw("start");
            var durationToken = p.GetRaw("duration");
            if (startToken == null || durationToken == null)
                return new ErrorResponse("MISSING_PARAM", "'start' and 'duration' required for add_clip.");
            double start;
            double duration;
            try
            {
                start = startToken.Value<double>();
                duration = durationToken.Value<double>();
            }
            catch (Exception)
            {
                return new ErrorResponse("INVALID_PARAM", "'start' and 'duration' must be numbers.");
            }
            if (start < 0d || duration <= 0d)
                return new ErrorResponse("INVALID_PARAM", "'start' must be non-negative and 'duration' must be greater than zero.");

            UnityEngine.Object source = null;
            string sourcePath = null;
            var requestedAsset = p.Get("asset");
            if (!string.IsNullOrEmpty(requestedAsset))
            {
                if (!AssetPathGuard.TryNormalizeExistingAssetFile(
                        requestedAsset, out sourcePath, out var resolved,
                        out var pathCode, out var pathError))
                    return new ErrorResponse(pathCode, pathError);
                source = resolved ?? AssetDatabase.LoadMainAssetAtPath(sourcePath);
                if (source == null)
                    return new ErrorResponse("ASSET_NOT_FOUND", $"Clip source asset not found: '{requestedAsset}'.");
            }

            try
            {
                var track = tracks[0];
                object clip;
                if (source == null)
                {
                    var createDefault = track.GetType().GetMethod("CreateDefaultClip", BindingFlags.Instance | BindingFlags.Public);
                    clip = createDefault?.Invoke(track, null);
                }
                else
                {
                    var createWithAsset = track.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .FirstOrDefault(method =>
                        {
                            if (method.Name != "CreateClip" || method.IsGenericMethod) return false;
                            var parameters = method.GetParameters();
                            return parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(source.GetType());
                        });
                    if (createWithAsset == null)
                        return new ErrorResponse("TIMELINE_CLIP_ASSET_UNSUPPORTED", $"Track '{trackName}' cannot create a clip from {source.GetType().Name}.");
                    clip = createWithAsset.Invoke(track, new object[] { source });
                }

                if (clip == null)
                    return new ErrorResponse("TIMELINE_CLIP_CREATE_FAILED", $"Unity did not create a clip on track '{trackName}'.");
                SetProperty(clip, "start", start);
                SetProperty(clip, "duration", duration);
                var name = p.Get("name");
                if (!string.IsNullOrEmpty(name)) SetProperty(clip, "displayName", name);
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();
                return new SuccessResponse("Timeline clip added", new
                {
                    path,
                    track = trackName,
                    name = GetProperty(clip, "displayName") as string,
                    start = GetDouble(clip, "start"),
                    duration = GetDouble(clip, "duration"),
                    asset = sourcePath,
                });
            }
            catch (Exception exception)
            {
                return new ErrorResponse("TIMELINE_CLIP_CREATE_FAILED", ReflectionMessage(exception));
            }
        }

        private static Type TimelineType() => Type.GetType(TimelineTypeName, false);

        private static ErrorResponse PackageMissing() => new ErrorResponse(
            "PACKAGE_NOT_INSTALLED",
            "com.unity.timeline is not installed or its Unity.Timeline assembly is not loaded.");

        private static (UnityEngine.Object timeline, string path, ErrorResponse error) LoadTimeline(string rawPath)
        {
            var timelineType = TimelineType();
            if (timelineType == null) return (null, null, PackageMissing());
            if (!AssetPathGuard.TryNormalizeExistingAssetFile(
                    rawPath, out var path, out var resolved, out var pathCode, out var pathError))
                return (null, null, new ErrorResponse(pathCode, pathError));

            var asset = resolved ?? AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null || !timelineType.IsInstanceOfType(asset))
                return (null, null, new ErrorResponse("TIMELINE_NOT_FOUND", $"No TimelineAsset at '{rawPath}'."));
            return (asset, path, null);
        }

        private static Type ResolveTrackType(string raw)
        {
            string typeName;
            switch ((raw ?? "").ToLowerInvariant())
            {
                case "animation": typeName = "AnimationTrack"; break;
                case "audio": typeName = "AudioTrack"; break;
                case "activation": typeName = "ActivationTrack"; break;
                case "control": typeName = "ControlTrack"; break;
                case "playable": typeName = "PlayableTrack"; break;
                case "signal": typeName = "SignalTrack"; break;
                case "marker": typeName = "MarkerTrack"; break;
                case "group": typeName = "GroupTrack"; break;
                default: return null;
            }
            var type = Type.GetType($"UnityEngine.Timeline.{typeName}, {TimelineAssembly}", false);
            var trackType = Type.GetType(TrackTypeName, false);
            return type != null && trackType != null && trackType.IsAssignableFrom(type) && !type.IsAbstract ? type : null;
        }

        private static List<UnityEngine.Object> AllTracks(UnityEngine.Object timeline)
        {
            var tracks = new List<UnityEngine.Object>();
            foreach (var root in GetItems(timeline, "GetRootTracks"))
                AddTrackTree(root, tracks);
            return tracks;
        }

        private static void AddTrackTree(object track, List<UnityEngine.Object> tracks)
        {
            if (!(track is UnityEngine.Object unityTrack)) return;
            tracks.Add(unityTrack);
            foreach (var child in GetItems(track, "GetChildTracks"))
                AddTrackTree(child, tracks);
        }

        private static List<UnityEngine.Object> FindTracks(UnityEngine.Object timeline, string name)
        {
            return AllTracks(timeline).Where(track => track.name == name).ToList();
        }

        private static IEnumerable<object> GetItems(object owner, string methodName)
        {
            var method = owner.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            if (!(method?.Invoke(owner, null) is IEnumerable items)) yield break;
            foreach (var item in items) yield return item;
        }

        private static object GetProperty(object owner, string name)
        {
            return owner?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(owner, null);
        }

        private static void SetProperty(object owner, string name, object value)
        {
            var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanWrite)
                throw new MissingMemberException(owner.GetType().FullName, name);
            property.SetValue(owner, Convert.ChangeType(value, property.PropertyType), null);
        }

        private static double GetDouble(object owner, string name)
        {
            var value = GetProperty(owner, name);
            return value == null ? 0d : Convert.ToDouble(value);
        }

        private static bool GetBool(object owner, string name)
        {
            var value = GetProperty(owner, name);
            return value != null && Convert.ToBoolean(value);
        }

        private static void SetFrameRate(UnityEngine.Object timeline, Type timelineType, float frameRate)
        {
            var settings = timelineType.GetProperty("editorSettings", BindingFlags.Instance | BindingFlags.Public)?.GetValue(timeline, null);
            if (settings == null) throw new MissingMemberException(timelineType.FullName, "editorSettings");
            var property = settings.GetType().GetProperty("frameRate", BindingFlags.Instance | BindingFlags.Public)
                ?? settings.GetType().GetProperty("fps", BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanWrite)
                throw new MissingMemberException(settings.GetType().FullName, "frameRate");
            property.SetValue(settings, Convert.ChangeType(frameRate, property.PropertyType), null);
        }

        private static float GetFrameRate(UnityEngine.Object timeline, Type timelineType)
        {
            var settings = timelineType.GetProperty("editorSettings", BindingFlags.Instance | BindingFlags.Public)?.GetValue(timeline, null);
            var value = settings?.GetType().GetProperty("frameRate", BindingFlags.Instance | BindingFlags.Public)?.GetValue(settings, null)
                ?? settings?.GetType().GetProperty("fps", BindingFlags.Instance | BindingFlags.Public)?.GetValue(settings, null);
            return value == null ? 0f : Convert.ToSingle(value);
        }

        private static string FriendlyTrackType(Type type)
        {
            return type.Name.EndsWith("Track", StringComparison.Ordinal)
                ? type.Name.Substring(0, type.Name.Length - "Track".Length)
                : type.Name;
        }

        private static string ReflectionMessage(Exception exception)
        {
            while (exception is TargetInvocationException invocation && invocation.InnerException != null)
                exception = invocation.InnerException;
            return exception.Message;
        }
    }
}
