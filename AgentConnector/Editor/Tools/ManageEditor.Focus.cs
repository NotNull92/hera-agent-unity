using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraActionContract("focus", typeof(ManageEditor.FocusParameters), ResultType = typeof(ManageEditor.FocusResult), RiskClass = HeraRiskClass.Write)]
    [HeraArgumentGroup(ToolArgumentGroupMode.ExactlyOne, "type", "title", Action = "focus")]
    public static partial class ManageEditor
    {
        public sealed class FocusParameters
        {
            [ToolParameter("Exact loaded EditorWindow type name or full name.")]
            public string Type { get; set; }

            [ToolParameter("Exact EditorWindow title.")]
            public string Title { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class FocusResult
        {
            public string Type { get; set; }
            public string Title { get; set; }
        }

        private static object FocusWindow(ToolParams parameters)
        {
            var type = parameters.Get("type");
            var title = parameters.Get("title");
            if (string.IsNullOrEmpty(type) == string.IsNullOrEmpty(title))
                return new ErrorResponse("INVALID_PARAM", "Pass exactly one of 'type' or 'title'.");

            var matches = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Where(window => string.IsNullOrEmpty(type)
                    ? string.Equals(window.titleContent?.text, title, StringComparison.Ordinal)
                    : string.Equals(window.GetType().FullName, type, StringComparison.Ordinal)
                      || string.Equals(window.GetType().Name, type, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
                return new ErrorResponse("EDITOR_WINDOW_NOT_FOUND", $"No loaded EditorWindow matched {(type == null ? "title" : "type")} '{type ?? title}'.");
            if (matches.Length > 1)
                return new ErrorResponse("EDITOR_WINDOW_AMBIGUOUS", $"{matches.Length} loaded EditorWindows matched {(type == null ? "title" : "type")} '{type ?? title}'. Use the exact type full name.");

            var window = matches[0];
            window.Focus();
            return new SuccessResponse("Editor window focused.", new FocusResult
            {
                Type = window.GetType().FullName,
                Title = window.titleContent?.text,
            });
        }
    }
}
