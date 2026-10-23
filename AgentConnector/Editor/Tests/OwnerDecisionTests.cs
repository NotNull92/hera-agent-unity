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

    }
}
