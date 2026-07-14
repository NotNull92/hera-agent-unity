using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class ToolDiscoveryTests
    {
        [MenuItem("HeraAgent/Tests/ToolDiscovery")]
        public static void RunTests()
        {
            bool allPassed = true;

            allPassed &= ExpectSupported("object action", typeof(ActionShapes).GetMethod("Object"));
            allPassed &= ExpectSupported("Task<object> action", typeof(ActionShapes).GetMethod("AsyncObject"));
            allPassed &= ExpectSupported("Task action", typeof(ActionShapes).GetMethod("Async"));
            allPassed &= ExpectUnsupported("wrong parameter", typeof(ActionShapes).GetMethod("WrongParameter"), "JObject");
            allPassed &= ExpectUnsupported("wrong return", typeof(ActionShapes).GetMethod("WrongReturn"), "return");
            allPassed &= ExpectUnsupported("instance action", typeof(InstanceActionShape).GetMethod("Instance"), "public static");

            var recovered = ToolDiscovery.RecoverLoadableTypes(new ReflectionTypeLoadException(
                new[] { typeof(ActionShapes), null },
                new Exception[] { new InvalidOperationException("missing dependency") }, "partial load"));
            allPassed &= Expect("partial type load keeps non-null types",
                recovered.Length == 1 && recovered[0] == typeof(ActionShapes));

            var metadata = new ToolMetadata(typeof(EmptyDefaultParameters));
            var emptyDefault = metadata.ParametersSchema["properties"]?["label"] as JObject;
            allPassed &= Expect("empty string default is represented",
                emptyDefault? ["default"]?.Type == JTokenType.String
                && emptyDefault.Value<string>("default") == "");
            allPassed &= Expect("empty description is represented",
                emptyDefault? ["description"]?.Type == JTokenType.String
                && emptyDefault.Value<string>("description") == "");

            var names = ToolDiscovery.GetToolNames().Cast<string>().ToArray();
            allPassed &= Expect("tool names are ordinal-sorted",
                names.SequenceEqual(names.OrderBy(name => name, StringComparer.Ordinal)));

            var sceneSchema = JObject.FromObject(ToolDiscovery.GetToolSchema("scene"));
            var actions = sceneSchema["actions"]?.Values<string>("name").ToArray() ?? Array.Empty<string>();
            allPassed &= Expect("scene action descriptors are ordinal-sorted",
                actions.SequenceEqual(actions.OrderBy(name => name, StringComparer.Ordinal))
                && actions.SequenceEqual(new[] { "close", "info", "list", "load", "save" }));
            allPassed &= Expect("unsupported schema capabilities stay false",
                sceneSchema["metadata"]?.Value<bool>("enum_support") == false
                && sceneSchema["metadata"]?.Value<bool>("default_support") == false
                && sceneSchema["metadata"]?.Value<bool>("output_schema_support") == false);

            if (allPassed)
                Debug.Log("[ToolDiscoveryTests] ALL PASSED");
            else
                Debug.LogError("[ToolDiscoveryTests] SOME TESTS FAILED");
        }

        private static bool ExpectSupported(string label, MethodInfo method)
        {
            return Expect(label, ToolDiscovery.IsSupportedActionHandler(method, out _));
        }

        private static bool ExpectUnsupported(string label, MethodInfo method, string expectedDiagnostic)
        {
            var supported = ToolDiscovery.IsSupportedActionHandler(method, out var diagnostic);
            return Expect(label, !supported && diagnostic.IndexOf(expectedDiagnostic, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool Expect(string label, bool passed)
        {
            if (passed)
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError("[FAIL] " + label);
            return false;
        }

        private static class ActionShapes
        {
            [HeraAction]
            public static object Object(JObject parameters) => null;

            [HeraAction]
            public static Task<object> AsyncObject(JObject parameters) => null;

            [HeraAction]
            public static Task Async(JObject parameters) => null;

            [HeraAction]
            public static object WrongParameter(string parameters) => null;

            [HeraAction]
            public static string WrongReturn(JObject parameters) => null;
        }

        private sealed class InstanceActionShape
        {
            [HeraAction]
            public object Instance(JObject parameters) => null;
        }

        private sealed class EmptyDefaultParameters
        {
            [ToolParameter(Description = "", Default = "")]
            public string Label { get; set; }
        }
    }
}
