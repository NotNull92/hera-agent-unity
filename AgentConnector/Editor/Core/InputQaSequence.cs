using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace HeraAgent
{
    [InitializeOnLoad]
    internal static partial class InputQaSequence
    {
        private const int MaxWallClockMs = 45000;
        private static CancellationTokenSource activeSource;

        static InputQaSequence()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                    CancelActive();
            };
            AssemblyReloadEvents.beforeAssemblyReload += CancelActive;
        }

        internal static async Task<object> Execute(JObject raw)
        {
            var (plan, parseError) = Parse(raw);
            if (parseError != null)
                return parseError;
            return await ExecutePlan(plan, "sequence", null, true);
        }

        internal static async Task<object> ExecutePlan(
            InputQaSequencePlan plan,
            string action,
            object sourceDetails,
            bool includeStepSummaries)
        {
            if (activeSource != null)
                return new ErrorResponse(
                    "INPUT_SEQUENCE_BUSY",
                    "[Hera] I can't start another input sequence while one is already running.");
            if (InputQaInputSystem.HasHeldControls())
                return new ErrorResponse(
                    "INPUT_SEQUENCE_PREEXISTING_HOLD",
                    "[Hera] I need you to release standalone Hera-held keys and mouse buttons " +
                    "before starting a sequence.",
                    new { held = InputQaInputSystem.InjectedState() });

            for (var index = 0; index < plan.Steps.Count; index++)
            {
                var error = InputQaInputSystem.ValidateForSequence(plan.Steps[index]);
                if (error != null)
                {
                    return new ErrorResponse(
                        "INPUT_SEQUENCE_PREFLIGHT_FAILED",
                        $"[Hera] I couldn't preflight input sequence step {index}: {error.message}",
                        new
                        {
                            step_index = index,
                            step_action = plan.Steps[index].Action,
                            cause_code = error.code,
                        });
                }
            }

            var summaries = new List<object>(plan.Steps.Count);
            var completed = 0;
            var failedIndex = -1;
            ErrorResponse failure = null;
            InputQaCleanupResult cleanup;
            var stopwatch = Stopwatch.StartNew();
            var source = new CancellationTokenSource();
            source.CancelAfter(MaxWallClockMs);
            activeSource = source;
            try
            {
                for (var index = 0; index < plan.Steps.Count; index++)
                {
                    var step = plan.Steps[index];
                    step.CancellationToken = source.Token;
                    var response = await ExecuteStep(step);
                    if (response is ErrorResponse error)
                    {
                        failedIndex = index;
                        failure = error;
                        break;
                    }
                    if (!(response is SuccessResponse))
                    {
                        failedIndex = index;
                        failure = new ErrorResponse(
                            "INPUT_SEQUENCE_INVALID_RESULT",
                            "[Hera] I received an unknown response type from an input sequence step.");
                        break;
                    }

                    if (includeStepSummaries)
                        summaries.Add(StepSummary(index, step));
                    completed++;
                }
            }
            catch (OperationCanceledException)
            {
                failedIndex = completed;
                failure = new ErrorResponse(
                    "INPUT_SEQUENCE_CANCELLED",
                    "[Hera] I cancelled the input sequence before completion.");
            }
            catch (Exception ex)
            {
                failedIndex = completed;
                failure = new ErrorResponse(
                    "INPUT_SEQUENCE_EXECUTION_FAILED",
                    "[Hera] I couldn't finish the input sequence: " + ex.Message);
            }
            finally
            {
                activeSource = null;
                source.Dispose();
                cleanup = InputQaInputSystem.ReleaseSequenceControls();
                stopwatch.Stop();
            }

            var result = new
            {
                backend = "inputsystem",
                evidence_level = "inputsystem",
                action,
                source = sourceDetails,
                steps_total = plan.Steps.Count,
                completed_count = completed,
                failed_step_index = failedIndex < 0 ? (int?)null : failedIndex,
                total_hold_ms = plan.TotalHoldMs,
                total_awaited_frames = plan.TotalAwaitedFrames,
                elapsed_ms = stopwatch.ElapsedMilliseconds,
                cleanup,
                held_after = InputQaInputSystem.InjectedState(),
                steps = includeStepSummaries ? summaries : null,
                cause_code = failure?.code,
            };

            if (!cleanup.succeeded)
                return new ErrorResponse(
                    "INPUT_SEQUENCE_CLEANUP_FAILED",
                    "[Hera] I ended the input sequence but couldn't release one or more held controls.",
                    result);
            if (failure != null)
            {
                var code = failure.code == "INPUT_SEQUENCE_CANCELLED"
                    ? failure.code
                    : "INPUT_SEQUENCE_STEP_FAILED";
                var message = failure.message.StartsWith("[Hera]", StringComparison.Ordinal)
                    ? failure.message
                    : $"[Hera] I couldn't complete input sequence step {failedIndex}: " +
                      failure.message;
                return new ErrorResponse(code, message, result);
            }
            return new SuccessResponse(
                action == "replay" ? "Input replay" : "Input sequence",
                result);
        }

        private static Task<object> ExecuteStep(InputQaOptions step)
        {
            return step.Action == "keyboard"
                ? InputQaInputSystem.Keyboard(step)
                : InputQaInputSystem.Mouse(step);
        }

        private static object StepSummary(int index, InputQaOptions step)
        {
            var mode = step.Mode ?? (step.Action == "keyboard" ? "press" : "click");
            return new
            {
                index,
                action = step.Action,
                mode,
                control = step.Action == "keyboard"
                    ? step.Key
                    : MouseControlName(step, mode),
            };
        }

        private static string MouseControlName(InputQaOptions step, string mode)
        {
            if (mode == "move")
                return "position";
            if (mode == "delta")
                return "delta";
            if (mode == "scroll")
                return "scroll";
            return InputQaInputSystem.ButtonName(step.Button);
        }

        private static void CancelActive()
        {
            activeSource?.Cancel();
        }
    }
}
