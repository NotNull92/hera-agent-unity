using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class AtomicBatchTests
    {
        internal static readonly string[] AllowedActions =
        {
            "manage_components/add", "manage_components/set",
            "manage_gameobject/create", "manage_gameobject/move",
            "manage_gameobject/set_active", "manage_gameobject/set_layer",
            "manage_gameobject/set_name", "manage_gameobject/set_parent",
            "manage_gameobject/set_tag", "manage_gameobject/set_transform",
            "manage_ui/set_anchor", "manage_ui/set_rect",
        };

        public static void RunTests()
        {
            TestAllowlist();
            TestRejectionBeforeLockAndMutation();
            TestNonAtomicStillRuns();
            TestRollback();
        }

        static CommandRouter.BatchCommandItem Item(string tool, JObject parameters) =>
            new CommandRouter.BatchCommandItem { Command = tool, Params = parameters };

        static CommandRouter.BatchCommandItem Rename(GameObject go, string name) =>
            Item("manage_gameobject", new JObject
            {
                ["action"] = "set_name", ["instance_id"] = EntityIdCompat.IdOf(go), ["name"] = name,
            });

        static void TestAllowlist()
        {
            var contracts = ToolDiscovery.GetToolNames().Cast<string>()
                .Select(ToolContractRegistry.Get)
                .Where(c => ToolContractSafety.IsBuiltIn(c.ToolType)).ToArray();
            var reversible = contracts.SelectMany(c => c.Actions.Values
                .Where(a => a.Safety.Reversible).Select(a => c.Name + "/" + a.Name))
                .OrderBy(n => n, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(AllowedActions, reversible);
            Assert.IsFalse(contracts.Any(c => c.Safety.Reversible), "No tool-wide Undo claim was reviewed.");
            foreach (var contract in contracts)
                foreach (var action in contract.Actions.Values.Where(a => a.Safety.Reversible))
                {
                    Assert.IsFalse(action.Safety.Destructive);
                    Assert.IsFalse(action.Safety.MayReloadDomain);
                    Assert.IsFalse(action.Safety.RequiresConfirmation);
                }
        }

        static void TestRejectionBeforeLockAndMutation()
        {
            var go = new GameObject("AtomicPreflightOriginal");
            var gate = (SemaphoreSlim)typeof(CommandRouter).GetField("s_Lock",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.IsTrue(gate.Wait(0));
            var group = Undo.GetCurrentGroup();
            try
            {
                var items = new List<CommandRouter.BatchCommandItem>
                {
                    Rename(go, "MustNotExecute"),
                    Item("exec", new JObject { ["code"] = "return null;" }),
                    Item("manage_assets", new JObject { ["action"] = "mkdir", ["path"] = "Assets/AtomicMustNotExist" }),
                    Item("not_a_tool", new JObject { ["action"] = "missing" }),
                    Item("manage_gameobject", new JObject { ["action"] = "missing" }),
                    Item("manage_gameobject", new JObject { ["action"] = "destroy", ["instance_id"] = EntityIdCompat.IdOf(go) }),
                    Item("screenshot", new JObject()),
                };
                var task = CommandRouter.DispatchBatch(items, new CommandRouter.BatchOptions { Atomic = true });
                Assert.IsTrue(task.IsCompleted, "Preflight must return without acquiring the busy command lock.");
                var error = task.GetAwaiter().GetResult() as ErrorResponse;
                Assert.AreEqual("ATOMIC_UNSUPPORTED_ITEM", error?.code);
                var rejects = (JArray)JObject.FromObject(error.data)["rejects"];
                var reasons = new[] { "not_reversible", "may_reload_domain", "unknown_tool", "unknown_action", "destructive", "not_reversible" };
                Assert.AreEqual(reasons.Length, rejects.Count);
                for (var i = 0; i < rejects.Count; i++)
                {
                    Assert.AreEqual(i + 1, rejects[i].Value<int>("index"));
                    Assert.AreEqual(items[i + 1].Command, rejects[i].Value<string>("tool"));
                    Assert.AreEqual(items[i + 1].Params.Value<string>("action"), rejects[i].Value<string>("action"));
                    Assert.AreEqual(reasons[i], rejects[i].Value<string>("reason"));
                }
                Assert.AreEqual("AtomicPreflightOriginal", go.name);
                Assert.AreEqual(group, Undo.GetCurrentGroup());
            }
            finally
            {
                gate.Release();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static void TestNonAtomicStillRuns()
        {
            var previous = Selection.objects;
            try
            {
                var response = CommandRouter.DispatchBatch(new List<CommandRouter.BatchCommandItem>
                {
                    Item("manage_editor", new JObject { ["action"] = "set_selection", ["targets"] = new JArray() }),
                }, new CommandRouter.BatchOptions()).GetAwaiter().GetResult() as CommandRouter.BatchCommandResponse;
                Assert.AreEqual(1, response?.Completed);
                Assert.AreEqual(0, response?.Failed);
                Assert.IsEmpty(Selection.objects);
            }
            finally { Selection.objects = previous; }
        }

        static void TestRollback()
        {
            var go = new GameObject("AtomicRollbackOriginal");
            try
            {
                var missing = Rename(go, "Missing");
                missing.Params["instance_id"] = long.MaxValue;
                var response = CommandRouter.DispatchBatch(new List<CommandRouter.BatchCommandItem>
                {
                    Rename(go, "MustRollBack"), missing,
                }, new CommandRouter.BatchOptions { Atomic = true }).GetAwaiter().GetResult()
                    as CommandRouter.BatchCommandResponse;
                Assert.AreEqual(2, response?.Completed);
                Assert.AreEqual(1, response?.Failed);
                Assert.AreEqual("AtomicRollbackOriginal", go.name);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
