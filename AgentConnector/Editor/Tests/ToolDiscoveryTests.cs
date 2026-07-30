using System;
using System.Collections.Generic;
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

            allPassed &= TestNoPropertyLevelBooleanRequired(out var propertyRequiredCount);
            allPassed &= TestRequiredIsTopLevelStringArray();
            allPassed &= TestArraysDeclareItems();
            allPassed &= TestNestedObjectsDeclareProperties();
            allPassed &= TestNullableTypesAllowNull();
            allPassed &= TestUnsupportedTypesFailCatalogBuild();
            allPassed &= TestAllSchemasPassDraft202012MetaSchema(out var invalidRuntimeSchemaCount);
            allPassed &= TestCatalogSchemasAreDeterministic();
            allPassed &= TestRuntimeToolAndActionNamesUnchanged();
            allPassed &= TestExternalResponseFieldsArePreserved();

            Debug.Log(
                $"[ToolDiscoveryTests] property-level required boolean count = {propertyRequiredCount}; " +
                $"invalid runtime schema count = {invalidRuntimeSchemaCount}");

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

        private static bool TestNoPropertyLevelBooleanRequired(out int count)
        {
            count = RuntimeSchemaTokens()
                .SelectMany(schema => schema.DescendantsAndSelf())
                .OfType<JProperty>()
                .Count(property => property.Name == "required"
                    && property.Value.Type == JTokenType.Boolean);
            return Expect(nameof(TestNoPropertyLevelBooleanRequired), count == 0);
        }

        private static bool TestRequiredIsTopLevelStringArray()
        {
            var valid = RuntimeSchemaTokens()
                .SelectMany(schema => schema.DescendantsAndSelf())
                .OfType<JProperty>()
                .Where(property => property.Name == "required")
                .All(property => property.Value is JArray required
                    && required.All(item => item.Type == JTokenType.String));
            return Expect(nameof(TestRequiredIsTopLevelStringArray), valid);
        }

        private static bool TestArraysDeclareItems()
        {
            var valid = RuntimeSchemaTokens()
                .SelectMany(schema => schema.DescendantsAndSelf())
                .OfType<JObject>()
                .Where(schema => SchemaAllowsType(schema, "array"))
                .All(schema => schema["items"] is JObject);
            return Expect(nameof(TestArraysDeclareItems), valid);
        }

        private static bool TestNestedObjectsDeclareProperties()
        {
            var nested = SchemaUtility.GenerateSchema(typeof(NestedShape));
            var nestedProperties = nested["properties"] as JObject;
            var childProperties = nestedProperties?["child"]?["properties"] as JObject;
            var runtimeObjectsValid = RuntimeSchemaTokens()
                .SelectMany(schema => schema.DescendantsAndSelf())
                .OfType<JObject>()
                .Where(schema => SchemaAllowsType(schema, "object"))
                .All(schema => schema["properties"] is JObject);

            return Expect(nameof(TestNestedObjectsDeclareProperties),
                nestedProperties != null
                && childProperties != null
                && childProperties["label"] is JObject
                && runtimeObjectsValid);
        }

        private static bool TestNullableTypesAllowNull()
        {
            var nullable = SchemaUtility.GenerateSchema(typeof(int?));
            return Expect(nameof(TestNullableTypesAllowNull),
                SchemaAllowsType(nullable, "integer")
                && SchemaAllowsType(nullable, "null"));
        }

        private static bool TestUnsupportedTypesFailCatalogBuild()
        {
            try
            {
                _ = new ToolMetadata(typeof(UnsupportedSchemaTool));
                return Expect(nameof(TestUnsupportedTypesFailCatalogBuild), false);
            }
            catch (SchemaGenerationException exception)
            {
                return Expect(nameof(TestUnsupportedTypesFailCatalogBuild),
                    exception.UnsupportedType == typeof(DateTime));
            }
        }

        private static bool TestAllSchemasPassDraft202012MetaSchema(
            out int invalidRuntimeSchemaCount)
        {
            invalidRuntimeSchemaCount = 0;
            foreach (var toolName in ToolDiscovery.GetToolNames().Cast<string>())
            {
                var tool = JObject.FromObject(ToolDiscovery.GetToolSchema(toolName));
                var errors = new List<string>();
                ValidateSchema(tool["schema"] as JObject, "$.schema", errors);
                ValidateSchema(tool["output_schema"] as JObject, "$.output_schema", errors);
                if (errors.Count == 0) continue;

                invalidRuntimeSchemaCount++;
                Debug.LogError(
                    $"[FAIL] {toolName} schema: {string.Join("; ", errors.Take(3))}");
            }

            return Expect(nameof(TestAllSchemasPassDraft202012MetaSchema),
                invalidRuntimeSchemaCount == 0);
        }

        private static bool TestCatalogSchemasAreDeterministic()
        {
            foreach (var toolName in ToolDiscovery.GetToolNames().Cast<string>())
            {
                var first = JObject.FromObject(ToolDiscovery.GetToolSchema(toolName));
                var second = JObject.FromObject(ToolDiscovery.GetToolSchema(toolName));
                if (!JToken.DeepEquals(first["schema"], second["schema"])
                    || !JToken.DeepEquals(first["output_schema"], second["output_schema"])
                    || !HasCanonicalObjectOrdering(first["schema"])
                    || !HasCanonicalObjectOrdering(first["output_schema"]))
                {
                    return Expect(nameof(TestCatalogSchemasAreDeterministic), false);
                }
            }

            return Expect(nameof(TestCatalogSchemasAreDeterministic), true);
        }

        private static bool TestRuntimeToolAndActionNamesUnchanged()
        {
            var expectedTools = new[]
            {
                "console", "describe_shader", "describe_type", "detect_assets", "exec",
                "find_gameobjects", "find_method", "game_feel", "input", "list_assemblies",
                "log", "manage_animation", "manage_asset_import", "manage_assets",
                "manage_components", "manage_editor", "manage_gameobject", "manage_material",
                "manage_packages", "manage_prefab", "manage_ui", "menu", "profiler",
                "refresh_unity", "reserialize", "run_tests", "scene", "screenshot",
                "ui_doc", "ui_slop", "unity_docs",
            };
            var expectedActions = new Dictionary<string, string[]>
            {
                ["manage_components"] = new[] { "add", "get", "list", "remove", "set" },
                ["manage_gameobject"] = new[]
                {
                    "create", "destroy", "duplicate", "get_transform", "move",
                    "set_active", "set_name", "set_parent",
                },
                ["manage_packages"] = new[] { "add", "embed", "list", "remove" },
                ["manage_ui"] = new[] { "create", "get_rect", "set_anchor", "set_rect" },
                ["menu"] = new[] { "list" },
                ["scene"] = new[] { "close", "info", "list", "load", "save" },
            };

            var actualTools = ToolDiscovery.GetToolNames().Cast<string>().ToArray();
            if (!actualTools.SequenceEqual(expectedTools))
                return Expect(nameof(TestRuntimeToolAndActionNamesUnchanged), false);

            var actionCount = 0;
            foreach (var toolName in actualTools)
            {
                var tool = JObject.FromObject(ToolDiscovery.GetToolSchema(toolName));
                var actualActions = tool["actions"]?.Values<string>("name").ToArray()
                    ?? Array.Empty<string>();
                var expected = expectedActions.TryGetValue(toolName, out var actions)
                    ? actions
                    : Array.Empty<string>();
                if (!actualActions.SequenceEqual(expected))
                    return Expect(nameof(TestRuntimeToolAndActionNamesUnchanged), false);
                actionCount += actualActions.Length;
            }

            Debug.Log(
                $"[ToolDiscoveryTests] tool names unchanged = true ({actualTools.Length}); " +
                $"action names unchanged = true ({actionCount})");
            return Expect(nameof(TestRuntimeToolAndActionNamesUnchanged),
                actualTools.Length == 31 && actionCount == 27);
        }

        private static bool TestExternalResponseFieldsArePreserved()
        {
            var tool = JObject.FromObject(ToolDiscovery.GetToolSchema("scene"));
            var expected = new[]
            {
                "name", "description", "group", "groups", "examples", "actions",
                "schema", "output_schema", "metadata",
            };
            return Expect(nameof(TestExternalResponseFieldsArePreserved),
                tool.Properties().Select(property => property.Name).SequenceEqual(expected));
        }

        private static IEnumerable<JObject> RuntimeSchemaTokens()
        {
            foreach (var toolName in ToolDiscovery.GetToolNames().Cast<string>())
            {
                var tool = JObject.FromObject(ToolDiscovery.GetToolSchema(toolName));
                if (tool["schema"] is JObject input) yield return input;
                if (tool["output_schema"] is JObject output) yield return output;
            }
        }

        private static void ValidateSchema(
            JObject schema,
            string path,
            ICollection<string> errors)
        {
            if (schema == null)
            {
                errors.Add(path + " is not an object schema");
                return;
            }

            if (!HasValidTypeKeyword(schema["type"]))
                errors.Add(path + ".type is invalid");

            if (schema["required"] is JToken required
                && (!(required is JArray requiredArray)
                    || requiredArray.Any(item => item.Type != JTokenType.String)))
            {
                errors.Add(path + ".required must be a string array");
            }

            if (SchemaAllowsType(schema, "array") && !(schema["items"] is JObject))
                errors.Add(path + ".items is required for arrays");

            if (SchemaAllowsType(schema, "object") && !(schema["properties"] is JObject))
                errors.Add(path + ".properties is required for objects");

            if (schema["properties"] is JObject properties)
            {
                foreach (var property in properties.Properties())
                {
                    if (property.Value is JObject propertySchema)
                        ValidateSchema(propertySchema, path + ".properties." + property.Name, errors);
                    else
                        errors.Add(path + ".properties." + property.Name + " is not a schema");
                }
            }

            if (schema["items"] is JObject items)
                ValidateSchema(items, path + ".items", errors);

            foreach (var combinator in new[] { "anyOf", "oneOf", "allOf" })
            {
                if (!(schema[combinator] is JArray branches)) continue;
                foreach (var branch in branches.OfType<JObject>())
                    ValidateSchema(branch, path + "." + combinator, errors);
            }

            if (schema["additionalProperties"] is JObject additionalProperties)
                ValidateSchema(additionalProperties, path + ".additionalProperties", errors);
        }

        private static bool HasValidTypeKeyword(JToken type)
        {
            if (type == null) return true;
            if (type.Type == JTokenType.String)
                return IsJsonSchemaType(type.Value<string>());
            return type is JArray types
                && types.Count > 0
                && types.All(item => item.Type == JTokenType.String
                    && IsJsonSchemaType(item.Value<string>()));
        }

        private static bool IsJsonSchemaType(string type)
        {
            return type == "null"
                || type == "boolean"
                || type == "object"
                || type == "array"
                || type == "number"
                || type == "integer"
                || type == "string";
        }

        private static bool SchemaAllowsType(JObject schema, string expected)
        {
            var type = schema?["type"];
            return type?.Type == JTokenType.String
                ? type.Value<string>() == expected
                : type is JArray types && types.Values<string>().Contains(expected);
        }

        private static bool HasCanonicalObjectOrdering(JToken token)
        {
            if (token is JObject obj)
            {
                var names = obj.Properties().Select(property => property.Name).ToArray();
                if (!names.SequenceEqual(names.OrderBy(name => name, StringComparer.Ordinal)))
                    return false;
            }

            return token == null || token.Children().All(HasCanonicalObjectOrdering);
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

        private sealed class NestedShape
        {
            public NestedChild Child { get; set; }
        }

        private sealed class NestedChild
        {
            public string Label { get; set; }
        }

        private sealed class UnsupportedSchemaTool
        {
            public sealed class Parameters
            {
                [ToolParameter("Unsupported test value.")]
                public DateTime Value { get; set; }
            }
        }
    }
}
