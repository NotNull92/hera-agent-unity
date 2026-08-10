using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent
{
    [InitializeOnLoad]
    internal static class InputQaRecording
    {
        internal const string Schema = "hera.input-recording/1";
        internal const int MaxEvents = 256;
        internal const int MaxFrames = 600;
        internal const int MaxDurationSeconds = 30;
        internal const int MaxFileBytes = 512 * 1024;

        private static Session current;

        static InputQaRecording()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                    current?.StopCapture("play_mode_exit");
            };
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (current == null)
                    return;
                try
                {
                    current.StopCapture("domain_reload");
                    current.Write();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[Hera] I couldn't save the active input recording before script reload: " +
                        ex.Message);
                }
                finally
                {
                    current = null;
                }
            };
        }

        internal static object Handle(JObject raw)
        {
            var mode = raw?["mode"]?.Value<string>()?.Trim().ToLowerInvariant();
            if (mode != "start" && raw?["path"] != null)
            {
                return new ErrorResponse(
                    "INPUT_RECORD_INVALID_ARGUMENT",
                    "[Hera] I accept 'path' only when input record mode is start.");
            }
            switch (mode)
            {
                case "start":
                    return Start(raw?["path"]?.Value<string>());
                case "stop":
                    return Stop();
                case "status":
                    return Status();
                default:
                    return new ErrorResponse(
                        "INPUT_RECORD_INVALID_MODE",
                        "[Hera] I need input record mode to be start, stop, or status.");
            }
        }

        private static object Start(string rawPath)
        {
            if (current != null)
                return new ErrorResponse(
                    "INPUT_RECORD_BUSY",
                    "[Hera] I already have an active or pending input recording. Stop it before starting another.");
            var source = InputQaInputSystem.CreateRecordingSource(out var sourceError);
            if (sourceError != null)
                return sourceError;
            if (!TryResolvePath(rawPath, false, out var path, out var pathError))
            {
                source.Dispose();
                return pathError;
            }

            try
            {
                current = new Session(source, path);
                current.Start();
                return new SuccessResponse("Input recording started", current.Status());
            }
            catch (Exception ex)
            {
                source.Dispose();
                current = null;
                return new ErrorResponse(
                    "INPUT_RECORD_START_FAILED",
                    "[Hera] I couldn't start input recording: " + ex.Message);
            }
        }

        private static object Stop()
        {
            if (current == null)
                return new ErrorResponse(
                    "INPUT_RECORD_NOT_RUNNING",
                    "[Hera] I don't have an input recording to stop.");
            try
            {
                current.StopCapture("requested");
                var result = current.Write();
                current = null;
                return new SuccessResponse("Input recording saved", result);
            }
            catch (Exception ex)
            {
                return new ErrorResponse(
                    "INPUT_RECORD_WRITE_FAILED",
                    "[Hera] I couldn't save the input recording: " + ex.Message,
                    current.Status());
            }
        }

        private static object Status()
        {
            return new SuccessResponse(
                "Input recording status",
                current?.Status() ?? new
                {
                    schema = Schema,
                    active = false,
                    pending_save = false,
                });
        }

        internal static bool TryResolvePath(
            string rawPath,
            bool mustExist,
            out string path,
            out ErrorResponse error)
        {
            path = null;
            error = null;
            try
            {
                var requested = rawPath;
                if (string.IsNullOrWhiteSpace(requested))
                {
                    requested = Path.Combine(
                        "Library",
                        "HeraAgent",
                        "Recordings",
                        "input-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                        + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
                }
                path = Path.IsPathRooted(requested)
                    ? Path.GetFullPath(requested)
                    : Path.GetFullPath(Path.Combine(ProjectIdentity.CurrentRoot, requested));
                if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    error = PathError("Input recording paths must end with .json.");
                    return false;
                }
                if (!OutputFilePolicy.IsUnderTrustedRoot(path))
                {
                    error = PathError(
                        "Input recording paths must stay under the Unity project or system temp directory.");
                    return false;
                }
                if (mustExist && !File.Exists(path))
                {
                    error = new ErrorResponse(
                        "INPUT_RECORDING_NOT_FOUND",
                        "[Hera] I couldn't find the input recording file: " + path);
                    return false;
                }
                if (!mustExist && File.Exists(path))
                {
                    error = new ErrorResponse(
                        "INPUT_RECORDING_PATH_EXISTS",
                        "[Hera] I won't overwrite an existing input recording: " + path);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = PathError(ex.Message);
                return false;
            }
        }

        private static ErrorResponse PathError(string message)
        {
            return new ErrorResponse(
                "INPUT_RECORDING_INVALID_PATH",
                "[Hera] I rejected the input recording path: " + message);
        }

        private sealed class Session
        {
            private readonly InputQaInputSystem.InputQaRecordingSource source;
            private readonly string path;
            private readonly List<JObject> events = new List<JObject>();
            private readonly HashSet<string> pressedKeys =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> pressedButtons =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly int startFrame;
            private readonly double startTime;
            private Vector2? position;
            private bool active;
            private string stopReason;
            private int lastRelativeFrame;

            internal Session(InputQaInputSystem.InputQaRecordingSource source, string path)
            {
                this.source = source;
                this.path = path;
                startFrame = Time.frameCount;
                startTime = EditorApplication.timeSinceStartup;
            }

            internal void Start()
            {
                active = true;
                Sample();
                source.Subscribe(Sample);
            }

            internal void StopCapture(string reason)
            {
                if (!active)
                    return;
                active = false;
                stopReason = reason;
                source.Dispose();
            }

            internal object Status()
            {
                return new
                {
                    schema = Schema,
                    active,
                    pending_save = !active,
                    path,
                    event_count = events.Count,
                    total_frames = lastRelativeFrame,
                    elapsed_ms = (long)((EditorApplication.timeSinceStartup - startTime) * 1000d),
                    stop_reason = stopReason,
                    update_type = source.UpdateType,
                };
            }

            internal object Write()
            {
                var root = new JObject
                {
                    ["schema"] = Schema,
                    ["metadata"] = new JObject
                    {
                        ["recorded_at"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                        ["unity_version"] = Application.unityVersion,
                        ["inputsystem_version"] = source.PackageVersion,
                        ["update_type"] = source.UpdateType,
                        ["total_frames"] = lastRelativeFrame,
                        ["duration_ms"] = (long)((EditorApplication.timeSinceStartup - startTime) * 1000d),
                        ["event_count"] = events.Count,
                        ["stop_reason"] = stopReason ?? "requested",
                    },
                    ["events"] = new JArray(events),
                };
                var bytes = Encoding.UTF8.GetBytes(root.ToString(Formatting.None));
                if (bytes.Length > MaxFileBytes)
                    throw new InvalidOperationException(
                        $"The recording is {bytes.Length} bytes; the limit is {MaxFileBytes} bytes.");
                OutputFilePolicy.WriteAllBytes(path, bytes, false);
                return new
                {
                    schema = Schema,
                    path,
                    event_count = events.Count,
                    total_frames = lastRelativeFrame,
                    bytes = bytes.Length,
                    stop_reason = stopReason ?? "requested",
                };
            }

            private void Sample()
            {
                if (!active)
                    return;
                try
                {
                    var relativeFrame = Math.Max(0, Time.frameCount - startFrame);
                    lastRelativeFrame = relativeFrame;
                    if (relativeFrame > MaxFrames
                        || EditorApplication.timeSinceStartup - startTime > MaxDurationSeconds)
                    {
                        StopCapture("duration_limit");
                        return;
                    }

                    var snapshot = source.Capture();
                    CaptureButtons(snapshot.Keys, pressedKeys, relativeFrame, "keyboard", "key");
                    CaptureButtons(snapshot.MouseButtons, pressedButtons, relativeFrame, "mouse", "button");
                    if (!position.HasValue || snapshot.Position != position.Value)
                    {
                        Add(relativeFrame, new JObject
                        {
                            ["action"] = "mouse",
                            ["mode"] = "move",
                            ["position"] = Vector(snapshot.Position),
                        });
                        position = snapshot.Position;
                    }
                    if (snapshot.Delta.sqrMagnitude > 0f)
                    {
                        Add(relativeFrame, new JObject
                        {
                            ["action"] = "mouse",
                            ["mode"] = "delta",
                            ["delta"] = Vector(snapshot.Delta),
                        });
                    }
                    if (snapshot.Scroll.sqrMagnitude > 0f)
                    {
                        Add(relativeFrame, new JObject
                        {
                            ["action"] = "mouse",
                            ["mode"] = "scroll",
                            ["scroll_delta"] = Vector(snapshot.Scroll),
                        });
                    }
                }
                catch (Exception ex)
                {
                    stopReason = "capture_error: " + ex.Message;
                    StopCapture(stopReason);
                }
            }

            private void CaptureButtons(
                IDictionary<string, bool> values,
                ISet<string> pressed,
                int frame,
                string action,
                string field)
            {
                foreach (var pair in values)
                {
                    var wasPressed = pressed.Contains(pair.Key);
                    if (pair.Value == wasPressed)
                        continue;
                    if (pair.Value)
                        pressed.Add(pair.Key);
                    else
                        pressed.Remove(pair.Key);
                    Add(frame, new JObject
                    {
                        ["action"] = action,
                        ["mode"] = pair.Value ? "down" : "up",
                        [field] = pair.Key,
                    });
                }
            }

            private void Add(int frame, JObject inputEvent)
            {
                if (!active || events.Count >= MaxEvents)
                {
                    StopCapture("event_limit");
                    return;
                }
                inputEvent.AddFirst(new JProperty("frame", frame));
                events.Add(inputEvent);
                if (events.Count >= MaxEvents)
                    StopCapture("event_limit");
            }

            private static string Vector(Vector2 value)
            {
                return value.x.ToString("R", CultureInfo.InvariantCulture) + "," +
                    value.y.ToString("R", CultureInfo.InvariantCulture);
            }
        }
    }
}
