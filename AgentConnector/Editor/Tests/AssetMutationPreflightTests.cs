using System;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HeraAgent.Tests
{
    public sealed class AssetMutationPreflightProbe : ScriptableObject
    {
        public int Value;
    }

    public static class AssetMutationPreflightTests
    {
        [MenuItem("HeraAgent/Tests/AssetMutationPreflight")]
        public static void RunTests()
        {
            var allPassed = true;
            var root = "Assets/HeraAssetPreflightTests_" + Guid.NewGuid().ToString("N");
            var folder = root.Substring("Assets/".Length);
            AssetDatabase.CreateFolder("Assets", folder);

            try
            {
                allPassed &= ExpectError(
                    "invalid initial property creates no asset",
                    ManageAssets.HandleCommand(new JObject
                    {
                        ["action"] = "create",
                        ["type"] = typeof(AssetMutationPreflightProbe).FullName,
                        ["path"] = root + "/invalid-properties.asset",
                        ["properties"] = new JObject { ["NotAProperty"] = 1 },
                    }),
                    "INVALID_INITIAL_PROPERTIES");
                allPassed &= ExpectTrue(
                    "invalid initial property left destination absent",
                    AssetDatabase.LoadMainAssetAtPath(root + "/invalid-properties.asset") == null);

                var controllerPath = root + "/controller.controller";
                allPassed &= ExpectSuccess(
                    "create controller",
                    ManageAnimation.HandleCommand(new JObject { ["action"] = "create_controller", ["path"] = controllerPath }));
                allPassed &= ExpectSuccess(
                    "create source state",
                    ManageAnimation.HandleCommand(new JObject { ["action"] = "add_state", ["path"] = controllerPath, ["name"] = "Source" }));
                allPassed &= ExpectSuccess(
                    "create destination state",
                    ManageAnimation.HandleCommand(new JObject { ["action"] = "add_state", ["path"] = controllerPath, ["name"] = "Destination" }));

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                var stateMachine = controller.layers[0].stateMachine;
                var statesBeforeMissingMotion = stateMachine.states.Length;
                allPassed &= ExpectError(
                    "invalid motion creates no state",
                    ManageAnimation.HandleCommand(new JObject
                    {
                        ["action"] = "add_state",
                        ["path"] = controllerPath,
                        ["name"] = "NoMotionState",
                        ["motion"] = root + "/missing.anim",
                    }),
                    "MOTION_NOT_FOUND");
                allPassed &= ExpectEqual(
                    "invalid motion left state count unchanged",
                    statesBeforeMissingMotion,
                    stateMachine.states.Length);

                var source = FindState(stateMachine, "Source");
                var transitionsBeforeInvalidCondition = source.transitions.Length;
                allPassed &= ExpectError(
                    "invalid condition creates no transition",
                    ManageAnimation.HandleCommand(new JObject
                    {
                        ["action"] = "add_transition",
                        ["path"] = controllerPath,
                        ["from"] = "Source",
                        ["to"] = "Destination",
                        ["conditions"] = new JArray
                        {
                            new JObject { ["parameter"] = "MissingParameter", ["mode"] = "Greater", ["threshold"] = 1 },
                        },
                    }),
                    "PARAMETER_NOT_FOUND");
                allPassed &= ExpectEqual(
                    "invalid condition left transition count unchanged",
                    transitionsBeforeInvalidCondition,
                    source.transitions.Length);

                allPassed &= ExpectError(
                    "material path outside Assets is rejected",
                    ManageMaterial.HandleCommand(new JObject
                    {
                        ["action"] = "create",
                        ["path"] = "../outside.mat",
                        ["shader"] = "Unlit/Color",
                    }),
                    "INVALID_PATH");
                allPassed &= ExpectError(
                    "prefab path outside Assets is rejected",
                    ManagePrefab.HandleCommand(new JObject
                    {
                        ["action"] = "instantiate",
                        ["path"] = "../outside.prefab",
                    }),
                    "INVALID_PATH");
                allPassed &= ExpectError(
                    "asset-import path outside Assets is rejected",
                    ManageAssetImport.HandleCommand(new JObject
                    {
                        ["action"] = "set",
                        ["path"] = "../outside.png",
                        ["property"] = "m_sRGBTexture",
                        ["value"] = 1,
                    }),
                    "INVALID_PATH");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                allPassed = false;
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.SaveAssets();
            }

            if (allPassed)
                Debug.Log("[AssetMutationPreflightTests] ALL PASSED");
            else
                Debug.LogError("[AssetMutationPreflightTests] SOME TESTS FAILED");
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
        {
            foreach (var child in stateMachine.states)
                if (child.state.name == name)
                    return child.state;
            return null;
        }

        private static bool ExpectSuccess(string label, object response)
        {
            return ExpectTrue(label, response is SuccessResponse);
        }

        private static bool ExpectError(string label, object response, string code)
        {
            return ExpectTrue(label, response is ErrorResponse error && error.code == code);
        }

        private static bool ExpectEqual(string label, int expected, int actual)
        {
            return ExpectTrue(label + $" (expected {expected}, got {actual})", expected == actual);
        }

        private static bool ExpectTrue(string label, bool actual)
        {
            if (actual)
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError("[FAIL] " + label);
            return false;
        }
    }
}
