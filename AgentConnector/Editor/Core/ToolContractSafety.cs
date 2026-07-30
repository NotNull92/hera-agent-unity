using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal sealed class ToolSafetyContract
    {
        public HeraRiskClass RiskClass { get; set; }
        public bool ReadOnly { get; set; }
        public bool Destructive { get; set; }
        public bool Idempotent { get; set; }
        public bool MayReloadDomain { get; set; }
        public bool RequiresPlayMode { get; set; }
        public bool RequiresConfirmation { get; set; }
        public bool Reversible { get; set; }
        public bool SupportsCancellation { get; set; }
        public string SideEffectScope { get; set; }
    }

    internal sealed class ToolSafetyRule
    {
        public string Operation { get; set; }
        public JObject When { get; set; }
        public ToolSafetyContract Safety { get; set; }
    }

    internal sealed class McpToolAnnotations
    {
        public bool ReadOnlyHint { get; set; }
        public bool DestructiveHint { get; set; }
        public bool IdempotentHint { get; set; }
        public bool OpenWorldHint { get; set; }
    }

    internal static class ToolContractSafety
    {
        internal static bool IsBuiltIn(Type type)
        {
            return type?.Namespace == "HeraAgent.Tools"
                || type?.Namespace == "HeraAgent.TestRunner";
        }

        internal static ToolSafetyContract From(
            HeraToolAttribute attribute,
            Type owner)
        {
            return Normalize(
                attribute?.RiskClass ?? HeraRiskClass.Unspecified,
                attribute?.ReadOnly ?? false,
                attribute?.Destructive ?? false,
                attribute?.Idempotent ?? false,
                attribute?.MayReloadDomain ?? false,
                attribute?.RequiresPlayMode ?? false,
                attribute?.RequiresConfirmation ?? false,
                attribute?.Reversible ?? false,
                attribute?.SupportsCancellation ?? false,
                owner);
        }

        internal static ToolSafetyContract From(
            HeraActionContractAttribute attribute,
            Type owner)
        {
            return Normalize(
                attribute?.RiskClass ?? HeraRiskClass.Unspecified,
                attribute?.ReadOnly ?? false,
                attribute?.Destructive ?? false,
                attribute?.Idempotent ?? false,
                attribute?.MayReloadDomain ?? false,
                attribute?.RequiresPlayMode ?? false,
                attribute?.RequiresConfirmation ?? false,
                attribute?.Reversible ?? false,
                attribute?.SupportsCancellation ?? false,
                owner);
        }

        internal static ToolSafetyContract From(
            HeraActionAttribute attribute,
            Type owner)
        {
            return Normalize(
                attribute?.RiskClass ?? HeraRiskClass.Unspecified,
                attribute?.ReadOnly ?? false,
                attribute?.Destructive ?? false,
                attribute?.Idempotent ?? false,
                attribute?.MayReloadDomain ?? false,
                attribute?.RequiresPlayMode ?? false,
                attribute?.RequiresConfirmation ?? false,
                attribute?.Reversible ?? false,
                attribute?.SupportsCancellation ?? false,
                owner);
        }

        internal static ToolSafetyContract Apply(
            ToolSafetyContract current,
            HeraActionSafetyAttribute attribute,
            Type owner)
        {
            if (attribute == null)
                return current;
            var currentIsClassified =
                current.RiskClass != HeraRiskClass.Unspecified;
            var risk = attribute.RiskClass == HeraRiskClass.Unspecified
                ? current.RiskClass
                : attribute.RiskClass;
            return Normalize(
                risk,
                (currentIsClassified && current.ReadOnly) || attribute.ReadOnly,
                (currentIsClassified && current.Destructive) || attribute.Destructive,
                (currentIsClassified && current.Idempotent) || attribute.Idempotent,
                (currentIsClassified && current.MayReloadDomain) || attribute.MayReloadDomain,
                (currentIsClassified && current.RequiresPlayMode) || attribute.RequiresPlayMode,
                (currentIsClassified && current.RequiresConfirmation)
                    || attribute.RequiresConfirmation,
                (currentIsClassified && current.Reversible) || attribute.Reversible,
                (currentIsClassified && current.SupportsCancellation)
                    || attribute.SupportsCancellation,
                owner);
        }

        internal static ToolSafetyContract EnsureClassified(
            ToolSafetyContract safety,
            Type owner)
        {
            if (safety.RiskClass == HeraRiskClass.Unspecified && IsBuiltIn(owner))
            {
                throw new SchemaGenerationException(
                    owner,
                    "Built-in tools and actions require an explicit safety classification.");
            }
            return safety;
        }

        internal static IReadOnlyList<ToolSafetyRule> BuildRules(
            Type owner,
            ToolSafetyContract fallback)
        {
            return ToolContractSafetyRules.Build(owner, fallback);
        }

        internal static ToolSafetyContract Resolve(
            ToolSafetyContract fallback,
            IReadOnlyList<ToolSafetyRule> rules,
            JObject arguments)
        {
            return ToolContractSafetyRules.Resolve(fallback, rules, arguments);
        }

        internal static McpToolAnnotations ToMcpAnnotations(ToolContract contract)
        {
            var safety = new[] { contract.Safety }
                .Concat(contract.SafetyRules.Select(rule => rule.Safety))
                .Concat(contract.Actions.Values.Select(action => action.Safety))
                .ToArray();
            return new McpToolAnnotations
            {
                ReadOnlyHint = safety.All(item => item.ReadOnly),
                DestructiveHint = safety.Any(item => item.Destructive),
                IdempotentHint = safety.All(item => item.Idempotent),
                OpenWorldHint = safety.Any(item =>
                    item.RiskClass == HeraRiskClass.ArbitraryCode
                    || item.RiskClass == HeraRiskClass.PackageChange
                    || item.RiskClass == HeraRiskClass.ExternalProcess),
            };
        }

        internal static ToolSafetyContract Normalize(
            HeraRiskClass risk,
            bool readOnly,
            bool destructive,
            bool idempotent,
            bool mayReloadDomain,
            bool requiresPlayMode,
            bool requiresConfirmation,
            bool reversible,
            bool supportsCancellation,
            Type owner,
            ToolSafetyContract fallback = null)
        {
            if (risk == HeraRiskClass.Unspecified)
            {
                if (readOnly)
                    risk = HeraRiskClass.ReadOnly;
                else if (destructive)
                    risk = HeraRiskClass.Destructive;
                else if (fallback != null)
                    risk = fallback.RiskClass;
            }
            if (risk == HeraRiskClass.Unspecified && IsBuiltIn(owner))
            {
                throw new SchemaGenerationException(
                    owner,
                    "Built-in tools and actions require an explicit safety classification.");
            }

            var conservativeUnknown = risk == HeraRiskClass.Unspecified;
            readOnly = risk == HeraRiskClass.ReadOnly;
            destructive = destructive
                || risk == HeraRiskClass.Destructive
                || conservativeUnknown;
            idempotent = readOnly || idempotent;
            requiresConfirmation = requiresConfirmation
                || destructive
                || risk == HeraRiskClass.ArbitraryCode
                || risk == HeraRiskClass.PackageChange
                || risk == HeraRiskClass.ExternalProcess;
            return new ToolSafetyContract
            {
                RiskClass = risk,
                ReadOnly = readOnly,
                Destructive = destructive,
                Idempotent = conservativeUnknown ? false : idempotent,
                MayReloadDomain = mayReloadDomain,
                RequiresPlayMode = requiresPlayMode,
                RequiresConfirmation = requiresConfirmation,
                Reversible = reversible,
                SupportsCancellation = supportsCancellation,
                SideEffectScope = SideEffectScope(risk),
            };
        }

        private static string SideEffectScope(HeraRiskClass risk)
        {
            switch (risk)
            {
                case HeraRiskClass.ReadOnly:
                    return "none";
                case HeraRiskClass.PackageChange:
                    return "package_environment";
                case HeraRiskClass.ExternalProcess:
                    return "external_process";
                case HeraRiskClass.ArbitraryCode:
                    return "unity_editor_and_project";
                default:
                    return "unity_project";
            }
        }

    }
}
