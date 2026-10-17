using System.Collections.Generic;
using Newtonsoft.Json;

namespace HeraAgent
{
    public class SuccessResponse
    {
        public bool success = true;
        public string message;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object data;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string agent_hint;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<ToolContractDiagnostic> diagnostics;

        public Dictionary<string, long> timings;

        /// <summary>
        /// True when the failure is transient and the identical (idempotent) request may be
        /// retried. Omitted when false so payloads stay lean.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? retryable;

        /// <summary>Editor state observed while the response was produced (ready/compiling/reloading/...).</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string state;

        /// <summary>Suggested wait before retrying a retryable failure.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? retry_after_ms;

        public SuccessResponse(string message, object data = null)
        {
            this.message = message;
            this.data = data;
        }

        /// <summary>
        /// Adds a one-line agent hint, keeping any hint already attached. Several
        /// modes can contribute to the same response (game feel, UI juice, UI
        /// slop), so hints are appended rather than assigned — otherwise the last
        /// writer silently wins and the order of the calls becomes load-bearing.
        /// Null or empty hints are ignored.
        /// </summary>
        public SuccessResponse AppendHint(string hint)
        {
            if (string.IsNullOrEmpty(hint)) return this;
            agent_hint = string.IsNullOrEmpty(agent_hint) ? hint : agent_hint + "  " + hint;
            return this;
        }
    }

    public class ErrorResponse
    {
        public bool success = false;
        public string message;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string code;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> suggestions;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object data;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<ToolContractDiagnostic> diagnostics;

        public Dictionary<string, long> timings;

        /// <summary>
        /// True when the failure is transient and the identical (idempotent) request may be
        /// retried. Omitted when false so payloads stay lean.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? retryable;

        /// <summary>Editor state observed while the response was produced (ready/compiling/reloading/...).</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string state;

        /// <summary>Suggested wait before retrying a retryable failure.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? retry_after_ms;

        public ErrorResponse(string message, object data = null)
        {
            this.message = message;
            this.data = data;
        }

        /// <summary>
        /// Structured error: code is a stable enum-like string (e.g. EXEC_COMPILE_ERROR)
        /// agents can branch on without string-matching the message.
        /// </summary>
        public ErrorResponse(string code, string message, object data = null, List<string> suggestions = null)
        {
            this.code = code;
            this.message = message;
            this.data = data;
            this.suggestions = suggestions;
        }
    }

    /// <summary>
    /// Helper for attaching timing measurements to tool responses.
    /// Tolerates objects that aren't Success/ErrorResponse — no-op in that case.
    /// </summary>
    public static class ResponseTimings
    {
        public static void Set(object response, string key, long valueMs)
        {
            switch (response)
            {
                case SuccessResponse s:
                    if (s.timings == null) s.timings = new Dictionary<string, long>();
                    s.timings[key] = valueMs;
                    break;
                case ErrorResponse e:
                    if (e.timings == null) e.timings = new Dictionary<string, long>();
                    e.timings[key] = valueMs;
                    break;
            }
        }

        public static void Merge(object response, Dictionary<string, long> source)
        {
            if (source == null) return;
            foreach (var kv in source) Set(response, kv.Key, kv.Value);
        }
    }

    /// <summary>
    /// Fills the retry contract (retryable / state / retry_after_ms) on failures.
    /// Single source for the router, the HTTP ingress, and the reload fast-path so the
    /// CLI can branch on one contract instead of inferring transience from messages.
    /// </summary>
    public static class ResponsePolicy
    {
        /// <summary>Failure while the editor is compiling/reloading; not executed, safe to retry once ready.</summary>
        public const string CodeReloading = "RELOADING";

        static readonly HashSet<string> s_RetryableCodes = new HashSet<string>
        {
            CodeReloading,
            "COMMAND_LOCK_TIMEOUT",
            "HTTP_QUEUE_FULL",
            "HTTP_INTERNAL_ERROR",
        };

        public static bool IsRetryableCode(string code) =>
            !string.IsNullOrEmpty(code) && s_RetryableCodes.Contains(code);

        public static void Apply(ErrorResponse error, int? retryAfterMs = null)
        {
            if (error == null || !IsRetryableCode(error.code))
                return;
            error.retryable = true;
            if (string.IsNullOrEmpty(error.state))
                error.state = Heartbeat.CurrentState();
            error.retry_after_ms = retryAfterMs ?? (error.code == CodeReloading ? 1000 : 500);
        }
    }

    public static class ResponseDiagnostics
    {
        public static void Set(object response, IReadOnlyList<ToolContractDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return;
            var copy = new List<ToolContractDiagnostic>(diagnostics);
            switch (response)
            {
                case SuccessResponse success:
                    success.diagnostics = copy;
                    break;
                case ErrorResponse error:
                    error.diagnostics = copy;
                    break;
            }
        }
    }
}
