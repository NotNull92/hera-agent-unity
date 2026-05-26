using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraTool(Name = "log", Description = "Write a message to the Unity console. Faster than exec for simple Debug.Log markers — no csc compile cost.")]
    public static class LogToConsole
    {
        public class Parameters
        {
            [ToolParameter("Message body to log.", Required = true)]
            public string Message { get; set; }

            [ToolParameter("Log level: log (default), warning, error.")]
            public string Level { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            var p = new ToolParams(parameters);
            var message = p.Get("message")
                ?? (p.GetRaw("args") as JArray)?[0]?.ToString();
            if (string.IsNullOrEmpty(message))
                return new ErrorResponse("MISSING_PARAM", "'message' required",
                    suggestions: new List<string> { "Pass message as positional arg or --message <text>" });

            var level = (p.Get("level") ?? "log").ToLowerInvariant();
            switch (level)
            {
                case "warning":
                case "warn":
                    Debug.LogWarning(message);
                    break;
                case "error":
                case "err":
                    Debug.LogError(message);
                    break;
                case "log":
                case "info":
                case "":
                    Debug.Log(message);
                    break;
                default:
                    return new ErrorResponse("INVALID_PARAM",
                        $"Unknown level '{level}'. Valid: log, warning, error.");
            }

            return new SuccessResponse("logged", new { message, level });
        }
    }
}
