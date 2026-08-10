using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal sealed class InputQaSequencePlan
    {
        internal readonly List<InputQaOptions> Steps = new List<InputQaOptions>();
        internal int TotalHoldMs;
        internal int TotalAwaitedFrames;
    }

    internal static partial class InputQaSequence
    {
        internal const int MaxSequenceSteps = 32;
        internal const int MaxTotalHoldMs = 30000;
        internal const int MaxTotalAwaitedFrames = 600;

        private static readonly HashSet<string> KeyboardFields =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "action", "backend", "key", "mode", "hold_ms", "settle_frames",
            };

        private static readonly HashSet<string> MouseFields =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "action", "backend", "mode", "button", "position", "delta",
                "scroll_delta", "hold_ms", "settle_frames",
            };

        internal static (InputQaSequencePlan plan, ErrorResponse error) Parse(
            JObject raw,
            int maxSteps = MaxSequenceSteps)
        {
            if (!(raw?["steps"] is JArray rawSteps) || rawSteps.Count == 0)
                return (null, new ErrorResponse(
                    "INPUT_SEQUENCE_INVALID_STEPS",
                    "[Hera] I need a non-empty 'steps' array for an input sequence."));
            if (rawSteps.Count > maxSteps)
                return (null, new ErrorResponse(
                    "INPUT_SEQUENCE_LIMIT_EXCEEDED",
                    $"[Hera] I can run at most {maxSteps} input sequence steps."));

            var plan = new InputQaSequencePlan();
            var heldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var heldButtons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < rawSteps.Count; index++)
            {
                if (!(rawSteps[index] is JObject step))
                    return StepError(
                        "INPUT_SEQUENCE_INVALID_STEP",
                        index,
                        "[Hera] I need each input sequence step to be a JSON object.");

                var action = step.Value<string>("action")?.Trim().ToLowerInvariant();
                if (action != "keyboard" && action != "mouse")
                    return StepError(
                        "INPUT_SEQUENCE_UNSUPPORTED_ACTION",
                        index,
                        "[Hera] I can only run keyboard and mouse steps in an input sequence.");

                var fields = action == "keyboard" ? KeyboardFields : MouseFields;
                foreach (var property in step.Properties())
                {
                    if (!fields.Contains(property.Name))
                        return StepError(
                            "INPUT_SEQUENCE_INVALID_STEP",
                            index,
                            $"[Hera] I don't recognize the {action} sequence field '{property.Name}'.");
                }

                var backend = step.Value<string>("backend")?.ToLowerInvariant();
                if (backend != null && backend != "inputsystem" && backend != "auto")
                    return StepError(
                        "INPUT_SEQUENCE_INVALID_STEP",
                        index,
                        "[Hera] I require backend 'inputsystem' or 'auto' for sequence steps.");

                var (options, parseError) = InputQaResolver.Parse(step);
                if (parseError != null)
                    return StepError(
                        "INPUT_SEQUENCE_INVALID_STEP",
                        index,
                        $"[Hera] I couldn't parse input sequence step {index}: {parseError.message}",
                        parseError.code);
                var validationError = ValidateStep(options, heldKeys, heldButtons);
                if (validationError != null)
                    return StepError(
                        validationError.code,
                        index,
                        validationError.message);

                plan.TotalHoldMs += AwaitedHoldMs(options);
                plan.TotalAwaitedFrames += options.SettleFrames + ImplicitFrames(options);
                if (plan.TotalHoldMs > MaxTotalHoldMs
                    || plan.TotalAwaitedFrames > MaxTotalAwaitedFrames)
                {
                    return StepError(
                        "INPUT_SEQUENCE_DURATION_EXCEEDED",
                        index,
                        $"[Hera] I limit sequence totals to {MaxTotalHoldMs} hold milliseconds " +
                        $"or {MaxTotalAwaitedFrames} awaited frames.");
                }
                plan.Steps.Add(options);
            }
            return (plan, null);
        }

        private static ErrorResponse ValidateStep(
            InputQaOptions options,
            ISet<string> heldKeys,
            ISet<string> heldButtons)
        {
            if (options.Action == "keyboard")
            {
                if (string.IsNullOrWhiteSpace(options.Key))
                    return InvalidStep("[Hera] I need 'key' for each keyboard sequence step.");
                var mode = options.Mode ?? "press";
                if (mode != "press" && mode != "down" && mode != "up")
                    return InvalidStep("[Hera] I need keyboard sequence mode to be press, down, or up.");
                return ValidateOwnership(heldKeys, Normalize(options.Key), mode, "key");
            }

            var mouseMode = options.Mode ?? "click";
            if (mouseMode != "move" && mouseMode != "click" && mouseMode != "down"
                && mouseMode != "up" && mouseMode != "delta" && mouseMode != "scroll")
            {
                return InvalidStep(
                    "[Hera] I need mouse sequence mode to be move, click, down, up, delta, or scroll.");
            }
            if (mouseMode == "move" && !options.Position.HasValue)
                return InvalidStep("[Hera] I need 'position' for mouse move sequence steps.");
            if (mouseMode == "delta" && !options.Delta.HasValue)
                return InvalidStep("[Hera] I need 'delta' for mouse delta sequence steps.");
            if (mouseMode == "scroll" && !options.ScrollDelta.HasValue)
                return InvalidStep("[Hera] I need 'scroll_delta' for mouse scroll sequence steps.");
            if (mouseMode == "click" || mouseMode == "down" || mouseMode == "up")
            {
                return ValidateOwnership(
                    heldButtons,
                    InputQaInputSystem.ButtonName(options.Button),
                    mouseMode,
                    "mouse button");
            }
            return null;
        }

        private static ErrorResponse ValidateOwnership(
            ISet<string> held,
            string control,
            string mode,
            string kind)
        {
            if (mode == "up")
            {
                if (!held.Remove(control))
                    return new ErrorResponse(
                        "INPUT_SEQUENCE_OWNERSHIP_INVALID",
                        $"[Hera] I can't release {kind} '{control}' before this sequence acquires it.");
                return null;
            }
            if (held.Contains(control))
                return new ErrorResponse(
                    "INPUT_SEQUENCE_OWNERSHIP_INVALID",
                    $"[Hera] I already hold {kind} '{control}' in this sequence.");
            if (mode == "down")
                held.Add(control);
            return null;
        }

        private static ErrorResponse InvalidStep(string message)
        {
            return new ErrorResponse("INPUT_SEQUENCE_INVALID_STEP", message);
        }

        private static int ImplicitFrames(InputQaOptions options)
        {
            var mode = options.Mode ?? (options.Action == "keyboard" ? "press" : "click");
            return mode == "press" || mode == "click" ? 1 : 0;
        }

        private static int AwaitedHoldMs(InputQaOptions options)
        {
            var mode = options.Mode ?? (options.Action == "keyboard" ? "press" : "click");
            return mode == "press" || mode == "click" ? options.HoldMs : 0;
        }

        private static string Normalize(string value)
        {
            return value.Replace("_", "").Replace("-", "").Replace(" ", "");
        }

        private static (InputQaSequencePlan plan, ErrorResponse error) StepError(
            string code,
            int index,
            string message,
            string causeCode = null)
        {
            return (null, new ErrorResponse(code, message, new
            {
                step_index = index,
                cause_code = causeCode,
            }));
        }
    }
}
