using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal static class ToolCatalogBuilder
    {
        internal const string SchemaVersion = "hera.tool-catalog/1";

        internal static ToolCatalogEnvelope Build()
        {
            var tools = ToolDiscovery.GetToolNames()
                .Cast<string>()
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => ToolContractRegistry.Get(name)
                    ?? throw new SchemaGenerationException(
                        null,
                        $"Tool contract not found: {name}"))
                .Select(BuildEntry)
                .ToArray();
            var catalog = new ToolCatalogEnvelope
            {
                SchemaVersion = SchemaVersion,
                DomainEpoch = ToolCatalogRuntime.DomainEpoch,
                ProjectId = ToolCatalogRuntime.ProjectId,
                Tools = tools,
            };
            catalog.CatalogHash = ToolContractCanonicalJson.ComputeCatalogHash(
                JObject.FromObject(catalog));
            return catalog;
        }

        internal static ToolCatalogEntry BuildEntry(ToolContract contract)
        {
            if (contract == null)
                throw new ArgumentNullException(nameof(contract));
            var name = contract.Name;
            var attribute = contract.ToolType.GetCustomAttribute<HeraToolAttribute>();
            return new ToolCatalogEntry
            {
                Name = name,
                Title = string.IsNullOrWhiteSpace(attribute?.Title)
                    ? BuildTitle(name)
                    : attribute.Title.Trim(),
                Description = attribute?.Description ?? "",
                Source = new ToolCatalogSource
                {
                    Kind = ToolContractSafety.IsBuiltIn(contract.ToolType)
                        ? "builtin"
                        : "custom",
                    Assembly = contract.ToolType.Assembly.GetName().Name,
                    Type = contract.ToolType.FullName,
                },
                ContractMode = contract.Mode == ToolContractMode.Strict ? "strict" : "legacy",
                Profiles = contract.Profiles
                    .OrderBy(profile => profile, StringComparer.Ordinal)
                    .ToArray(),
                Aliases = Array.Empty<string>(),
                Examples = BuildExamples(attribute),
                InputSchema = ToolContractCanonicalJson.Canonicalize(contract.InputSchema),
                OutputSchema = ToolContractCanonicalJson.Canonicalize(contract.OutputSchema),
                Actions = contract.Actions.Values
                    .OrderBy(action => action.Name, StringComparer.Ordinal)
                    .Select(BuildAction)
                    .ToArray(),
                Safety = BuildSafety(contract.Safety, contract.SafetyRules),
            };
        }

        static ToolCatalogAction BuildAction(ToolActionContract action)
        {
            return new ToolCatalogAction
            {
                Name = action.Name,
                Description = action.Description ?? "",
                Aliases = action.Aliases
                    .OrderBy(alias => alias, StringComparer.Ordinal)
                    .ToArray(),
                InputSchema = ToolContractCanonicalJson.Canonicalize(action.InputSchema),
                OutputSchema = ToolContractCanonicalJson.Canonicalize(action.OutputSchema),
                Safety = BuildSafety(action.Safety, Array.Empty<ToolSafetyRule>()),
            };
        }

        static ToolCatalogSafety BuildSafety(
            ToolSafetyContract safety,
            IReadOnlyList<ToolSafetyRule> rules)
        {
            return new ToolCatalogSafety
            {
                RiskClass = RiskName(safety.RiskClass),
                ReadOnly = safety.ReadOnly,
                Destructive = safety.Destructive,
                Idempotent = safety.Idempotent,
                MayReloadDomain = safety.MayReloadDomain,
                RequiresPlayMode = safety.RequiresPlayMode,
                RequiresConfirmation = safety.RequiresConfirmation,
                Reversible = safety.Reversible,
                SupportsCancellation = safety.SupportsCancellation,
                SideEffectScope = safety.SideEffectScope,
                Rules = rules
                    .OrderBy(rule => rule.Operation, StringComparer.Ordinal)
                    .ThenBy(rule => rule.When?.ToString(), StringComparer.Ordinal)
                    .Select(BuildSafetyRule)
                    .ToArray(),
            };
        }

        static ToolCatalogSafetyRule BuildSafetyRule(ToolSafetyRule rule)
        {
            var safety = rule.Safety;
            return new ToolCatalogSafetyRule
            {
                Operation = rule.Operation,
                When = ToolContractCanonicalJson.Canonicalize(rule.When),
                RiskClass = RiskName(safety.RiskClass),
                ReadOnly = safety.ReadOnly,
                Destructive = safety.Destructive,
                Idempotent = safety.Idempotent,
                MayReloadDomain = safety.MayReloadDomain,
                RequiresPlayMode = safety.RequiresPlayMode,
                RequiresConfirmation = safety.RequiresConfirmation,
                Reversible = safety.Reversible,
                SupportsCancellation = safety.SupportsCancellation,
                SideEffectScope = safety.SideEffectScope,
                Rules = Array.Empty<ToolCatalogSafetyRule>(),
            };
        }

        static IReadOnlyList<ToolCatalogExample> BuildExamples(HeraToolAttribute attribute)
        {
            var calls = attribute?.Examples ?? Array.Empty<string>();
            var descriptions = attribute?.ExampleDescriptions ?? Array.Empty<string>();
            return calls.Select((call, index) => new ToolCatalogExample
            {
                Call = call,
                Description = index < descriptions.Length ? descriptions[index] : "",
            }).ToArray();
        }

        static string BuildTitle(string name)
        {
            return string.Join(" ", name.Split('_')
                .Where(part => part.Length > 0)
                .Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }

        internal static string RiskName(HeraRiskClass risk)
        {
            return risk switch
            {
                HeraRiskClass.Unspecified => "unspecified",
                HeraRiskClass.ReadOnly => "read_only",
                HeraRiskClass.Write => "write",
                HeraRiskClass.Destructive => "destructive",
                HeraRiskClass.ArbitraryCode => "arbitrary_code",
                HeraRiskClass.PackageChange => "package_change",
                HeraRiskClass.ExternalProcess => "external_process",
                _ => throw new ArgumentOutOfRangeException(nameof(risk), risk, null),
            };
        }
    }
}
