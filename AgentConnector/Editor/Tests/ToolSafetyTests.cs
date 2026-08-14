using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class ToolSafetyTests
    {
        [MenuItem("HeraAgent/Tests/ToolSafety")]
        public static void RunTests()
        {
            if (RunAll())
                Debug.Log("[ToolSafetyTests] ALL PASSED");
            else
                Debug.LogError("[ToolSafetyTests] SOME TESTS FAILED");
        }

        internal static bool RunAll()
        {
            var allPassed = true;
            allPassed &= TestEveryBuiltInOperationClassified();
            allPassed &= TestHandlerDerivedRiskAudit();
            allPassed &= TestParameterDependentRules();
            allPassed &= TestLegacyBooleanNormalization();
            allPassed &= TestUnspecifiedCustomIsConservative();
            allPassed &= TestUnspecifiedBuiltInFails();
            allPassed &= TestConservativeMcpAnnotations();
            allPassed &= TestAmbiguousRulesFailValidation();
            return allPassed;
        }

        private static bool TestEveryBuiltInOperationClassified()
        {
            var contracts = BuiltInContracts();
            var actionCount = contracts.Sum(contract => contract.Actions.Count);
            var unclassified = contracts.Count(contract =>
                    contract.Safety.RiskClass == HeraRiskClass.Unspecified)
                + contracts.Sum(contract => contract.Actions.Values.Count(action =>
                    action.Safety.RiskClass == HeraRiskClass.Unspecified));
            Debug.Log(
                $"[ToolSafetyTests] built-in tools = {contracts.Length}; " +
                $"actions = {actionCount}; " +
                $"unclassified built-in tools/actions = {unclassified}");
            return Expect(nameof(TestEveryBuiltInOperationClassified),
                contracts.Length == 34 && actionCount == 132 && unclassified == 0);
        }

        private static bool TestHandlerDerivedRiskAudit()
        {
            var actual = BuiltInContracts()
                .SelectMany(contract => new[]
                    {
                        new KeyValuePair<string, HeraRiskClass>(
                            contract.Name, contract.Safety.RiskClass)
                    }
                    .Concat(contract.Actions.Values.Select(action =>
                        new KeyValuePair<string, HeraRiskClass>(
                            contract.Name + "/" + action.Name,
                            action.Safety.RiskClass))))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

            var riskMatches = ToolSafetyExpectations.ExpectedRisk.All(pair =>
                actual.TryGetValue(pair.Key, out var risk) && risk == pair.Value);
            var mismatches = ToolSafetyExpectations.ExpectedRisk
                .Where(pair => !actual.TryGetValue(pair.Key, out var risk)
                    || risk != pair.Value)
                .Select(pair => pair.Key + "="
                    + (actual.TryGetValue(pair.Key, out var risk)
                        ? risk.ToString()
                        : "<missing>")
                    + " (expected " + pair.Value + ")")
                .ToArray();
            if (mismatches.Length != 0)
                Debug.LogError("[ToolSafetyTests] risk mismatches: " + string.Join(", ", mismatches));
            var remainingWrites = actual
                .Where(pair => pair.Value == HeraRiskClass.Unspecified)
                .Select(pair => pair.Key)
                .ToArray();
            var confirmationsAreConservative = BuiltInContracts()
                .SelectMany(AllSafety)
                .Where(safety => safety.RiskClass == HeraRiskClass.Destructive
                    || safety.RiskClass == HeraRiskClass.ArbitraryCode
                    || safety.RiskClass == HeraRiskClass.PackageChange)
                .All(safety => safety.RequiresConfirmation);
            var readOnlyIsNotDestructive = BuiltInContracts()
                .SelectMany(AllSafety)
                .Where(safety => safety.RiskClass == HeraRiskClass.ReadOnly)
                .All(safety => !safety.Destructive
                    && !safety.RequiresConfirmation);

            return Expect(nameof(TestHandlerDerivedRiskAudit),
                riskMatches
                && actual.Count == ToolSafetyExpectations.ExpectedRisk.Count
                && remainingWrites.Length == 0
                && confirmationsAreConservative
                && readOnlyIsNotDestructive);
        }

        private static bool TestParameterDependentRules()
        {
            var console = ToolContractRegistry.Get("console");
            var consoleRead = ToolContractSafety.Resolve(console.Safety, console.SafetyRules, new JObject());
            var consoleClear = ToolContractSafety.Resolve(
                console.Safety,
                console.SafetyRules,
                new JObject { ["clear"] = true });
            var exec = ToolContractRegistry.Get("exec");
            var execRun = ToolContractSafety.Resolve(exec.Safety, exec.SafetyRules, new JObject());
            var execCheck = ToolContractSafety.Resolve(
                exec.Safety,
                exec.SafetyRules,
                new JObject { ["compile_only"] = true });
            return Expect(nameof(TestParameterDependentRules),
                consoleRead.RiskClass == HeraRiskClass.ReadOnly
                && consoleClear.RiskClass == HeraRiskClass.Destructive
                && consoleClear.RequiresConfirmation
                && consoleClear.Idempotent
                && execRun.RiskClass == HeraRiskClass.ArbitraryCode
                && execCheck.RiskClass == HeraRiskClass.Write);
        }

        private static bool TestLegacyBooleanNormalization()
        {
            var contract = ToolContractRegistry.Build(typeof(LegacyReadOnlyFixture));
            return Expect(nameof(TestLegacyBooleanNormalization),
                contract.Safety.RiskClass == HeraRiskClass.ReadOnly
                && contract.Safety.ReadOnly
                && contract.Safety.Idempotent
                && !contract.Safety.Destructive);
        }

        private static bool TestUnspecifiedCustomIsConservative()
        {
            var contract = ToolContractRegistry.Build(typeof(UnspecifiedCustomFixture));
            return Expect(nameof(TestUnspecifiedCustomIsConservative),
                contract.Safety.RiskClass == HeraRiskClass.Unspecified
                && contract.Safety.Destructive
                && contract.Safety.RequiresConfirmation
                && !contract.Safety.Idempotent
                && contract.Profiles.Count == 0);
        }

        private static bool TestUnspecifiedBuiltInFails()
        {
            try
            {
                _ = ToolContractSafety.EnsureClassified(
                    new ToolSafetyContract
                    {
                        RiskClass = HeraRiskClass.Unspecified,
                    },
                    typeof(Tools.ManageScene));
                return Expect(nameof(TestUnspecifiedBuiltInFails), false);
            }
            catch (SchemaGenerationException)
            {
                return Expect(nameof(TestUnspecifiedBuiltInFails), true);
            }
        }

        private static bool TestConservativeMcpAnnotations()
        {
            var readonlyHints = ToolContractSafety.ToMcpAnnotations(
                ToolContractRegistry.Get("describe_type"));
            var mixedHints = ToolContractSafety.ToMcpAnnotations(
                ToolContractRegistry.Get("manage_assets"));
            var execHints = ToolContractSafety.ToMcpAnnotations(
                ToolContractRegistry.Get("exec"));

            return Expect(nameof(TestConservativeMcpAnnotations),
                readonlyHints.ReadOnlyHint && readonlyHints.IdempotentHint
                && !readonlyHints.DestructiveHint && !readonlyHints.OpenWorldHint
                && !mixedHints.ReadOnlyHint && mixedHints.DestructiveHint
                && !mixedHints.IdempotentHint
                && execHints.OpenWorldHint && !execHints.ReadOnlyHint);
        }

        private static bool TestAmbiguousRulesFailValidation()
        {
            try
            {
                _ = ToolContractRegistry.Build(typeof(AmbiguousRuleFixture));
                return Expect(nameof(TestAmbiguousRulesFailValidation), false);
            }
            catch (SchemaGenerationException)
            {
                return Expect(nameof(TestAmbiguousRulesFailValidation), true);
            }
        }

        private static ToolContract[] BuiltInContracts()
        {
            return ToolDiscovery.GetToolNames().Cast<string>()
                .Select(ToolContractRegistry.Get)
                .Where(contract => ToolContractSafety.IsBuiltIn(contract.ToolType))
                .OrderBy(contract => contract.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private static IEnumerable<ToolSafetyContract> AllSafety(ToolContract contract)
        {
            yield return contract.Safety;
            foreach (var action in contract.Actions.Values)
                yield return action.Safety;
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
            ReadOnly = true,
            Idempotent = true,
            ContractMode = ToolContractMode.Strict,
            Enabled = false)]
        private static class LegacyReadOnlyFixture
        {
        }

        [HeraTool(ContractMode = ToolContractMode.Strict, Enabled = false)]
        private static class UnspecifiedCustomFixture
        {
        }

        [HeraTool(
            RiskClass = HeraRiskClass.ReadOnly,
            ContractMode = ToolContractMode.Strict,
            Enabled = false)]
        [HeraSafetyRule(
            "first",
            "enabled",
            "true",
            RiskClass = HeraRiskClass.Write)]
        [HeraSafetyRule(
            "second",
            "force",
            "true",
            RiskClass = HeraRiskClass.Destructive)]
        private static class AmbiguousRuleFixture
        {
        }
    }
}
