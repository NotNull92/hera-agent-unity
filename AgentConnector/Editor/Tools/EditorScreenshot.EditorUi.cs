using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeraAgent.Tools
{
    public static partial class EditorScreenshot
    {
        private static object CollectEditorUi(ToolParams parameters)
        {
            var windowName = parameters.Get("editor_window");
            if (string.IsNullOrWhiteSpace(windowName))
                return new ErrorResponse("SCREENSHOT_EDITOR_WINDOW_REQUIRED", "'editor_window' is required when editor_ui_only is true.");

            var limit = parameters.GetInt("max_editor_elements") ?? 100;
            if (limit < 1 || limit > 500)
                return new ErrorResponse("SCREENSHOT_INVALID_MAX_EDITOR_ELEMENTS", "'max_editor_elements' must be 1-500.");

            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var window = windows.FirstOrDefault(candidate => WindowMatches(candidate, windowName)
                    && candidate.rootVisualElement?.panel != null)
                ?? windows.FirstOrDefault(candidate => WindowMatches(candidate, windowName));
            if (window == null)
                return new ErrorResponse("SCREENSHOT_EDITOR_WINDOW_NOT_FOUND", $"No loaded EditorWindow matched '{windowName}'.");

            var root = window.rootVisualElement;
            if (root == null)
                return new ErrorResponse("SCREENSHOT_EDITOR_UI_UNAVAILABLE", $"EditorWindow '{windowName}' has no rootVisualElement.");

            var selector = parameters.Get("editor_selector");
            if (!string.IsNullOrEmpty(selector) && selector[0] == '#' && selector.Length == 1)
                return new ErrorResponse("SCREENSHOT_INVALID_EDITOR_SELECTOR", "'#' must be followed by an element name.");

            var matched = new List<object>(Math.Min(limit, 100));
            var total = 0;
            TraverseEditorUi(root, "/" + EditorUiSegment(root, 0), selector, limit, matched, ref total);
            if (total == 0 && !string.IsNullOrEmpty(selector))
                return new ErrorResponse("SCREENSHOT_EDITOR_ELEMENT_NOT_FOUND", $"No UI Toolkit element matched '{selector}' in '{windowName}'.");

            return new SuccessResponse("Editor UI metadata collected without capturing pixels.", new
            {
                pixels_requested = false,
                window = new
                {
                    type = window.GetType().FullName,
                    title = window.titleContent?.text,
                },
                selector,
                total,
                returned = matched.Count,
                truncated = total > matched.Count,
                elements = matched.ToArray(),
            });
        }

        private static void TraverseEditorUi(
            VisualElement element,
            string path,
            string selector,
            int limit,
            List<object> matched,
            ref int total)
        {
            if (EditorUiMatches(element, selector))
            {
                total++;
                if (matched.Count < limit)
                {
                    var layout = element.layout;
                    matched.Add(new
                    {
                        type = element.GetType().Name,
                        name = string.IsNullOrEmpty(element.name) ? null : element.name,
                        hierarchy_path = path,
                        visible = element.visible && element.resolvedStyle.display != DisplayStyle.None,
                        enabled = element.enabledInHierarchy,
                        layout = new[]
                        {
                            Finite(layout.x),
                            Finite(layout.y),
                            Finite(layout.width),
                            Finite(layout.height),
                        },
                    });
                }
            }

            for (var i = 0; i < element.childCount; i++)
            {
                var child = element[i];
                TraverseEditorUi(child, path + "/" + EditorUiSegment(child, i), selector, limit, matched, ref total);
            }
        }

        private static bool WindowMatches(EditorWindow window, string name)
        {
            return string.Equals(window.GetType().FullName, name, StringComparison.Ordinal)
                || string.Equals(window.GetType().Name, name, StringComparison.Ordinal)
                || string.Equals(window.titleContent?.text, name, StringComparison.Ordinal);
        }

        private static bool EditorUiMatches(VisualElement element, string selector)
        {
            if (string.IsNullOrEmpty(selector)) return true;
            if (selector[0] == '#')
                return string.Equals(element.name, selector.Substring(1), StringComparison.Ordinal);
            return string.Equals(element.GetType().Name, selector, StringComparison.Ordinal)
                || string.Equals(element.GetType().FullName, selector, StringComparison.Ordinal);
        }

        private static string EditorUiSegment(VisualElement element, int siblingIndex)
        {
            return string.IsNullOrEmpty(element.name)
                ? element.GetType().Name + "[" + siblingIndex + "]"
                : "#" + element.name;
        }

        private static float Finite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
