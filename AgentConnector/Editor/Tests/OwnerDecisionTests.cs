using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HeraAgent.Tests
{
    public sealed class OwnerDecisionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SavesOnlyChangedAsset(bool animation)
        {
            var root = "Assets/HeraOwner-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
            try
            {
                var shader = Shader.Find("Unlit/Color");
                Assert.IsNotNull(shader);
                var unrelated = new Material(shader);
                var unrelatedPath = root + "/B.mat";
                AssetDatabase.CreateAsset(unrelated, unrelatedPath);
                var path = root + (animation ? "/A.controller" : "/A.mat");
                if (animation) AnimatorController.CreateAnimatorControllerAtPath(path);
                else AssetDatabase.CreateAsset(new Material(shader), path);
                var before = File.ReadAllBytes(unrelatedPath);
                unrelated.color = Color.magenta;
                EditorUtility.SetDirty(unrelated);
                if (animation)
                {
                    Assert.IsInstanceOf<SuccessResponse>(ManageAnimation.HandleCommand(new JObject
                        { ["action"] = "add_state", ["path"] = path, ["name"] = "SavedState", ["default"] = true }));
                    Assert.IsInstanceOf<SuccessResponse>(ManageAnimation.HandleCommand(new JObject
                        { ["action"] = "add_state", ["path"] = path, ["name"] = "SavedDestination" }));
                    Assert.IsInstanceOf<SuccessResponse>(ManageAnimation.HandleCommand(new JObject
                        { ["action"] = "add_transition", ["path"] = path, ["from"] = "SavedState", ["to"] = "SavedDestination", ["duration"] = 0.375f }));
                    var disk = File.ReadAllText(path);
                    StringAssert.Contains("SavedState", disk);
                    StringAssert.Contains("SavedDestination", disk);
                    StringAssert.Contains("m_TransitionDuration: 0.375", disk);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                    Assert.AreEqual("SavedState", controller.layers[0].stateMachine.defaultState.name);
                    Assert.AreEqual("SavedDestination", controller.layers[0].stateMachine.defaultState.transitions[0].destinationState.name);
                }
                else
                {
                    var beforeA = File.ReadAllBytes(path);
                    Assert.IsInstanceOf<SuccessResponse>(ManageMaterial.HandleCommand(new JObject
                        { ["action"] = "set", ["path"] = path, ["property"] = "_Color", ["value"] = "0,1,0,1" }));
                    Assert.IsFalse(beforeA.SequenceEqual(File.ReadAllBytes(path)), "A must be saved");
                    Assert.IsFalse(EditorUtility.IsDirty(AssetDatabase.LoadAssetAtPath<Material>(path)));
                }
                Assert.IsTrue(EditorUtility.IsDirty(unrelated), "B must remain dirty");
                CollectionAssert.AreEqual(before, File.ReadAllBytes(unrelatedPath), "B disk bytes must not change");
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void DeleteMovesAssetToTrash()
        {
            var path = "Assets/HeraTrash-" + Guid.NewGuid().ToString("N") + ".mat";
            AssetDatabase.CreateAsset(new Material(Shader.Find("Unlit/Color")), path);
            try
            {
                var result = ManageAssets.HandleCommand(new JObject { ["action"] = "delete", ["path"] = path }) as SuccessResponse;
                Assert.IsNotNull(result);
                StringAssert.Contains("trash", result.message.ToLowerInvariant());
                Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(path));
                Assert.IsFalse(File.Exists(path));
            }
            finally { if (File.Exists(path)) AssetDatabase.DeleteAsset(path); }
        }

        [Test]
        public async Task ActionDiscoveryIsScoped()
        {
            var response = await CommandRouter.Dispatch("list", new JObject { ["tool"] = "input", ["action"] = "click" }) as SuccessResponse;
            Assert.IsNotNull(response);
            var data = JObject.FromObject(response.data);
            Assert.AreEqual("click", data.Value<string>("action"));
            Assert.IsNull(data["actions"]);
            Assert.IsTrue(JToken.DeepEquals(ToolContractRegistry.Get("input").Actions["click"].InputSchema, data["schema"]));
            Assert.IsNotNull(data["metadata"]?["safety"]);
            Assert.Less(data.ToString(Newtonsoft.Json.Formatting.None).Length, 8000);
        }

        [Test]
        public async Task ActionDiscoveryRejectsUnknownAndUnscopedSelectors()
        {
            var invalid = await CommandRouter.Dispatch("list", new JObject { ["tool"] = "input", ["action"] = "clik" }) as ErrorResponse;
            Assert.AreEqual("UNKNOWN_ACTION", invalid?.code);
            StringAssert.Contains("click", JObject.FromObject(invalid).ToString());
            var unscoped = await CommandRouter.Dispatch("list", new JObject { ["action"] = "click" }) as ErrorResponse;
            Assert.AreEqual("INVALID_ARGUMENT", unscoped?.code);
        }

        [Test]
        public void LongDescriptionsStayBounded()
        {
            foreach (var name in new[] { "game_feel", "ui_slop", "manage_prefab", "manage_packages", "manage_components", "find_gameobjects" })
                Assert.LessOrEqual(JObject.FromObject(ToolDiscovery.GetToolSchema(name)).Value<string>("description").Length, 300, name);
        }
    }
}
