using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HeraAgent.Tests
{
    internal static class InputQaRecordingTests
    {
        internal static bool RunContractTests()
        {
            var allPassed = true;
            var directory = Path.Combine(
                Path.GetTempPath(),
                "hera-input-recording-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var valid = Path.Combine(directory, "valid.json");
                Write(valid, Recording(new JArray(
                    MouseMove(0, "10,20"),
                    Keyboard(2, "space", "down"),
                    Keyboard(4, "space", "up"))));
                var (plan, error, source) = InputQaReplay.Load(valid);
                allPassed &= ExpectTrue(
                    "recording loader preserves frame timing",
                    error == null
                    && source != null
                    && plan?.Steps.Count == 3
                    && plan.Steps[0].SettleFrames == 2
                    && plan.Steps[1].SettleFrames == 2
                    && plan.TotalAwaitedFrames == 4);

                var unsupported = Path.Combine(directory, "unsupported.json");
                var unsupportedRoot = Recording(new JArray(MouseMove(0, "1,2")));
                unsupportedRoot["schema"] = "hera.input-recording/2";
                Write(unsupported, unsupportedRoot);
                allPassed &= ExpectError(
                    "recording loader rejects unknown schema",
                    unsupported,
                    "INPUT_RECORDING_SCHEMA_UNSUPPORTED");

                var unordered = Path.Combine(directory, "unordered.json");
                Write(unordered, Recording(new JArray(
                    MouseMove(0, "1,2"),
                    MouseMove(2, "2,3"),
                    MouseMove(1, "3,4"))));
                allPassed &= ExpectError(
                    "recording loader rejects decreasing frames",
                    unordered,
                    "INPUT_RECORDING_INVALID_EVENT");

                var unowned = Path.Combine(directory, "unowned.json");
                Write(unowned, Recording(new JArray(Keyboard(0, "space", "up"))));
                allPassed &= ExpectError(
                    "recording preflight rejects unowned releases",
                    unowned,
                    "INPUT_RECORDING_PREFLIGHT_FAILED");

                var nonFinite = Path.Combine(directory, "non-finite.json");
                Write(nonFinite, Recording(new JArray(MouseMove(0, "NaN,0"))));
                allPassed &= ExpectError(
                    "recording loader rejects non-finite vectors",
                    nonFinite,
                    "INPUT_RECORDING_INVALID_EVENT");

                var oversizedEvents = Path.Combine(directory, "too-many-events.json");
                var eventList = new JArray();
                for (var index = 0; index <= InputQaRecording.MaxEvents; index++)
                    eventList.Add(MouseMove(index, index + ",0"));
                Write(oversizedEvents, Recording(eventList));
                allPassed &= ExpectError(
                    "recording loader enforces the event limit before parsing",
                    oversizedEvents,
                    "INPUT_RECORDING_EVENT_LIMIT_EXCEEDED");

                var existing = Path.Combine(directory, "existing.json");
                File.WriteAllText(existing, "{}");
                allPassed &= ExpectTrue(
                    "recording output never overwrites existing files",
                    !InputQaRecording.TryResolvePath(
                        existing,
                        false,
                        out _,
                        out var pathError)
                    && pathError?.code == "INPUT_RECORDING_PATH_EXISTS");
                allPassed &= ExpectTrue(
                    "recording output rejects non-JSON paths",
                    !InputQaRecording.TryResolvePath(
                        Path.Combine(directory, "recording.txt"),
                        false,
                        out _,
                        out var extensionError)
                    && extensionError?.code == "INPUT_RECORDING_INVALID_PATH");
                var invalidStop = InputQaRecording.Handle(new JObject
                {
                    ["action"] = "record",
                    ["mode"] = "status",
                    ["path"] = existing,
                }) as ErrorResponse;
                allPassed &= ExpectTrue(
                    "recording status rejects start-only path",
                    invalidStop?.code == "INPUT_RECORD_INVALID_ARGUMENT");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
            return allPassed;
        }

        private static JObject Recording(JArray events)
        {
            return new JObject
            {
                ["schema"] = InputQaRecording.Schema,
                ["metadata"] = new JObject { ["update_type"] = "dynamic" },
                ["events"] = events,
            };
        }

        private static JObject MouseMove(int frame, string position)
        {
            return new JObject
            {
                ["frame"] = frame,
                ["action"] = "mouse",
                ["mode"] = "move",
                ["position"] = position,
            };
        }

        private static JObject Keyboard(int frame, string key, string mode)
        {
            return new JObject
            {
                ["frame"] = frame,
                ["action"] = "keyboard",
                ["key"] = key,
                ["mode"] = mode,
            };
        }

        private static void Write(string path, JObject value)
        {
            File.WriteAllText(path, value.ToString(Formatting.None));
        }

        private static bool ExpectError(string label, string path, string code)
        {
            var (_, error, _) = InputQaReplay.Load(path);
            return ExpectTrue(label, error?.code == code);
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
