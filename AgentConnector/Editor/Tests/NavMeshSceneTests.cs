using System;
using System.IO;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeraAgent.Tests
{
    public class NavMeshSceneTests
    {
        string folder;
        SceneSetup[] originalSetup;

        [SetUp]
        public void SetUp()
        {
            originalSetup = EditorSceneManager.GetSceneManagerSetup();
            folder = "Assets/HeraNavMesh-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(folder);
            if (Array.Exists(originalSetup, entry => entry.isLoaded && !string.IsNullOrEmpty(entry.path)))
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        static SuccessResponse Set(float radius, bool preview = false)
        {
            var response = ManageSettings.HandleCommand(new JObject { ["action"] = "set_navmesh", ["agent_radius"] = radius, ["dry_run"] = preview });
            Assert.IsInstanceOf<SuccessResponse>(response, JObject.FromObject(response).ToString());
            return (SuccessResponse)response;
        }

        static float Read() => JObject.FromObject(((SuccessResponse)ManageSettings.HandleCommand(new JObject { ["action"] = "get_navmesh" })).data).Value<float>("agent_radius");

        [TestCase(false)]
        [TestCase(true)]
        public void SavedSceneChangesStayUnsaved(bool checkMaterialFirst)
        {
            var scene = SceneManager.GetActiveScene();
            var scenePath = folder + "/Nav.unity";
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, scenePath));
            var sceneBytes = File.ReadAllBytes(scenePath);
            Assert.IsFalse(scene.isDirty);
            var material = new Material(Shader.Find("Unlit/Color"));
            var materialPath = folder + "/B.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            var materialBytes = File.ReadAllBytes(materialPath);
            material.color = Color.magenta;
            EditorUtility.SetDirty(material);
            var response = Set(.875f);
            if (checkMaterialFirst) Assert.IsTrue(EditorUtility.IsDirty(material), "Unrelated material must stay dirty");
            Assert.IsTrue(scene.isDirty, "NavMesh changes must mark their scene dirty");
            Assert.IsTrue(EditorUtility.IsDirty(material));
            CollectionAssert.AreEqual(materialBytes, File.ReadAllBytes(materialPath));
            CollectionAssert.AreEqual(sceneBytes, File.ReadAllBytes(scenePath), "The scene must not be saved implicitly");
            Assert.AreEqual(.875f, Read());
            StringAssert.Contains("save", response.agent_hint);
        }

        [Test]
        public void UntitledPreviewAndInvalidValues()
        {
            var scene = SceneManager.GetActiveScene();
            Assert.IsEmpty(scene.path);
            Assert.IsFalse(scene.isDirty);
            var before = Read();
            Assert.IsNull(Set(.875f, true).agent_hint);
            Assert.IsFalse(scene.isDirty);
            Assert.AreEqual(before, Read());
            Set(-1f);
            Assert.IsFalse(scene.isDirty);
            Assert.AreEqual(before, Read());
            Set(.875f);
            Assert.IsTrue(scene.isDirty);
            Assert.IsEmpty(scene.path);
            Assert.AreEqual(.875f, Read());
        }

        [Test]
        public void OnlyActiveSceneIsMarked()
        {
            var a = SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(a, folder + "/A.unity");
            var original = Read();
            var b = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(b, folder + "/B.unity");
            SceneManager.SetActiveScene(b);
            Assert.AreEqual(b, SceneManager.GetActiveScene());
            Set(.875f);
            Assert.IsTrue(b.isDirty);
            Assert.IsFalse(a.isDirty);
            Assert.AreEqual(.875f, Read());
            SceneManager.SetActiveScene(a);
            Assert.AreEqual(original, Read());
        }
    }
}
