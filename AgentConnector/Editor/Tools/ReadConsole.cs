using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraTool(Name = "console", Description = "Read or clear Unity console logs.")]
    public static class ReadConsole
    {
        private static MethodInfo _startGettingEntriesMethod, _endGettingEntriesMethod, _clearMethod, _getCountMethod, _getEntryMethod;
        private static FieldInfo _modeField, _messageField, _fileField, _lineField;
        private static Type _logEntryType;

        /// <summary>
        /// console relies on the UnityEditor internal LogEntries API because Unity
        /// does not expose a public way to read the console programmatically. The
        /// reflection results are cached; if the internal shape changes across a
        /// Unity version, ReadConsole gracefully degrades to READCONSOLE_INIT_FAILED.
        /// </summary>
        static ReadConsole()
        {
            try
            {
                Type logEntriesType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
                if (logEntriesType == null) throw new Exception("Could not find UnityEditor.LogEntries");
                BindingFlags sf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                BindingFlags inf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                _startGettingEntriesMethod = logEntriesType.GetMethod("StartGettingEntries", sf)
                    ?? throw new Exception("Method not found: LogEntries.StartGettingEntries");
                _endGettingEntriesMethod = logEntriesType.GetMethod("EndGettingEntries", sf)
                    ?? throw new Exception("Method not found: LogEntries.EndGettingEntries");
                _clearMethod = logEntriesType.GetMethod("Clear", sf)
                    ?? throw new Exception("Method not found: LogEntries.Clear");
                _getCountMethod = logEntriesType.GetMethod("GetCount", sf)
                    ?? throw new Exception("Method not found: LogEntries.GetCount");
                _getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", sf)
                    ?? throw new Exception("Method not found: LogEntries.GetEntryInternal");

                _logEntryType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntry")
                    ?? throw new Exception("Could not find UnityEditor.LogEntry");
                _modeField = _logEntryType.GetField("mode", inf)
                    ?? throw new Exception("Field not found: LogEntry.mode");
                _messageField = _logEntryType.GetField("message", inf)
                    ?? throw new Exception("Field not found: LogEntry.message");
                _fileField = _logEntryType.GetField("file", inf);
                _lineField = _logEntryType.GetField("line", inf);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Hera] ReadConsole init failed: {e.Message}");
                _startGettingEntriesMethod = _endGettingEntriesMethod = _clearMethod = _getCountMethod = _getEntryMethod = null;
                _modeField = _messageField = _fileField = _lineField = null;
                _logEntryType = null;
            }
        }

        public class Parameters
        {
            [ToolParameter("Comma-separated log types: error, warning, log. Default: error,warning,log")]
            public string Type { get; set; }

            [ToolParameter("Maximum number of log entries to return")]
            public int Lines { get; set; }

            [ToolParameter("Stack trace mode: none (first line), user (user code frames only), full (raw). Default: user")]
            public string Stacktrace { get; set; }

            [ToolParameter("Clear console")]
            public bool Clear { get; set; }

            [ToolParameter("Return only entries with index >= since. Use last_cursor from prior response.")]
            public int Since { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (_startGettingEntriesMethod == null || _endGettingEntriesMethod == null ||
                _clearMethod == null || _getCountMethod == null || _getEntryMethod == null ||
                _modeField == null || _messageField == null || _logEntryType == null)
            {
                var missing = new List<string>();
                if (_startGettingEntriesMethod == null) missing.Add("LogEntries.StartGettingEntries");
                if (_endGettingEntriesMethod == null) missing.Add("LogEntries.EndGettingEntries");
                if (_clearMethod == null) missing.Add("LogEntries.Clear");
                if (_getCountMethod == null) missing.Add("LogEntries.GetCount");
                if (_getEntryMethod == null) missing.Add("LogEntries.GetEntryInternal");
                if (_modeField == null) missing.Add("LogEntry.mode");
                if (_messageField == null) missing.Add("LogEntry.message");
                if (_logEntryType == null) missing.Add("LogEntry (type)");
                return new ErrorResponse("READCONSOLE_INIT_FAILED",
                    "ReadConsole failed to initialize (reflection error).",
                    data: new { missing_members = missing, unity_version = Application.unityVersion });
            }

            if (@params == null)
                return new ErrorResponse("MISSING_PARAM", "Parameters cannot be null.");

            var p = new ToolParams(@params);

            // --clear
            if (p.GetBool("clear"))
            {
                _clearMethod.Invoke(null, null);
                return new SuccessResponse("Console cleared.");
            }

            var type = p.Get("type", "error,warning,log").ToLower();
            var types = type.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

            int? count = p.GetInt("lines") ?? p.GetInt("count") ?? 20;
            string stacktrace = p.Get("stacktrace", "user").ToLower();
            int since = p.GetInt("since") ?? 0;

            return GetEntries(types, count, stacktrace, since);
        }

        private static object GetEntries(List<string> types, int? count, string stacktrace, int since)
        {
            var entries = new List<string>();
            int total = 0;
            int filteredTotal = 0;
            int lastIndex = since;
            bool truncated = false;
            try
            {
                _startGettingEntriesMethod.Invoke(null, null);
                total = (int)_getCountMethod.Invoke(null, null);
                object logEntry = Activator.CreateInstance(_logEntryType);

                for (int i = since; i < total; i++)
                {
                    _getEntryMethod.Invoke(null, new object[] { i, logEntry });
                    int mode = (int)_modeField.GetValue(logEntry);
                    string message = (string)_messageField.GetValue(logEntry);
                    if (string.IsNullOrEmpty(message)) continue;

                    LogType logType = GetLogTypeFromMode(mode);
                    bool want = logType == LogType.Exception || logType == LogType.Assert
                        ? types.Contains("error")
                        : types.Contains(logType.ToString().ToLowerInvariant());

                    if (!want) continue;

                    filteredTotal++;
                    if (count.HasValue && entries.Count > count.Value)
                    {
                        truncated = true;
                        continue;
                    }

                    entries.Add(FormatMessage(message, stacktrace));
                    lastIndex = i + 1; // cursor advances past the last returned entry
                }
            }
            finally
            {
                try { _endGettingEntriesMethod.Invoke(null, null); } catch { }
            }

            return new SuccessResponse($"Retrieved {entries.Count} entries.", new
            {
                entries,
                total_in_console = total,
                matched = filteredTotal,
                returned = entries.Count,
                since,
                last_cursor = lastIndex,
                truncated,
            });
        }

        private static string FormatMessage(string message, string mode)
        {
            switch (mode)
            {
                case "full":
                    return message;

                case "user":
                    var lines = message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var sb = new System.Text.StringBuilder();
                    foreach (var line in lines)
                    {
                        // Skip framework / runtime noise so the agent reads
                        // the user's own frames first. Console stack traces
                        // use "Type:Method (args)" format (different from the
                        // "at Type.Method" format that ExecuteCsharp filters),
                        // so this list mirrors that shape.
                        if (line.Contains("UnityEngine.Debug:") ||
                            line.Contains("UnityEditor.EditorGUIUtility:") ||
                            line.Contains("Unity.Entities.SystemState:") ||
                            line.Contains("(at Library/") ||
                            line.Contains("(at ./Library/") ||
                            // hera-agent-unity's own dispatch + the exec wrapper —
                            // never user-actionable, always noise in console traces.
                            line.Contains("__CliDynamic:") ||
                            line.Contains("HeraAgent.CommandRouter:") ||
                            line.Contains("HeraAgent.HttpServer:") ||
                            // Reflection + async machinery that wraps every
                            // exec call regardless of user code shape.
                            line.Contains("System.Reflection.MethodBase:Invoke") ||
                            line.Contains("System.Runtime.CompilerServices.AsyncTaskMethodBuilder") ||
                            line.Contains("UnityEditor.EditorApplication:Internal_CallUpdateFunctions"))
                            continue;
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append(line);
                    }
                    return sb.ToString();

                default: // "none"
                    string[] firstLine = message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    return firstLine.Length > 0 ? firstLine[0] : message;
            }
        }

        private const int ErrorMask =
            (1 << 0)  |  // Error
            (1 << 6)  |  // AssetImportError
            (1 << 8)  |  // ScriptingError
            (1 << 11) |  // ScriptCompileError
            (1 << 13);   // StickyError

        private const int WarningMask =
            (1 << 7)  |  // AssetImportWarning
            (1 << 9)  |  // ScriptingWarning
            (1 << 12);   // ScriptCompileWarning

        private const int ExceptionMask =
            (1 << 1)  |  // Assert
            (1 << 4)  |  // Fatal
            (1 << 17) |  // ScriptingException
            (1 << 21);   // ScriptingAssertion

        private static LogType GetLogTypeFromMode(int mode)
        {
            if ((mode & ExceptionMask) != 0) return LogType.Exception;
            if ((mode & ErrorMask) != 0) return LogType.Error;
            if ((mode & WarningMask) != 0) return LogType.Warning;
            return LogType.Log;
        }
    }
}
