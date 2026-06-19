using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace HeraAgent.Tools
{
    [HeraTool(Name = "menu", Description = "Execute a Unity menu item by path.")]
    public static class ExecuteMenuItem
    {
        private static readonly HashSet<string> Blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "File/Quit" };

        public class Parameters
        {
            [ToolParameter("Unity menu item path to execute (e.g. File/Save Project)", Required = true)]
            public string MenuPath { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            string menuPath = p.Get("menu_path")
                ?? (p.GetRaw("args") as JArray)?[0]?.ToString();
            if (string.IsNullOrWhiteSpace(menuPath))
                return new ErrorResponse("MISSING_PARAM", "'menu_path' parameter required.");

            if (Blacklist.Contains(menuPath))
                return new ErrorResponse("MENU_BLOCKED", $"Execution of '{menuPath}' is blocked for safety.");

            bool executed = EditorApplication.ExecuteMenuItem(menuPath);
            if (!executed)
                return new ErrorResponse("MENU_EXECUTION_FAILED", $"Failed to execute menu item '{menuPath}'.");

            return new SuccessResponse($"Executed menu item: '{menuPath}'.");
        }
    }
}
