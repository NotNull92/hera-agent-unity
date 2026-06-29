using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace HeraAgent.Tools
{
    [HeraTool(Name = "menu", Description = "Execute a Unity menu item by path, or discover items: 'menu list' returns top-level groups, 'menu list --filter Assets' lists items under a group.")]
    public static class ExecuteMenuItem
    {
        private static readonly HashSet<string> Blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "File/Quit" };

        public class Parameters
        {
            [ToolParameter("Unity menu item path to execute (e.g. File/Save Project). Omit when action is 'list'.", Required = true)]
            public string MenuPath { get; set; }

            [ToolParameter("list: case-insensitive substring to match menu paths. Omit to get top-level groups instead of a flat list.")]
            public string Filter { get; set; }

            [ToolParameter("list: max items to return when filtering (default 300).")]
            public int? Limit { get; set; }
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

        // `menu list [--filter X] [--limit N]` — discover menu items declared via
        // [MenuItem]. Without a filter this returns top-level group counts rather
        // than a flat list, so a project with hundreds of items can't flood (or
        // silently truncate into) the agent's context. With a filter it returns a
        // bounded, explicitly-truncatable flat list.
        [HeraAction]
        public static object List(JObject raw)
        {
            var p = new ToolParams(raw);
            string filter = p.Get("filter");
            int limit = p.GetInt("limit") ?? 300;
            if (limit < 1) limit = 1;

            var paths = CollectMenuPaths();

            if (string.IsNullOrEmpty(filter))
            {
                var groups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in paths)
                {
                    int slash = path.IndexOf('/');
                    string top = slash > 0 ? path.Substring(0, slash) : path;
                    groups.TryGetValue(top, out int c);
                    groups[top] = c + 1;
                }

                var groupList = groups
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => (object)new { name = g.Key, count = g.Value })
                    .ToList();

                return new SuccessResponse(
                    $"{paths.Count} menu items in {groupList.Count} groups.",
                    new { total = paths.Count, groups = groupList })
                {
                    agent_hint = "Pass --filter <group> (e.g. --filter Assets) to list the items under a group.",
                };
            }

            var matched = new List<string>();
            int totalMatched = 0;
            foreach (var path in paths)
            {
                if (path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                totalMatched++;
                if (matched.Count < limit) matched.Add(path);
            }

            bool truncated = totalMatched > matched.Count;
            var resp = new SuccessResponse(
                $"{totalMatched} menu item(s) match '{filter}'.",
                new { filter, total = totalMatched, returned = matched.Count, truncated, items = matched });
            if (truncated)
                resp.agent_hint = $"Showing {matched.Count} of {totalMatched}. This list is INCOMPLETE — narrow --filter or raise --limit.";
            return resp;
        }

        // Distinct, executable [MenuItem] paths gathered via TypeCache (covers all
        // loaded assemblies without an AppDomain scan). Validation hooks and
        // component CONTEXT menus are excluded; native C++ menus (File/Save, …)
        // carry no [MenuItem] attribute and so are not listed.
        private static SortedSet<string> CollectMenuPaths()
        {
            var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var method in TypeCache.GetMethodsWithAttribute<MenuItem>())
            {
                foreach (var attr in method.GetCustomAttributes(typeof(MenuItem), false))
                {
                    var item = (MenuItem)attr;
                    if (item.validate) continue;
                    var path = item.menuItem;
                    if (string.IsNullOrEmpty(path)) continue;
                    if (path.StartsWith("CONTEXT/", StringComparison.OrdinalIgnoreCase)) continue;
                    if (path.StartsWith("internal:", StringComparison.OrdinalIgnoreCase)) continue;
                    paths.Add(path);
                }
            }
            return paths;
        }
    }
}
