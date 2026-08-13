using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class ToolProfileTests
    {
        private static readonly IReadOnlyDictionary<string, string[]> ExpectedProfiles
            = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["core"] = new[]
                {
                    "console", "find_gameobjects", "manage_components", "manage_editor",
                    "manage_gameobject", "refresh_unity", "scene", "screenshot",
                },
                ["scene"] = new[]
                {
                    "bake", "find_gameobjects", "manage_animation", "manage_components",
                    "manage_gameobject", "manage_material", "manage_prefab", "refresh_unity",
                    "scene", "screenshot",
                },
                ["assets"] = new[]
                {
                    "describe_shader", "detect_assets", "manage_asset_import", "manage_assets",
                    "manage_material", "manage_packages", "manage_prefab", "refresh_unity",
                    "reserialize",
                },
                ["ui"] = new[]
                {
                    "game_feel", "input", "manage_components", "manage_gameobject", "manage_ui",
                    "screenshot", "ui_slop",
                },
                ["diagnostics"] = new[]
                {
                    "console", "describe_shader", "describe_type", "find_method",
                    "list_assemblies", "log", "manage_settings", "profiler", "run_tests",
                    "screenshot", "unity_docs",
                },
                ["testing"] = new[]
                {
                    "console", "input", "manage_editor", "profiler", "run_tests", "screenshot",
                },
                ["advanced"] = new[] { "exec", "menu" },
            };

        [MenuItem("HeraAgent/Tests/ToolProfiles")]
        public static void RunTests()
        {
            if (RunAll())
                Debug.Log("[ToolProfileTests] ALL PASSED");
            else
                Debug.LogError("[ToolProfileTests] SOME TESTS FAILED");
        }

        internal static bool RunAll()
        {
            var allPassed = true;
            allPassed &= TestExpectedProfiles();
            allPassed &= TestProfileValidation();
            allPassed &= TestNormalProfilesExcludeArbitraryCode();
            allPassed &= TestStrictCustomDefaultsToCustomAndFull();
            allPassed &= TestSafetyRulesParticipateInValidation();
            allPassed &= TestProfileResolutionIsStateless();
            return allPassed;
        }

        private static bool TestExpectedProfiles()
        {
            var contracts = BuiltInContracts();
            foreach (var pair in ExpectedProfiles)
            {
                var actual = contracts
                    .Where(contract => contract.Profiles.Contains(pair.Key))
                    .Select(contract => contract.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                if (!actual.SequenceEqual(pair.Value.OrderBy(name => name, StringComparer.Ordinal)))
                {
                    Debug.LogError(
                        $"[ToolProfileTests] {pair.Key} actual: {string.Join(", ", actual)}");
                    return Expect(nameof(TestExpectedProfiles) + "/" + pair.Key, false);
                }
            }

            var expectedFull = contracts
                .Where(contract => contract.Safety.RiskClass != HeraRiskClass.ArbitraryCode)
                .Select(contract => contract.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var actualFull = contracts
                .Where(contract => contract.Profiles.Contains("full"))
                .Select(contract => contract.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            return Expect(nameof(TestExpectedProfiles), actualFull.SequenceEqual(expectedFull));
        }

        private static bool TestProfileValidation()
        {
            var failures = ToolContractProfiles.Validate(BuiltInContracts());
            Debug.Log(
                $"[ToolProfileTests] profile validation failures = {failures.Count}");
            return Expect(nameof(TestProfileValidation), failures.Count == 0);
        }

        private static bool TestNormalProfilesExcludeArbitraryCode()
        {
            var normalProfiles = new[]
            {
                "core", "scene", "assets", "ui", "diagnostics", "testing", "full",
            };
            var exposed = BuiltInContracts()
                .Where(contract => contract.Profiles.Any(normalProfiles.Contains))
                .SelectMany(contract => new[] { contract.Safety }
                    .Concat(contract.SafetyRules.Select(rule => rule.Safety))
                    .Concat(contract.Actions.Values.Select(action => action.Safety)))
                .Count(safety => safety.RiskClass == HeraRiskClass.ArbitraryCode);
            Debug.Log(
                $"[ToolProfileTests] normal-profile arbitrary-code operations = {exposed}");
            return Expect(nameof(TestNormalProfilesExcludeArbitraryCode), exposed == 0);
        }

        private static bool TestStrictCustomDefaultsToCustomAndFull()
        {
            var contract = ToolContractRegistry.Build(typeof(StrictCustomFixture));
            return Expect(nameof(TestStrictCustomDefaultsToCustomAndFull),
                contract.Profiles.SequenceEqual(new[] { "custom", "full" }));
        }

        private static bool TestSafetyRulesParticipateInValidation()
        {
            var contract = ToolContractRegistry.Build(typeof(ArbitraryRuleFixture));
            var failures = ToolContractProfiles.Validate(new[] { contract });
            return Expect(nameof(TestSafetyRulesParticipateInValidation),
                failures.Count == 1
                && failures[0].Contains("arbitrary code"));
        }

        private static bool TestProfileResolutionIsStateless()
        {
            var first = SnapshotProfiles();
            _ = ToolContractProfiles.Validate(BuiltInContracts());
            var second = SnapshotProfiles();
            return Expect(nameof(TestProfileResolutionIsStateless),
                first.SequenceEqual(second));
        }

        private static ToolContract[] BuiltInContracts()
        {
            return ToolDiscovery.GetToolNames().Cast<string>()
                .Select(ToolContractRegistry.Get)
                .Where(contract => ToolContractSafety.IsBuiltIn(contract.ToolType))
                .OrderBy(contract => contract.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] SnapshotProfiles()
        {
            return BuiltInContracts()
                .Select(contract =>
                    contract.Name + ":" + string.Join(",", contract.Profiles))
                .ToArray();
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

        [HeraTool(
            RiskClass = HeraRiskClass.ReadOnly,
            ContractMode = ToolContractMode.Strict,
            Enabled = false)]
        private static class StrictCustomFixture
        {
        }

        [HeraTool(
            Profiles = new[] { "core" },
            RiskClass = HeraRiskClass.Write,
            ContractMode = ToolContractMode.Strict,
            Enabled = false)]
        [HeraSafetyRule(
            "raw",
            "raw",
            "true",
            RiskClass = HeraRiskClass.ArbitraryCode)]
        private static class ArbitraryRuleFixture
        {
        }
    }
}
