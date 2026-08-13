using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class ToolCatalogTests
    {
        const string SchemaVersion = ProtocolContracts.ToolCatalogSchemaVersion;

        [MenuItem("HeraAgent/Tests/ToolCatalog")]
        public static void RunTests()
        {
            if (RunAll())
                Debug.Log("[ToolCatalogTests] ALL PASSED");
            else
                Debug.LogError("[ToolCatalogTests] SOME TESTS FAILED");
        }

        internal static bool RunAll()
        {
            bool allPassed = true;
            allPassed &= TestCatalogOrderIsDeterministic();
            allPassed &= TestCatalogHashStableForEquivalentContracts();
            allPassed &= TestCatalogHashChangesForContractChange();
            allPassed &= TestCatalogExcludesVolatileFieldsFromHash();
            allPassed &= TestHeartbeatDomainEpochChangesAfterReload();
            allPassed &= TestPackageJobIdsRetainGuidEntropy();
            allPassed &= TestLegacyListShapesRemainCompatible();
            allPassed &= TestCatalogSnapshotIsComplete();
            allPassed &= TestExampleDescriptionsHaveMatchingCalls();
            allPassed &= TestLegacyCustomActionsAreCataloged();
            allPassed &= TestCatalogListModeReturnsValidatedBuiltIns();
            allPassed &= TestExecutionProtocolValidation();
            allPassed &= TestRequestCatalogHashValidation();
            return allPassed;
        }

        static bool TestCatalogOrderIsDeterministic()
        {
            var catalog = ToolCatalogBuilder.Build();
            var toolNames = catalog.Tools.Select(tool => tool.Name).ToArray();
            var actionsSorted = catalog.Tools.All(tool =>
                tool.Actions.Select(action => action.Name).SequenceEqual(
                    tool.Actions.Select(action => action.Name)
                        .OrderBy(name => name, StringComparer.Ordinal)));
            return Expect(
                nameof(TestCatalogOrderIsDeterministic),
                toolNames.SequenceEqual(toolNames.OrderBy(name => name, StringComparer.Ordinal))
                && actionsSorted);
        }

        static bool TestCatalogHashStableForEquivalentContracts()
        {
            var first = ToolCatalogBuilder.Build();
            ToolContractRegistry.Clear();
            var second = ToolCatalogBuilder.Build();
            return Expect(
                nameof(TestCatalogHashStableForEquivalentContracts),
                first.CatalogHash == second.CatalogHash);
        }

        static bool TestCatalogHashChangesForContractChange()
        {
            var catalog = ToolCatalogBuilder.Build();
            var changed = JObject.FromObject(catalog);
            changed["tools"][0]["description"] = changed["tools"][0].Value<string>("description") + " changed";
            return Expect(
                nameof(TestCatalogHashChangesForContractChange),
                catalog.CatalogHash != ToolContractCanonicalJson.ComputeCatalogHash(changed));
        }

        static bool TestCatalogExcludesVolatileFieldsFromHash()
        {
            var catalog = ToolCatalogBuilder.Build();
            var changed = JObject.FromObject(catalog);
            changed["domain_epoch"] = "another-domain";
            changed["project_id"] = "sha256:" + new string('f', 64);
            changed["timestamp"] = 123456789L;
            changed["project_path"] = "/volatile/project";
            changed["port"] = 9999;
            changed["pid"] = 1234;
            return Expect(
                nameof(TestCatalogExcludesVolatileFieldsFromHash),
                catalog.CatalogHash == ToolContractCanonicalJson.ComputeCatalogHash(changed));
        }

        static bool TestHeartbeatDomainEpochChangesAfterReload()
        {
            var current = ToolCatalogRuntime.DomainEpoch;
            var next = ToolCatalogRuntime.CreateDomainEpoch();
            var heartbeat = JObject.FromObject(Heartbeat.BuildStatus());
            var features = heartbeat["features"]?.Values<string>().ToArray()
                ?? Array.Empty<string>();
            return Expect(
                nameof(TestHeartbeatDomainEpochChangesAfterReload),
                !string.IsNullOrEmpty(current)
                && current == ToolCatalogRuntime.DomainEpoch
                && current != next
                && heartbeat.Value<string>("domainEpoch") == current
                && features.SequenceEqual(new[]
                {
                    ProtocolContracts.FeatureApprovalV1,
                    ProtocolContracts.FeatureDomainEpochV1,
                    ProtocolContracts.FeatureExecutionProtocolV1,
                    ProtocolContracts.FeatureOperationLedgerV1,
                    ProtocolContracts.FeatureTaskBridgeV1,
                    ProtocolContracts.FeatureToolCatalogV1,
                }));
        }

        static bool TestLegacyListShapesRemainCompatible()
        {
            var names = JArray.FromObject(ToolDiscovery.GetToolNames());
            var summaries = JArray.FromObject(ToolDiscovery.GetToolSummaries());
            var scene = JObject.FromObject(ToolDiscovery.GetToolSchema("scene"));
            var defaultResponse = ToolCatalogTestSupport.DispatchList(new JObject());
            var namesResponse = ToolCatalogTestSupport.DispatchList(
                new JObject { ["names"] = true });
            var compactResponse = ToolCatalogTestSupport.DispatchList(
                new JObject { ["compact"] = true });
            var sceneResponse = ToolCatalogTestSupport.DispatchList(
                new JObject { ["tool"] = "scene" });
            var expectedSceneFields = new[]
            {
                "name", "description", "group", "groups", "examples", "actions",
                "schema", "output_schema", "metadata",
            };
            return Expect(
                nameof(TestLegacyListShapesRemainCompatible),
                names.All(token => token.Type == JTokenType.String)
                && summaries.All(token =>
                    token.Type == JTokenType.Object
                    && ((JObject)token).Properties().Select(property => property.Name)
                        .SequenceEqual(new[] { "name", "description" }))
                && scene.Properties().Select(property => property.Name)
                    .SequenceEqual(expectedSceneFields)
                && ToolCatalogTestSupport.SerializeData(defaultResponse)
                    == summaries.ToString(Formatting.None)
                && ToolCatalogTestSupport.SerializeData(namesResponse)
                    == names.ToString(Formatting.None)
                && ToolCatalogTestSupport.SerializeData(compactResponse)
                    == names.ToString(Formatting.None)
                && ToolCatalogTestSupport.SerializeData(sceneResponse)
                    == scene.ToString(Formatting.None));
        }

        static bool TestPackageJobIdsRetainGuidEntropy()
        {
            var first = Tools.ManagePackages.CreateJobId();
            var second = Tools.ManagePackages.CreateJobId();
            return Expect(
                nameof(TestPackageJobIdsRetainGuidEntropy),
                first.StartsWith("pkg-", StringComparison.Ordinal)
                && first.Length == 36
                && first.Substring(4).All(Uri.IsHexDigit)
                && first != second);
        }

        static bool TestCatalogSnapshotIsComplete()
        {
            var catalog = ToolCatalogBuilder.Build();
            var hash = catalog.CatalogHash;
            var projectId = catalog.ProjectId;
            var actionCount = catalog.Tools.Sum(tool => tool.Actions.Count);
            var fieldsComplete = catalog.Tools.All(tool =>
                !string.IsNullOrEmpty(tool.Name)
                && !string.IsNullOrEmpty(tool.Title)
                && tool.Description != null
                && tool.Source != null
                && tool.Source.Kind == "builtin"
                && !string.IsNullOrEmpty(tool.Source.Assembly)
                && !string.IsNullOrEmpty(tool.Source.Type)
                && tool.ContractMode == "strict"
                && tool.Profiles != null
                && tool.Aliases != null
                && tool.Examples != null
                && tool.InputSchema != null
                && tool.OutputSchema != null
                && tool.Actions != null
                && tool.Safety != null
                && tool.Actions.All(action =>
                    !string.IsNullOrEmpty(action.Name)
                    && action.Description != null
                    && action.Aliases != null
                    && action.InputSchema != null
                    && action.OutputSchema != null
                    && action.Safety != null));
            return Expect(
                nameof(TestCatalogSnapshotIsComplete),
                catalog.SchemaVersion == SchemaVersion
                && ToolCatalogTestSupport.IsSha256(hash)
                && ToolCatalogTestSupport.IsSha256(projectId)
                && catalog.Tools.Count == 33
                && actionCount == 110
                && fieldsComplete);
        }

        static bool TestExampleDescriptionsHaveMatchingCalls()
        {
            var invalid = typeof(ToolCatalogBuilder).Assembly.GetTypes()
                .Select(type => type.GetCustomAttribute<HeraToolAttribute>())
                .Where(attribute => attribute != null
                    && (attribute.ExampleDescriptions?.Length ?? 0)
                        > (attribute.Examples?.Length ?? 0))
                .ToArray();
            return Expect(
                nameof(TestExampleDescriptionsHaveMatchingCalls),
                invalid.Length == 0);
        }

        static bool TestLegacyCustomActionsAreCataloged()
        {
            var contract = ToolContractRegistry.Build(
                typeof(ToolCatalogLegacyCustomFixture));
            var entry = ToolCatalogBuilder.BuildEntry(contract);
            return Expect(
                nameof(TestLegacyCustomActionsAreCataloged),
                entry.Source.Kind == "custom"
                && entry.ContractMode == "legacy"
                && entry.Actions.Count == 1
                && entry.Actions[0].Name == "ping"
                && entry.Actions[0].Safety.RiskClass == "unspecified"
                && entry.Actions[0].Safety.Destructive
                && entry.Actions[0].Safety.RequiresConfirmation);
        }

        static bool TestCatalogListModeReturnsValidatedBuiltIns()
        {
            var response = CommandRouter.Dispatch(
                "list",
                new JObject
                {
                    ["catalog"] = true,
                    ["schema_version"] = SchemaVersion,
                }).GetAwaiter().GetResult() as SuccessResponse;
            var data = response == null ? null : JObject.FromObject(response.data);
            var tools = data?["tools"] as JArray;
            var builtIns = tools?
                .Where(tool => tool["source"]?.Value<string>("kind") == "builtin")
                .ToArray() ?? Array.Empty<JToken>();
            var projectId = data?.Value<string>("project_id");
            var serialized = data?.ToString(Formatting.None) ?? "";
            var projectPath = System.IO.Path.GetDirectoryName(Application.dataPath) ?? "";
            return Expect(
                nameof(TestCatalogListModeReturnsValidatedBuiltIns),
                data?.Value<string>("schema_version") == SchemaVersion
                && data?.Value<string>("catalog_hash")?.StartsWith("sha256:", StringComparison.Ordinal) == true
                && projectId?.StartsWith("sha256:", StringComparison.Ordinal) == true
                && projectId.Length == 71
                && !string.IsNullOrEmpty(data?.Value<string>("domain_epoch"))
                && builtIns.Length == 33
                && serialized.IndexOf("projectPath", StringComparison.Ordinal) < 0
                && serialized.IndexOf(Application.dataPath, StringComparison.OrdinalIgnoreCase) < 0
                && serialized.IndexOf(projectPath, StringComparison.OrdinalIgnoreCase) < 0);
        }

        static bool TestExecutionProtocolValidation()
        {
            var arguments = new JObject { ["action"] = "info" };
            if (!CommandRequestContext.TryCreate(
                new JObject(), arguments, out var legacy, out _)
                || !CommandRequestContext.TryCreate(
                    new JObject
                    {
                        ["protocol_version"] = ProtocolContracts.ExecutionProtocolVersion,
                    },
                    arguments,
                    out var current,
                    out _)
                || !CommandRequestContext.TryCreate(
                    new JObject
                    {
                        ["protocol_version"] = "hera.execution/999",
                    },
                    arguments,
                    out var future,
                    out _))
            {
                return Expect(nameof(TestExecutionProtocolValidation), false);
            }

            var error = future.ValidateProtocol();
            var data = error?.data == null ? null : JObject.FromObject(error.data);
            return Expect(
                nameof(TestExecutionProtocolValidation),
                legacy.ValidateProtocol() == null
                && current.ValidateProtocol() == null
                && error?.code == "EXECUTION_PROTOCOL_UNSUPPORTED"
                && data?.Value<string>("request_protocol_version") == "hera.execution/999"
                && data?.Value<string>("current_protocol_version")
                    == ProtocolContracts.ExecutionProtocolVersion);
        }
        static bool TestRequestCatalogHashValidation()
        {
            var arguments = new JObject { ["action"] = "info" };
            if (!CommandRequestContext.TryCreate(
                new JObject(),
                arguments,
                out var legacy,
                out _))
            {
                return Expect(nameof(TestRequestCatalogHashValidation), false);
            }

            var currentHash = ToolCatalogRuntime.CatalogHash;
            if (!CommandRequestContext.TryCreate(
                new JObject { ["catalog_hash"] = currentHash },
                arguments,
                out var current,
                out _))
            {
                return Expect(nameof(TestRequestCatalogHashValidation), false);
            }

            var staleHash = "sha256:" + new string('0', 64);
            if (staleHash == currentHash)
                staleHash = "sha256:" + new string('f', 64);
            if (!CommandRequestContext.TryCreate(
                new JObject { ["catalog_hash"] = staleHash },
                arguments,
                out var stale,
                out _))
            {
                return Expect(nameof(TestRequestCatalogHashValidation), false);
            }

            var error = stale.ValidateCatalog();
            var data = error?.data == null ? null : JObject.FromObject(error.data);
            return Expect(
                nameof(TestRequestCatalogHashValidation),
                legacy.ValidateCatalog() == null
                && current.ValidateCatalog() == null
                && error?.code == "CATALOG_STALE"
                && data?.Value<string>("request_catalog_hash") == staleHash
                && data?.Value<string>("current_catalog_hash") == currentHash
                && data?.Value<string>("domain_epoch") == ToolCatalogRuntime.DomainEpoch);
        }

        static bool Expect(string label, bool passed)
        {
            if (passed)
            {
                Debug.Log("[PASS] " + label);
                return true;
            }
            Debug.LogError("[FAIL] " + label);
            return false;
        }
    }
}
