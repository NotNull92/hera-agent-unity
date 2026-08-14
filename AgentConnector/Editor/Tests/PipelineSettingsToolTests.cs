using System;
using System.Linq;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#pragma warning disable CS0618

namespace HeraAgent.Tests
{
    public static class PipelineSettingsToolTests
    {
        public static void RunTests()
        {
            TestContracts();
            TestShaderList();
            TestEditorFocus();
            TestSettings();
        }

        private static void TestContracts()
        {
            var settings = ToolContractRegistry.Get("manage_settings");
            foreach (var action in new[] { "get_graphics", "get_input", "get_lighting", "get_navmesh" })
                Assert.AreEqual(HeraRiskClass.ReadOnly, settings.Actions[action].Safety.RiskClass, action);
            foreach (var action in new[] { "set_graphics", "set_input", "set_lighting", "set_navmesh" })
                Assert.AreEqual(HeraRiskClass.Destructive, settings.Actions[action].Safety.RiskClass, action);
            Assert.AreEqual(
                HeraRiskClass.Write,
                ToolContractRegistry.Get("manage_editor").Actions["focus"].Safety.RiskClass);
        }

        private static void TestShaderList()
        {
            var first = RequireSuccess(DescribeShader.HandleCommand(new JObject
            {
                ["list"] = true,
                ["limit"] = 1,
            }));
            var firstName = first["shaders"]?.Values<string>().Single();
            Assert.IsFalse(string.IsNullOrEmpty(firstName));

            var filtered = RequireSuccess(DescribeShader.HandleCommand(new JObject
            {
                ["list"] = true,
                ["filter"] = firstName.ToUpperInvariant(),
                ["limit"] = 1,
            }));
            var shaders = filtered["shaders"]?.Values<string>().ToArray();
            Assert.IsNotNull(shaders);
            Assert.LessOrEqual(shaders.Length, 1);
            CollectionAssert.Contains(shaders, firstName);
        }

        private static void TestEditorFocus()
        {
            var window = ScriptableObject.CreateInstance<PipelineSettingsWindow>();
            window.titleContent = new GUIContent("Hera Pipeline Settings Test");
            try
            {
                window.Show();
                var focused = RequireSuccess(ManageEditor.HandleCommand(new JObject
                {
                    ["action"] = "focus",
                    ["type"] = typeof(PipelineSettingsWindow).FullName,
                }));
                Assert.AreEqual(typeof(PipelineSettingsWindow).FullName, focused.Value<string>("type"));
                RequireError(ManageEditor.HandleCommand(new JObject
                {
                    ["action"] = "focus",
                    ["type"] = "HeraAgent.Tests.__MissingEditorWindow",
                }), "EDITOR_WINDOW_NOT_FOUND");
            }
            finally
            {
                window.Close();
            }
        }

        private static void TestSettings()
        {
            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var folder = "Assets/HeraPipelineSettingsTests_" + Guid.NewGuid().ToString("N");
            Assert.IsFalse(string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length))));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, folder + "/Settings.unity"));

            var inputManager = LoadInputManager();
            var axes = inputManager.FindProperty("m_Axes");
            Assert.IsNotNull(axes);
            Assert.Greater(axes.arraySize, 0);
            var firstAxis = axes.GetArrayElementAtIndex(0);
            var axisName = firstAxis.FindPropertyRelative("m_Name").stringValue;
            var originalSensitivity = firstAxis.FindPropertyRelative("sensitivity").floatValue;

            var lighting = new LightingSettings { name = "HeraPipelineSettingsTests" };
            AssetDatabase.CreateAsset(lighting, folder + "/LightingSettings.lighting");
            Lightmapping.lightingSettings = lighting;
            var originalBounces = lighting.maxBounces;

            var navSettings = UnityEditor.AI.NavMeshBuilder.navMeshSettingsObject;
            Assert.IsNotNull(navSettings);
            var nav = new SerializedObject(navSettings);
            var radius = nav.FindProperty("m_BuildSettings.agentRadius");
            Assert.IsNotNull(radius);
            var originalRadius = radius.floatValue;

            try
            {
                var graphics = RequireSuccess(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "get_graphics",
                }));
                var pipelinePath = graphics.Value<string>("render_pipeline_asset_path");
                var graphicsDryRun = RequireSuccess(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "set_graphics",
                    ["render_pipeline_asset"] = pipelinePath == null
                        ? JValue.CreateNull()
                        : new JValue(pipelinePath),
                    ["dry_run"] = true,
                }));
                Assert.IsTrue(graphicsDryRun.Value<bool>("dry_run"));

                var input = RequireSuccess(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "get_input",
                    ["limit"] = 1,
                }));
                Assert.AreEqual(1, input.Value<int>("returned"));
                Assert.AreEqual(axisName, input["axes"]?[0]?.Value<string>("name"));

                var changedSensitivity = originalSensitivity + 0.125f;
                RequireSuccess(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "set_input",
                    ["axis"] = axisName,
                    ["sensitivity"] = changedSensitivity,
                    ["dry_run"] = true,
                }));
                Assert.AreEqual(originalSensitivity, ReadInputFloat(axisName, "sensitivity"), 0.0001f);
                RequireSuccess(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "set_input",
                    ["axis"] = axisName,
                    ["sensitivity"] = changedSensitivity,
                }));
                Assert.AreEqual(changedSensitivity, ReadInputFloat(axisName, "sensitivity"), 0.0001f);
                RequireError(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "set_input",
                    ["axis"] = "__hera_missing_axis__",
                    ["sensitivity"] = 1f,
                }), "INPUT_AXIS_NOT_FOUND");

                RequireSuccess(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "get_lighting",
                }));
                var changedBounces = originalBounces == int.MaxValue ? originalBounces - 1 : originalBounces + 1;
                RequireSuccess(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "set_lighting",
                    ["bounces"] = changedBounces,
                    ["dry_run"] = true,
                }));
                Assert.AreEqual(originalBounces, lighting.maxBounces);
                RequireSuccess(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "set_lighting",
                    ["bounces"] = changedBounces,
                }));
                Assert.AreEqual(changedBounces, lighting.maxBounces);

                RequireSuccess(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "get_navmesh",
                }));
                var changedRadius = originalRadius + 0.01f;
                RequireSuccess(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "set_navmesh",
                    ["agent_radius"] = changedRadius,
                    ["dry_run"] = true,
                }));
                Assert.AreEqual(originalRadius, ReadNavRadius(navSettings), 0.0001f);
                RequireSuccess(ManageSettings.HandleCommand(new JObject
                {
                    ["action"] = "set_navmesh",
                    ["agent_radius"] = changedRadius,
                }));
                Assert.AreEqual(changedRadius, ReadNavRadius(navSettings), 0.0001f);
            }
            finally
            {
                SetInputFloat(axisName, "sensitivity", originalSensitivity);
                lighting.maxBounces = originalBounces;
                EditorUtility.SetDirty(lighting);
                var restoreNav = new SerializedObject(navSettings);
                restoreNav.FindProperty("m_BuildSettings.agentRadius").floatValue = originalRadius;
                restoreNav.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                if (originalSetup.Any(entry => entry.isLoaded && !string.IsNullOrEmpty(entry.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.Refresh();
            }
        }

        private static SerializedObject LoadInputManager()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            Assert.IsNotNull(assets);
            Assert.Greater(assets.Length, 0);
            return new SerializedObject(assets[0]);
        }

        private static float ReadInputFloat(string axisName, string property)
        {
            var input = LoadInputManager();
            var axes = input.FindProperty("m_Axes");
            for (var i = 0; i < axes.arraySize; i++)
            {
                var axis = axes.GetArrayElementAtIndex(i);
                if (axis.FindPropertyRelative("m_Name").stringValue == axisName)
                    return axis.FindPropertyRelative(property).floatValue;
            }
            Assert.Fail("Input axis disappeared: " + axisName);
            return 0f;
        }

        private static void SetInputFloat(string axisName, string property, float value)
        {
            var input = LoadInputManager();
            var axes = input.FindProperty("m_Axes");
            for (var i = 0; i < axes.arraySize; i++)
            {
                var axis = axes.GetArrayElementAtIndex(i);
                if (axis.FindPropertyRelative("m_Name").stringValue != axisName)
                    continue;
                axis.FindPropertyRelative(property).floatValue = value;
                input.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }

        private static float ReadNavRadius(UnityEngine.Object settings)
        {
            return new SerializedObject(settings)
                .FindProperty("m_BuildSettings.agentRadius")
                .floatValue;
        }

        private static JObject RequireSuccess(object response)
        {
            var success = response as SuccessResponse;
            Assert.IsNotNull(success, "Expected success, got: " + JObject.FromObject(response));
            return success.data == null ? new JObject() : JObject.FromObject(success.data);
        }

        private static void RequireError(object response, string code)
        {
            var error = response as ErrorResponse;
            Assert.IsNotNull(error, "Expected error " + code + ", got: " + JObject.FromObject(response));
            Assert.AreEqual(code, error.code);
        }

        public sealed class PipelineSettingsWindow : EditorWindow
        {
        }
    }
}
