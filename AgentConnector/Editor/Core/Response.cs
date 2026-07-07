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

        public Dictionary<string, long> timings;

        public SuccessResponse(string message, object data = null)
        {
            this.message = message;
            this.data = data;
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

        public Dictionary<string, long> timings;

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
}
