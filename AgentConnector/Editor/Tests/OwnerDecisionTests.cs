using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

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
            // shortcut: leaves one small .mat in the OS trash per run, replace when Unity offers an isolated trash target.
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

#if UNITY_2022_3
        [UnityTest]
        public IEnumerator ActionDiscoveryIsScoped()
        {
            var task = ActionDiscoveryIsScopedAsync();
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }

        private async Task ActionDiscoveryIsScopedAsync()
#else
        [Test]
        public async Task ActionDiscoveryIsScoped()
#endif
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

#if UNITY_2022_3
        [UnityTest]
        public IEnumerator ActionDiscoveryRejectsUnknownAndUnscopedSelectors()
        {
            var task = ActionDiscoveryRejectsUnknownAndUnscopedSelectorsAsync();
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }

        private async Task ActionDiscoveryRejectsUnknownAndUnscopedSelectorsAsync()
#else
        [Test]
        public async Task ActionDiscoveryRejectsUnknownAndUnscopedSelectors()
#endif
        {
            var invalid = await CommandRouter.Dispatch("list", new JObject { ["tool"] = "input", ["action"] = "clik" }) as ErrorResponse;
            Assert.AreEqual("UNKNOWN_ACTION", invalid?.code);
            StringAssert.Contains("click", JObject.FromObject(invalid).ToString());
            var unscoped = await CommandRouter.Dispatch("list", new JObject { ["action"] = "click" }) as ErrorResponse;
            Assert.AreEqual("INVALID_ARGUMENT", unscoped?.code);
        }

#if UNITY_2022_3
        [UnityTest]
        public IEnumerator ActionDiscoveryIgnoresNonBooleanSelectors()
        {
            var task = ActionDiscoveryIgnoresNonBooleanSelectorsAsync();
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }

        private async Task ActionDiscoveryIgnoresNonBooleanSelectorsAsync()
#else
        [Test]
        public async Task ActionDiscoveryIgnoresNonBooleanSelectors()
#endif
        {
            foreach (var selector in new[] { "names", "catalog", "compact" })
            {
                var parameters = new JObject { ["tool"] = "input", ["action"] = "click", [selector] = "x" };
                var response = await CommandRouter.Dispatch("list", parameters);
                Assert.IsInstanceOf<SuccessResponse>(response, JObject.FromObject(response).ToString());
                Assert.AreEqual("click", JObject.FromObject(((SuccessResponse)response).data).Value<string>("action"));
                parameters[selector] = true;
                var conflict = await CommandRouter.Dispatch("list", parameters) as ErrorResponse;
                Assert.AreEqual("INVALID_ARGUMENT", conflict?.code, selector);
            }
        }

        [Test]
        public void LongDescriptionsStayBounded()
        {
            foreach (var name in new[] { "game_feel", "ui_slop", "manage_prefab", "manage_packages", "manage_components", "find_gameobjects" })
                Assert.LessOrEqual(JObject.FromObject(ToolDiscovery.GetToolSchema(name)).Value<string>("description").Length, 300, name);
        }
    }
}
