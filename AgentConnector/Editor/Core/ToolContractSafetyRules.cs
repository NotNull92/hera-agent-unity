using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal static class ToolContractSafetyRules
    {
        internal static IReadOnlyList<ToolSafetyRule> Build(
            Type owner,
            ToolSafetyContract fallback)
        {
            var rules = owner.GetCustomAttributes(typeof(HeraSafetyRuleAttribute), false)
                .Cast<HeraSafetyRuleAttribute>()
                .Select(attribute => new ToolSafetyRule
                {
                    Operation = attribute.Operation?.Trim() ?? "",
                    When = BuildWhen(owner, attribute),
                    Safety = ToolContractSafety.Normalize(
                        attribute.RiskClass,
                        attribute.ReadOnly,
                        attribute.Destructive,
                        attribute.Idempotent,
                        attribute.MayReloadDomain,
                        attribute.RequiresPlayMode,
                        attribute.RequiresConfirmation,
                        attribute.Reversible,
                        attribute.SupportsCancellation,
                        owner,
                        fallback),
                })
                .OrderBy(rule => rule.Operation, StringComparer.Ordinal)
                .ToArray();
            Validate(owner, rules);
            return rules;
        }

        internal static ToolSafetyContract Resolve(
            ToolSafetyContract fallback,
            IReadOnlyList<ToolSafetyRule> rules,
            JObject arguments)
        {
            var matches = (rules ?? Array.Empty<ToolSafetyRule>())
                .Where(rule => Matches(rule.When, arguments ?? new JObject()))
                .ToArray();
            if (matches.Length == 0)
                return fallback;
            var specificity = matches.Max(rule => rule.When.Count);
            var mostSpecific = matches
                .Where(rule => rule.When.Count == specificity)
                .ToArray();
            if (mostSpecific.Length != 1)
                throw new SchemaGenerationException(null, "Ambiguous safety rules matched.");
            return mostSpecific[0].Safety;
        }

        private static JObject BuildWhen(
            Type owner,
            HeraSafetyRuleAttribute attribute)
        {
            if (string.IsNullOrWhiteSpace(attribute.Operation)
                || string.IsNullOrWhiteSpace(attribute.Parameter))
            {
                throw new SchemaGenerationException(
                    owner,
                    "Safety rules require operation and parameter names.");
            }
            JToken value;
            try
            {
                value = JToken.Parse(attribute.Value);
            }
            catch
            {
                value = attribute.Value;
            }
            return new JObject
            {
                [attribute.Parameter.Trim().ToLowerInvariant()] = new JObject
                {
                    ["const"] = value,
                },
            };
        }

        private static bool Matches(JObject when, JObject arguments)
        {
            foreach (var property in when.Properties())
            {
                var expected = property.Value["const"];
                if (!JToken.DeepEquals(arguments[property.Name], expected))
                    return false;
            }
            return true;
        }

        private static void Validate(
            Type owner,
            IReadOnlyList<ToolSafetyRule> rules)
        {
            for (var i = 0; i < rules.Count; i++)
            {
                for (var j = i + 1; j < rules.Count; j++)
                {
                    if (rules[i].When.Count != rules[j].When.Count
                        || AreMutuallyExclusive(rules[i].When, rules[j].When))
                        continue;
                    throw new SchemaGenerationException(
                        owner,
                        $"Safety rules '{rules[i].Operation}' and " +
                        $"'{rules[j].Operation}' are ambiguous.");
                }
            }
        }

        private static bool AreMutuallyExclusive(JObject left, JObject right)
        {
            foreach (var property in left.Properties())
            {
                var other = right[property.Name];
                if (other != null
                    && !JToken.DeepEquals(property.Value["const"], other["const"]))
                    return true;
            }
            return false;
        }
    }
}
