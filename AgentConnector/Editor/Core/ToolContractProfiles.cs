using System;
using System.Collections.Generic;
using System.Linq;

namespace HeraAgent
{
    internal static class ToolContractProfiles
    {
        private static readonly string[] NormalProfiles =
        {
            "core", "scene", "assets", "ui", "diagnostics", "testing", "full",
        };

        internal static string[] Normalize(string[] profiles)
        {
            return (profiles ?? Array.Empty<string>())
                .Where(profile => !string.IsNullOrWhiteSpace(profile))
                .Select(profile => profile.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(profile => profile, StringComparer.Ordinal)
                .ToArray();
        }

        internal static string[] Normalize(
            string[] profiles,
            bool builtIn,
            ToolContractMode mode,
            HeraRiskClass risk)
        {
            var normalized = Normalize(profiles).ToList();
            if (risk == HeraRiskClass.Unspecified)
                return Array.Empty<string>();
            if (!builtIn
                && mode == ToolContractMode.Strict
                && normalized.Count == 0)
            {
                normalized.Add("custom");
            }
            if (mode == ToolContractMode.Strict
                && risk != HeraRiskClass.ArbitraryCode
                && !normalized.Contains("full"))
            {
                normalized.Add("full");
            }
            return normalized
                .Distinct(StringComparer.Ordinal)
                .OrderBy(profile => profile, StringComparer.Ordinal)
                .ToArray();
        }

        internal static IReadOnlyList<string> Validate(
            IReadOnlyList<ToolContract> contracts)
        {
            var failures = new List<string>();
            foreach (var contract in contracts.OrderBy(
                contract => contract.Name,
                StringComparer.Ordinal))
            {
                if (!contract.Profiles.SequenceEqual(
                    contract.Profiles.OrderBy(profile => profile, StringComparer.Ordinal)))
                {
                    failures.Add(contract.Name + ": profile ordering is not deterministic");
                }
                if (contract.Profiles.Count
                    != contract.Profiles.Distinct(StringComparer.Ordinal).Count())
                {
                    failures.Add(contract.Name + ": duplicate profile");
                }
                if (contract.Profiles.Any(NormalProfiles.Contains)
                    && contract.Mode != ToolContractMode.Strict)
                {
                    failures.Add(contract.Name + ": legacy tool in normal profile");
                }
                var safety = new[] { contract.Safety }
                    .Concat(contract.SafetyRules.Select(rule => rule.Safety))
                    .Concat(contract.Actions.Values.Select(action => action.Safety));
                if (contract.Profiles.Any(NormalProfiles.Contains)
                    && safety.Any(item => item.RiskClass == HeraRiskClass.ArbitraryCode))
                {
                    failures.Add(contract.Name + ": arbitrary code in normal profile");
                }
                if (contract.Safety.RiskClass == HeraRiskClass.Unspecified
                    && contract.Profiles.Count != 0)
                {
                    failures.Add(contract.Name + ": unspecified safety is profile-visible");
                }
            }
            return failures;
        }
    }
}
