using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace HeraAgent.Tests
{
    internal static class TestArtifactTests
    {
        public static void RunTests()
        {
            var runner = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("HeraAgent.TestRunner.RunTests"))
                .FirstOrDefault(type => type != null);
            Assert.NotNull(runner, "The test artifact gate requires the Test Framework connector.");
            var partition = runner.GetMethod("PartitionNames", BindingFlags.Static | BindingFlags.NonPublic);
            var names = new[] { "F.Z", "F.A(1,2)", "F.C", "F.B", "F.D" };
            Func<int, int, string[]> split = (index, count) => (string[])partition.Invoke(null, new object[] { names, index, count });
            CollectionAssert.AreEqual(new[] { "F.A(1,2)", "F.B", "F.C" }, split(1, 2));
            CollectionAssert.AreEqual(new[] { "F.D", "F.Z" }, split(2, 2));
            CollectionAssert.AreEquivalent(names, split(1, 2).Concat(split(2, 2)));
            Assert.IsEmpty(split(1, 2).Intersect(split(2, 2)));
            CollectionAssert.AreEqual(split(1, 2), split(1, 2));
            CollectionAssert.AreEqual(names.OrderBy(name => name, StringComparer.Ordinal), split(1, 1));
            Assert.IsEmpty(split(6, 6));
            Assert.Throws<TargetInvocationException>(() => split(0, 2));
            Assert.Throws<TargetInvocationException>(() => split(1, 0));
            Assert.Throws<TargetInvocationException>(() => split(3, 2));

            var parse = runner.GetMethod("ParseShard", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var raw in new[] { "{shard_index:1}", "{shard_count:2}", "{shard_index:0,shard_count:2}",
                "{shard_index:3,shard_count:2}", "{shard_index:1,shard_count:0}", "{shard_index:1.0,shard_count:2}",
                "{shard_index:1,shard_count:2147483648}" })
            {
                var arguments = new object[] { JObject.Parse(raw), null };
                var error = parse.Invoke(null, arguments);
                Assert.AreEqual("INVALID_ARGUMENT", JObject.FromObject(error)["code"]?.Value<string>());
            }
            var valid = new object[] { JObject.Parse("{shard_index:1,shard_count:1}"), null };
            Assert.IsNull(parse.Invoke(null, valid));
            Assert.NotNull(valid[1]);

            // Build real NUnit results, then wrap them with the installed Unity adaptors.
            var apiAssembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetType("UnityEditor.TestTools.TestRunner.Api.TestAdaptor") != null);
            var testAdaptor = apiAssembly.GetType("UnityEditor.TestTools.TestRunner.Api.TestAdaptor");
            var resultAdaptor = apiAssembly.GetType("UnityEditor.TestTools.TestRunner.Api.TestResultAdaptor");
            var cases = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(runner.GetNestedType("CaseResult")));
            var passed = new System.Collections.Generic.List<string>();
            var failed = new System.Collections.Generic.List<string>();
            var skipped = new System.Collections.Generic.List<string>();
            foreach (var state in new[] { ResultState.Success, ResultState.Failure, ResultState.Ignored, ResultState.Inconclusive })
            {
                var test = new TestMethod(new MethodWrapper(typeof(TestArtifactTests), nameof(Sample)));
                var result = test.MakeTestResult();
                result.SetResult(state, "message <&>", "stack <line>");
                result.Duration = 0.125;
                var node = Activator.CreateInstance(testAdaptor, BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { test, null }, null);
                var adapted = Activator.CreateInstance(resultAdaptor, BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { result, node, null }, null);
                runner.GetMethod("CollectResultTree", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { adapted, passed, failed, skipped, cases });
            }
            Assert.AreEqual(4, cases.Count);
            var serialized = JArray.FromObject(cases);
            CollectionAssert.AreEqual(new[] { "passed", "failed", "skipped", "skipped" }, serialized.Select(c => c["status"].Value<string>()));
            Assert.IsTrue(serialized.All(c => c["duration_ms"].Value<double>() == 125));
            Assert.AreEqual("message <&>", serialized[1]["message"].Value<string>());
            Assert.AreEqual("stack <line>", serialized[1]["stack_trace"].Value<string>());
            Assert.IsNull(serialized[0]["message"]);
            var build = runner.GetMethod("BuildResponse", BindingFlags.Static | BindingFlags.NonPublic);
            var response = JObject.FromObject(build.Invoke(null, new object[] { passed, failed, skipped, true, cases, valid[1] }));
            Assert.AreEqual("TESTS_FAILED", response["code"].Value<string>());
            Assert.AreEqual(4, response["data"]["total"].Value<int>());
            Assert.AreEqual(4, ((JArray)response["data"]["cases"]).Count);
            passed.Clear(); failed.Clear(); skipped.Clear(); cases.Clear();
            response = JObject.FromObject(build.Invoke(null, new object[] { passed, failed, skipped, true, cases, valid[1] }));
            Assert.AreEqual("NO_TESTS_MATCHED", response["code"].Value<string>());
            Assert.AreEqual(1, response["data"]["shard"]["index"].Value<int>());
        }

        public static void Sample() { }
    }
}
