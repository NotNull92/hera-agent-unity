using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal static class ToolContractRegistry
    {
        private static readonly Dictionary<string, ToolContract> Contracts
            = new Dictionary<string, ToolContract>(StringComparer.Ordinal);

        internal static ToolContract Get(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                return null;
            if (Contracts.TryGetValue(toolName, out var cached))
                return cached;

            var type = ToolDiscovery.FindToolType(toolName);
            if (type == null)
                return null;
            var contract = Build(type);
            Contracts[contract.Name] = contract;
            return contract;
        }

        internal static ToolContract Build(Type toolType)
        {
            if (toolType == null)
                throw new ArgumentNullException(nameof(toolType));

            var attribute = toolType.GetCustomAttribute<HeraToolAttribute>();
            var name = string.IsNullOrWhiteSpace(attribute?.Name)
                ? StringCaseUtility.ToSnakeCase(toolType.Name)
                : attribute.Name.Trim();
            var parameters = ToolContractSchemaBuilder.BuildParameters(
                toolType.GetNestedType("Parameters"));
            var resultType = toolType.GetNestedType("Result");
            var argumentGroups = BuildArgumentGroups(toolType, parameters, null);
            var safety = ToolContractSafety.From(attribute, toolType);
            var mode = attribute?.ContractMode ?? ToolContractMode.Legacy;
            var actions = ToolActionContractBuilder.Build(toolType, mode);

            return new ToolContract
            {
                Name = name,
                ToolType = toolType,
                Mode = mode,
                Parameters = parameters,
                Actions = actions,
                Safety = safety,
                SafetyRules = ToolContractSafety.BuildRules(toolType, safety),
                Profiles = ToolContractProfiles.Normalize(
                    attribute?.Profiles,
                    ToolContractSafety.IsBuiltIn(toolType),
                    mode,
                    safety.RiskClass),
                ArgumentGroups = argumentGroups,
                InputSchema = ToolContractSchemaBuilder.BuildInputSchema(
                    parameters,
                    argumentGroups: argumentGroups),
                OutputSchema = ToolContractSchemaBuilder.BuildOutputSchema(resultType),
            };
        }

        internal static void Clear()
        {
            Contracts.Clear();
        }

        internal static IReadOnlyList<ToolArgumentGroupContract> BuildArgumentGroups(
            Type toolType,
            IReadOnlyList<ToolParameterContract> parameters,
            string action)
        {
            var byName = parameters.ToDictionary(
                parameter => parameter.Name,
                StringComparer.Ordinal);
            var groups = new List<ToolArgumentGroupContract>();
            foreach (var attribute in toolType.GetCustomAttributes<HeraArgumentGroupAttribute>())
            {
                var targetAction = string.IsNullOrWhiteSpace(attribute.Action)
                    ? null
                    : attribute.Action.Trim().ToLowerInvariant();
                if (!string.Equals(targetAction, action, StringComparison.Ordinal))
                    continue;
                if (attribute.Terms == null || attribute.Terms.Length < 2)
                {
                    throw new SchemaGenerationException(
                        toolType,
                        "Argument groups require at least two terms.");
                }

                var terms = attribute.Terms
                    .Select(term => ParseTerm(toolType, byName, term))
                    .ToArray();
                if (terms.Select(term => term.Name).Distinct(StringComparer.Ordinal).Count()
                    != terms.Length)
                {
                    throw new SchemaGenerationException(
                        toolType,
                        "Argument group terms must reference distinct parameters.");
                }

                groups.Add(new ToolArgumentGroupContract
                {
                    Mode = attribute.Mode,
                    Terms = terms,
                    MissingErrorCode = attribute.MissingErrorCode,
                    ConflictErrorCode = attribute.ConflictErrorCode,
                    Path = string.IsNullOrWhiteSpace(attribute.Path) ? "/" : attribute.Path,
                    Expected = attribute.Expected ?? string.Join(" or ", attribute.Terms),
                });
            }
            return groups;
        }

        private static ToolArgumentTermContract ParseTerm(
            Type toolType,
            IReadOnlyDictionary<string, ToolParameterContract> parameters,
            string rawTerm)
        {
            if (string.IsNullOrWhiteSpace(rawTerm))
                throw new SchemaGenerationException(toolType, "Argument group term is empty.");

            var separator = rawTerm.IndexOf('=');
            var name = (separator < 0 ? rawTerm : rawTerm.Substring(0, separator))
                .Trim()
                .ToLowerInvariant();
            if (!parameters.TryGetValue(name, out var parameter))
            {
                throw new SchemaGenerationException(
                    toolType,
                    $"Argument group references unknown parameter '{name}'.");
            }

            var term = new ToolArgumentTermContract
            {
                Name = name,
                ValueType = parameter.ValueType,
            };
            if (separator < 0)
                return term;

            var rawValue = rawTerm.Substring(separator + 1).Trim();
            try
            {
                term.HasExpectedValue = true;
                term.ExpectedValue = JToken.Parse(rawValue);
                return term;
            }
            catch (Exception exception)
            {
                throw new SchemaGenerationException(
                    toolType,
                    $"Argument group value '{rawValue}' is not valid JSON: {exception.Message}");
            }
        }
    }
}
