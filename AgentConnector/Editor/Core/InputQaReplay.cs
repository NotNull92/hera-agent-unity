using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal static class InputQaReplay
    {
        private static readonly HashSet<string> EventFields =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "frame", "action", "mode", "key", "button", "position", "delta",
                "scroll_delta",
            };

        internal static async Task<object> Execute(JObject raw)
        {
            var requested = raw?["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(requested))
                return new ErrorResponse(
                    "INPUT_RECORDING_PATH_REQUIRED",
                    "[Hera] I need 'path' for input replay.");
            if (!InputQaRecording.TryResolvePath(requested, true, out var path, out var pathError))
                return pathError;

            var (plan, loadError, source) = Load(path);
            if (loadError != null)
                return loadError;
            return await InputQaSequence.ExecutePlan(plan, "replay", source, false);
        }

        internal static (
            InputQaSequencePlan plan,
            ErrorResponse error,
            object source) Load(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length > InputQaRecording.MaxFileBytes)
                    return Invalid(
                        "INPUT_RECORDING_FILE_TOO_LARGE",
                        $"I rejected a {info.Length}-byte file because the limit is {InputQaRecording.MaxFileBytes} bytes.");
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length > InputQaRecording.MaxFileBytes)
                    return Invalid(
                        "INPUT_RECORDING_FILE_TOO_LARGE",
                        $"I stopped because the file grew beyond the {InputQaRecording.MaxFileBytes}-byte limit while I read it.");
                var root = JObject.Parse(Encoding.UTF8.GetString(bytes));
                if (root.Value<string>("schema") != InputQaRecording.Schema)
                    return Invalid(
                        "INPUT_RECORDING_SCHEMA_UNSUPPORTED",
                        $"I require schema '{InputQaRecording.Schema}'.");
                if (!(root["events"] is JArray events) || events.Count == 0)
                    return Invalid(
                        "INPUT_RECORDING_INVALID_EVENTS",
                        "I need a non-empty 'events' array.");
                if (events.Count > InputQaRecording.MaxEvents)
                    return Invalid(
                        "INPUT_RECORDING_EVENT_LIMIT_EXCEEDED",
                        $"I can replay at most {InputQaRecording.MaxEvents} events.");

                var steps = new JArray();
                var frames = new List<int>(events.Count);
                var previousFrame = -1;
                for (var index = 0; index < events.Count; index++)
                {
                    if (!(events[index] is JObject inputEvent))
                        return EventError(index, "I need each event to be a JSON object.");
                    foreach (var property in inputEvent.Properties())
                    {
                        if (!EventFields.Contains(property.Name))
                            return EventError(index, "I don't recognize event field '" + property.Name + "'.");
                    }
                    if (inputEvent["frame"]?.Type != JTokenType.Integer)
                        return EventError(index, "I need an integer 'frame' on each event.");
                    var frame = inputEvent.Value<int>("frame");
                    if (frame < 0 || frame > InputQaRecording.MaxFrames || frame < previousFrame)
                        return EventError(
                            index,
                            $"I need monotonic frames from 0 through {InputQaRecording.MaxFrames}.");
                    if (index == 0 && frame != 0)
                        return EventError(index, "I need the first event to start at frame 0.");
                    previousFrame = frame;
                    frames.Add(frame);

                    var step = new JObject(inputEvent);
                    step.Remove("frame");
                    step["settle_frames"] = 0;
                    var action = step.Value<string>("action");
                    var mode = step.Value<string>("mode");
                    if ((action == "keyboard" && mode != "down" && mode != "up")
                        || (action == "mouse" && mode != "move" && mode != "down"
                            && mode != "up" && mode != "delta" && mode != "scroll"))
                    {
                        return EventError(
                            index,
                            "I allow keyboard down/up or mouse move/down/up/delta/scroll recording events only.");
                    }
                    steps.Add(step);
                }

                var (plan, parseError) = InputQaSequence.Parse(
                    new JObject
                    {
                        ["action"] = "sequence",
                        ["steps"] = steps,
                    },
                    InputQaRecording.MaxEvents);
                if (parseError != null)
                {
                    return Invalid(
                        "INPUT_RECORDING_PREFLIGHT_FAILED",
                        parseError.message,
                        new { cause_code = parseError.code, cause = parseError.data });
                }

                for (var index = 0; index < plan.Steps.Count; index++)
                {
                    var step = plan.Steps[index];
                    if (!IsFinite(step.Position) || !IsFinite(step.Delta)
                        || !IsFinite(step.ScrollDelta))
                    {
                        return EventError(
                            index,
                            "I need finite numeric components in every input vector.");
                    }
                }

                for (var index = 0; index < plan.Steps.Count - 1; index++)
                    plan.Steps[index].SettleFrames = frames[index + 1] - frames[index];
                plan.Steps[plan.Steps.Count - 1].SettleFrames = 0;
                plan.TotalAwaitedFrames = frames[frames.Count - 1];
                return (
                    plan,
                    null,
                    new
                    {
                        schema = InputQaRecording.Schema,
                        path,
                        bytes = bytes.Length,
                        update_type = root["metadata"]?["update_type"]?.Value<string>(),
                    });
            }
            catch (Exception ex)
            {
                return Invalid(
                    "INPUT_RECORDING_READ_FAILED",
                    "I couldn't read the recording: " + ex.Message);
            }
        }

        private static (InputQaSequencePlan, ErrorResponse, object) EventError(
            int index,
            string message)
        {
            return Invalid(
                "INPUT_RECORDING_INVALID_EVENT",
                message,
                new { event_index = index });
        }

        private static bool IsFinite(UnityEngine.Vector2? value)
        {
            return !value.HasValue
                || (!float.IsNaN(value.Value.x)
                    && !float.IsInfinity(value.Value.x)
                    && !float.IsNaN(value.Value.y)
                    && !float.IsInfinity(value.Value.y));
        }

        private static (InputQaSequencePlan, ErrorResponse, object) Invalid(
            string code,
            string message,
            object data = null)
        {
            var shaped = message.StartsWith("[Hera]", StringComparison.Ordinal)
                ? message
                : "[Hera] " + message;
            return (
                null,
                new ErrorResponse(code, shaped, data),
                null);
        }
    }
}
