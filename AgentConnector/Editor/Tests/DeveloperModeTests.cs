using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeraAgent.Tests
{
#if UNITY_2022_3
    [SetUpFixture]
    public sealed class DeveloperModeFixture
    {
        private string previous;
        private string path;

        [OneTimeSetUp]
        public void Enable()
        {
            previous = HeraSettings.s_ConfigPathOverride;
            path = Path.Combine(Path.GetTempPath(), "hera-developer-tests-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, "{\"developer_mode\":true}");
            HeraSettings.s_ConfigPathOverride = path;
            HeraSettings.ResetForTests();
        }

        [OneTimeTearDown]
        public void Restore()
        {
            HeraSettings.s_ConfigPathOverride = previous;
            HeraSettings.ResetForTests();
            if (File.Exists(path)) File.Delete(path);
        }
    }
#endif

    public sealed class DeveloperModeTests
    {
        [Test]
        public void SettingsWindowPreservesUnknownKeys()
        {
            var merge = typeof(HeraAgent.Editor.HeraAgentAssetConfigWindow).GetMethod(
                "MergeConfig", BindingFlags.NonPublic | BindingFlags.Static);
            var current = new JObject { ["developer_mode"] = true, ["custom_key"] = 42 };
            var desired = new JObject { ["developer_mode"] = false };
            var result = (JObject)merge.Invoke(null, new object[] { current, desired });
            Assert.AreEqual(42, result.Value<int>("custom_key"));
#if UNITY_2022_3
            Assert.IsFalse(result.Value<bool>("developer_mode"));
#else
            Assert.IsTrue(result.Value<bool>("developer_mode"));
#endif
        }

#if UNITY_2022_3
        [Test]
        public void InstanceIdsKeepTheirSignedValue()
        {
            var go = new GameObject("HeraDeveloperIdTest");
            try
            {
                var negative = go.GetInstanceID();
                Assert.Less(negative, 0);
                Assert.AreEqual((long)negative, EntityIdCompat.IdOf(go));
                Assert.AreSame(go, EntityIdCompat.ToObject(negative));
                var positive = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                Assert.IsNotNull(positive);
                Assert.Greater(positive.GetInstanceID(), 0);
                Assert.AreSame(positive, EntityIdCompat.ToObject(EntityIdCompat.IdOf(positive)));
                Assert.IsNull(EntityIdCompat.ToObject(0));
                Assert.IsNull(EntityIdCompat.ToObject((long)int.MinValue - 1));
                Assert.IsNull(EntityIdCompat.ToObject((long)int.MaxValue + 1));
                Object.DestroyImmediate(go);
                Assert.IsNull(EntityIdCompat.ToObject(negative));
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async Task GatePrecedesCatalogApprovalAndMutation()
        {
            var previous = HeraSettings.s_ConfigPathOverride;
            var path = Path.Combine(Path.GetTempPath(), "hera-developer-gate-" + Guid.NewGuid().ToString("N") + ".json");
            var go = new GameObject("HeraGateOriginal");
            try
            {
                File.WriteAllText(path, "{\"developer_mode\":false}");
                HeraSettings.s_ConfigPathOverride = path;
                HeraSettings.ResetForTests();
                var args = new JObject { ["action"] = "set_name", ["instance_id"] = EntityIdCompat.IdOf(go), ["name"] = "HeraGateChanged" };
                Assert.IsTrue(CommandRequestContext.TryCreate(new JObject { ["catalog_hash"] = "stale" }, args, out var context, out _));
                var locked = JObject.FromObject(await CommandRouter.Dispatch("manage_gameobject", args, context));
                Assert.AreEqual("DEVELOPER_MODE_REQUIRED", locked.Value<string>("code"));
                Assert.AreEqual("HeraGateOriginal", go.name);
                var batch = JObject.FromObject(await CommandRouter.DispatchBatch(
                    new List<CommandRouter.BatchCommandItem> { new CommandRouter.BatchCommandItem { Command = "manage_gameobject", Params = args } },
                    new CommandRouter.BatchOptions()));
                Assert.AreEqual("DEVELOPER_MODE_REQUIRED", batch.Value<string>("code"));
                Assert.AreEqual("HeraGateOriginal", go.name);
                Assert.IsTrue(CommandRequestContext.TryCreate(new JObject { ["protocol_version"] = "invalid" }, args, out context, out _));
                var protocol = JObject.FromObject(await CommandRouter.Dispatch("manage_gameobject", args, context));
                Assert.AreEqual("EXECUTION_PROTOCOL_UNSUPPORTED", protocol.Value<string>("code"));
                File.WriteAllText(path, "{\"developer_mode\":true}");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(1));
                var enabled = JObject.FromObject(await CommandRouter.Dispatch("list", new JObject { ["names"] = true }));
                Assert.IsTrue(enabled.Value<bool>("success"));
                File.WriteAllText(path, "{}");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));
                var disabled = JObject.FromObject(await CommandRouter.Dispatch("list", new JObject()));
                Assert.AreEqual("DEVELOPER_MODE_REQUIRED", disabled.Value<string>("code"));
            }
            finally
            {
                Object.DestroyImmediate(go);
                HeraSettings.s_ConfigPathOverride = previous;
                HeraSettings.ResetForTests();
                if (File.Exists(path)) File.Delete(path);
            }
        }
#endif
    }
}
