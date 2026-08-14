using System;
using System.Linq;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static HeraAgent.Tests.ToolResponseTestSupport;

namespace HeraAgent.Tests
{
    public static class EditorUiCaptureTests
    {
        public static void RunTests()
        {
            Assert.IsNotNull(
                ToolContractRegistry.Get("screenshot").InputSchema["properties"]?["editor_ui_only"],
                "screenshot must publish the editor_ui_only contract before the runtime assertions run.");

            var window = ScriptableObject.CreateInstance<EditorUiCaptureWindow>();
            window.titleContent = new GUIContent("Hera Editor UI Capture Test");
            try
            {
                var container = new VisualElement { name = "hera-container" };
                container.Add(new Button { name = "hera-button", text = "Run" });
                container.Add(new Label("Status") { name = "hera-label" });
                window.rootVisualElement.Add(container);
                window.Show();

                var data = RequireSuccess(EditorScreenshot.HandleCommand(new JObject
                {
                    ["editor_ui_only"] = true,
                    ["editor_window"] = typeof(EditorUiCaptureWindow).FullName,
                    ["editor_selector"] = "#hera-button",
                    ["max_editor_elements"] = 1,
                }));
                Assert.IsFalse(data.Value<bool>("pixels_requested"));
                Assert.AreEqual(typeof(EditorUiCaptureWindow).FullName, data["window"]?.Value<string>("type"));
                Assert.AreEqual(1, data.Value<int>("returned"));
                Assert.LessOrEqual(data["elements"]?.Count(), 1);
                var element = data["elements"]?[0] as JObject;
                Assert.AreEqual("Button", element?.Value<string>("type"));
                Assert.AreEqual("hera-button", element?.Value<string>("name"));
                Assert.IsTrue(element?.Value<string>("hierarchy_path")?.EndsWith("/#hera-button", StringComparison.Ordinal));
                Assert.AreEqual(JTokenType.Boolean, element?["visible"]?.Type);
                Assert.AreEqual(JTokenType.Boolean, element?["enabled"]?.Type);
                Assert.AreEqual(4, element?["layout"]?.Count());

                RequireError(EditorScreenshot.HandleCommand(new JObject
                {
                    ["editor_ui_only"] = true,
                    ["editor_window"] = "__MissingEditorWindow",
                }), "SCREENSHOT_EDITOR_WINDOW_NOT_FOUND");
                RequireError(EditorScreenshot.HandleCommand(new JObject
                {
                    ["editor_ui_only"] = true,
                    ["editor_window"] = typeof(EditorUiCaptureWindow).FullName,
                    ["max_editor_elements"] = 0,
                }), "SCREENSHOT_INVALID_MAX_EDITOR_ELEMENTS");
            }
            finally
            {
                window.Close();
            }
        }

        public sealed class EditorUiCaptureWindow : EditorWindow
        {
        }
    }
}
