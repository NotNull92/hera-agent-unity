using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HeraAgent.Tests
{
    internal static class InputQaSequenceTests
    {
        internal static bool RunContractTests()
        {
            var allPassed = true;
            allPassed &= ExpectError(
                "sequence requires steps",
                new JObject { ["action"] = "sequence" },
                "INPUT_SEQUENCE_INVALID_STEPS");
            allPassed &= ExpectError(
                "sequence rejects an empty step list",
                SequenceRequest(new JArray()),
                "INPUT_SEQUENCE_INVALID_STEPS");
            allPassed &= ExpectError(
                "sequence rejects more than 32 steps",
                SequenceRequest(RepeatedKeyboardSteps(33)),
                "INPUT_SEQUENCE_LIMIT_EXCEEDED");
            allPassed &= ExpectError(
                "sequence rejects non-object steps",
                SequenceRequest(new JArray("keyboard")),
                "INPUT_SEQUENCE_INVALID_STEP");
            allPassed &= ExpectError(
                "sequence rejects read-only actions",
                SequenceRequest(new JArray(new JObject { ["action"] = "state" })),
                "INPUT_SEQUENCE_UNSUPPORTED_ACTION");
            allPassed &= ExpectError(
                "sequence rejects an unknown action before execution",
                SequenceRequest(new JArray(new JObject { ["action"] = "teleport" })),
                "INPUT_SEQUENCE_UNSUPPORTED_ACTION");
            allPassed &= ExpectError(
                "sequence validates keyboard mode before execution",
                SequenceRequest(new JArray(new JObject
                {
                    ["action"] = "keyboard",
                    ["key"] = "space",
                    ["mode"] = "move",
                })),
                "INPUT_SEQUENCE_INVALID_STEP");
            allPassed &= ExpectError(
                "sequence validates mouse vectors before execution",
                SequenceRequest(new JArray(new JObject
                {
                    ["action"] = "mouse",
                    ["mode"] = "move",
                })),
                "INPUT_SEQUENCE_INVALID_STEP");
            allPassed &= ExpectError(
                "sequence rejects duplicate key ownership before execution",
                SequenceRequest(new JArray(
                    KeyboardStep("space", "down"),
                    KeyboardStep("space", "down"))),
                "INPUT_SEQUENCE_OWNERSHIP_INVALID");
            allPassed &= ExpectError(
                "sequence rejects unowned key release before execution",
                SequenceRequest(new JArray(KeyboardStep("space", "up"))),
                "INPUT_SEQUENCE_OWNERSHIP_INVALID");
            allPassed &= ExpectError(
                "sequence rejects nested sequences",
                SequenceRequest(new JArray(new JObject
                {
                    ["action"] = "sequence",
                    ["steps"] = new JArray(),
                })),
                "INPUT_SEQUENCE_UNSUPPORTED_ACTION");
            allPassed &= ExpectError(
                "sequence bounds cumulative hold time",
                SequenceRequest(RepeatedKeyboardSteps(7, holdMs: 5000)),
                "INPUT_SEQUENCE_DURATION_EXCEEDED");
            allPassed &= ExpectError(
                "sequence bounds cumulative settle frames",
                SequenceRequest(RepeatedKeyboardSteps(6, settleFrames: 120)),
                "INPUT_SEQUENCE_DURATION_EXCEEDED");

            var (plan, error) = InputQaSequence.Parse(SequenceRequest(new JArray(
                new JObject
                {
                    ["action"] = "keyboard",
                    ["key"] = "space",
                    ["mode"] = "press",
                    ["hold_ms"] = 0,
                    ["settle_frames"] = 0,
                },
                new JObject
                {
                    ["action"] = "mouse",
                    ["mode"] = "move",
                    ["position"] = "10,20",
                    ["settle_frames"] = 0,
                })));
            allPassed &= ExpectTrue(
                "sequence parses every step before execution",
                error == null
                && plan?.Steps.Count == 2
                && plan.TotalHoldMs == 0);
            var (grouped, groupError) = InputQaSequence.Parse(SequenceRequest(new JArray(
                KeyboardStep("leftshift", "down"),
                KeyboardStep("a", "down"),
                new JObject { ["action"] = "mouse", ["mode"] = "scroll", ["scroll_delta"] = "0,3", ["settle_frames"] = 1 },
                KeyboardStep("a", "up"),
                KeyboardStep("leftshift", "up"))));
            allPassed &= ExpectTrue("zero-settle chord stays in one group before a nonzero gap",
                groupError == null && InputQaSequence.GroupEnd(grouped.Steps, 0) == 2
                && InputQaSequence.GroupEnd(grouped.Steps, 3) == 4);

            var (pulses, pulseError) = InputQaSequence.Parse(SequenceRequest(new JArray(
                KeyboardStep("leftshift", "down"),
                KeyboardStep("a", "press"),
                new JObject { ["action"] = "mouse", ["mode"] = "click", ["settle_frames"] = 0, ["hold_ms"] = 20 },
                KeyboardStep("leftshift", "up"))));
            allPassed &= ExpectTrue("press and click keep their hold/release boundaries at settle zero",
                pulseError == null && InputQaSequence.GroupEnd(pulses.Steps, 0) == 0
                && InputQaSequence.GroupEnd(pulses.Steps, 1) == 1
                && InputQaSequence.GroupEnd(pulses.Steps, 2) == 2
                && InputQaSequence.GroupEnd(pulses.Steps, 3) == 3);
            return allPassed;
        }

        private static JObject SequenceRequest(JArray steps)
        {
            return new JObject
            {
                ["action"] = "sequence",
                ["steps"] = steps,
            };
        }

        private static JArray RepeatedKeyboardSteps(
            int count,
            int holdMs = 0,
            int settleFrames = 0)
        {
            var steps = new JArray();
            for (var index = 0; index < count; index++)
            {
                steps.Add(new JObject
                {
                    ["action"] = "keyboard",
                    ["key"] = "space",
                    ["hold_ms"] = holdMs,
                    ["settle_frames"] = settleFrames,
                });
            }
            return steps;
        }

        private static JObject KeyboardStep(string key, string mode)
        {
            return new JObject
            {
                ["action"] = "keyboard",
                ["key"] = key,
                ["mode"] = mode,
                ["hold_ms"] = 0,
                ["settle_frames"] = 0,
            };
        }

        private static bool ExpectError(string label, JObject request, string expectedCode)
        {
            var response = InputTool.HandleCommand(request).GetAwaiter().GetResult()
                as ErrorResponse;
            return ExpectTrue(label, response?.code == expectedCode);
        }

        private static bool ExpectTrue(string label, bool actual)
        {
            if (actual)
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError("[FAIL] " + label);
            return false;
        }
    }
}
