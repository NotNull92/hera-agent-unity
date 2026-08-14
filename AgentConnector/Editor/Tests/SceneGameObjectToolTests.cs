using System;
using System.Linq;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static HeraAgent.Tests.ToolResponseTestSupport;

namespace HeraAgent.Tests
{
    public static class SceneGameObjectToolTests
    {
        public static void RunTests()
        {
            Assert.IsFalse(Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(scene => scene.isDirty),
                "The disposable fixture must start with clean scenes.");

            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var folder = "Assets/HeraSceneGameObjectTests_" + Guid.NewGuid().ToString("N");
            var folderName = folder.Substring("Assets/".Length);
            Assert.IsFalse(string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", folderName)));

            try
            {
                var basePath = folder + "/Base.unity";
                var additivePath = folder + "/Additive.unity";
                var singlePath = folder + "/Single.unity";

                var baseScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Assert.IsTrue(EditorSceneManager.SaveScene(baseScene, basePath));

                RequireSuccess(ManageScene.Create(new JObject
                {
                    ["path"] = additivePath,
                    ["mode"] = "additive",
                    ["template"] = "default",
                }));
                var additiveScene = SceneManager.GetSceneByPath(additivePath);
                Assert.IsTrue(additiveScene.IsValid() && additiveScene.isLoaded);
                Assert.Greater(additiveScene.rootCount, 0);

                RequireError(ManageScene.Create(new JObject
                {
                    ["path"] = additivePath,
                    ["mode"] = "additive",
                }), "SCENE_EXISTS");

                RequireSuccess(ManageScene.SetActive(new JObject { ["path"] = basePath }));
                Assert.AreEqual(basePath, SceneManager.GetActiveScene().path);
                RequireError(ManageScene.SetActive(new JObject { ["path"] = folder + "/Missing.unity" }), "SCENE_NOT_LOADED");

                EditorSceneManager.MarkSceneDirty(baseScene);
                EditorSceneManager.MarkSceneDirty(additiveScene);
                RequireError(ManageScene.Create(new JObject
                {
                    ["path"] = singlePath,
                    ["mode"] = "single",
                }), "SCENE_DIRTY");

                var saveAll = RequireSuccess(ManageScene.SaveAll(new JObject()));
                CollectionAssert.AreEquivalent(
                    new[] { basePath, additivePath },
                    saveAll["scenes"].Values<string>());
                Assert.IsFalse(baseScene.isDirty || additiveScene.isDirty);

                var untitled = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.MarkSceneDirty(untitled);
                RequireError(ManageScene.SaveAll(new JObject()), "SCENE_UNSAVED");
                Assert.IsTrue(EditorSceneManager.CloseScene(untitled, true));

                RequireSuccess(ManageScene.Create(new JObject
                {
                    ["path"] = singlePath,
                    ["mode"] = "single",
                    ["template"] = "empty",
                }));
                Assert.AreEqual(1, SceneManager.sceneCount);
                Assert.AreEqual(singlePath, SceneManager.GetActiveScene().path);

                var parent = new GameObject("Parent");
                parent.transform.position = new Vector3(10f, 0f, 0f);
                var child = new GameObject("InactiveChild");
                child.transform.SetParent(parent.transform, false);
                parent.SetActive(false);
                Assert.IsTrue(EditorSceneManager.SaveScene(SceneManager.GetActiveScene()));

                var target = new JObject { ["instance_id"] = EntityIdCompat.IdOf(child) };
                RequireSuccess(ManageGameObject.SetTransform(Merge(target, new JObject
                {
                    ["position"] = new JArray(1f, 2f, 3f),
                    ["rotation"] = new JArray(10f, 20f, 30f),
                    ["scale"] = new JArray(2f, 3f, 4f),
                    ["space"] = "local",
                })));
                AssertVector(child.transform.localPosition, new Vector3(1f, 2f, 3f));
                AssertVector(child.transform.localEulerAngles, new Vector3(10f, 20f, 30f));
                AssertVector(child.transform.localScale, new Vector3(2f, 3f, 4f));
                Assert.IsTrue(child.scene.isDirty);

                RequireSuccess(ManageGameObject.SetTransform(Merge(target, new JObject
                {
                    ["position"] = new JArray(2f, 4f, 6f),
                    ["rotation"] = new JArray(0f, 90f, 0f),
                    ["space"] = "world",
                })));
                AssertVector(child.transform.position, new Vector3(2f, 4f, 6f));
                AssertVector(child.transform.eulerAngles, new Vector3(0f, 90f, 0f));
                RequireError(ManageGameObject.SetTransform(target), "MISSING_PARAM");

                RequireSuccess(ManageGameObject.SetTag(Merge(target, new JObject { ["tag"] = "Untagged" })));
                Assert.AreEqual("Untagged", child.tag);
                RequireError(ManageGameObject.SetTag(Merge(target, new JObject { ["tag"] = "__hera_missing_tag__" })), "TAG_NOT_FOUND");

                RequireSuccess(ManageGameObject.SetLayer(Merge(target, new JObject { ["layer"] = "UI" })));
                Assert.AreEqual(LayerMask.NameToLayer("UI"), child.layer);
                Undo.PerformUndo();
                Assert.AreEqual(0, child.layer);
                RequireSuccess(ManageGameObject.SetLayer(Merge(target, new JObject { ["layer"] = 5 })));
                Assert.AreEqual(5, child.layer);
                RequireError(ManageGameObject.SetLayer(Merge(target, new JObject { ["layer"] = 32 })), "LAYER_NOT_FOUND");
            }
            finally
            {
                if (originalSetup.Any(entry => entry.isLoaded && !string.IsNullOrEmpty(entry.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.Refresh();
            }
        }

        private static JObject Merge(JObject target, JObject values)
        {
            var merged = (JObject)target.DeepClone();
            merged.Merge(values);
            return merged;
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f),
                $"Expected {expected}, got {actual}.");
        }
    }
}
