using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HeraAgent
{
    internal static class ToolActionContractBuilder
    {
        internal static IReadOnlyDictionary<string, ToolActionContract> Build(
            Type toolType,
            ToolContractMode mode)
        {
            var actions = new Dictionary<string, ToolActionContract>(StringComparer.Ordinal);

            foreach (var attribute in toolType.GetCustomAttributes<HeraActionContractAttribute>())
            {
                Add(
                    actions,
                    attribute.Action,
                    attribute.Description,
                    attribute.Aliases,
                    attribute.ParametersType,
                    attribute.ResultType,
                    true,
                    toolType,
                    ToolContractSafety.From(attribute, null));
            }

            foreach (var method in toolType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(method => method.MetadataToken))
            {
                var attribute = method.GetCustomAttribute<HeraActionAttribute>();
                if (attribute == null)
                    continue;
                var name = string.IsNullOrWhiteSpace(attribute.Name)
                    ? StringCaseUtility.ToSnakeCase(method.Name)
                    : attribute.Name.Trim().ToLowerInvariant();
                Add(
                    actions,
                    name,
                    attribute.Description,
                    attribute.Aliases,
                    attribute.ParametersType,
                    attribute.ResultType,
                    attribute.ParametersType != null,
                    toolType,
                    ToolContractSafety.From(attribute, null),
                    method);
            }

            if (mode == ToolContractMode.Legacy)
                AddImplicitLegacyActions(actions, toolType);
            return actions;
        }

        static void AddImplicitLegacyActions(
            IDictionary<string, ToolActionContract> actions,
            Type toolType)
        {
            foreach (var method in toolType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(method => method.MetadataToken))
            {
                if (method.GetCustomAttribute<HeraActionAttribute>() != null
                    || method.Name == "Handle"
                    || method.Name == "HandleCommand"
                    || !ToolDiscovery.IsSupportedActionHandler(method, out _))
                {
                    continue;
                }
                var name = StringCaseUtility.ToSnakeCase(method.Name).ToLowerInvariant();
                if (actions.ContainsKey(name))
                    continue;
                Add(
                    actions,
                    name,
                    "",
                    Array.Empty<string>(),
                    null,
                    null,
                    false,
                    toolType,
                    ToolContractSafety.From((HeraActionAttribute)null, null),
                    method);
            }
        }

        static void Add(
            IDictionary<string, ToolActionContract> actions,
            string name,
            string description,
            string[] aliases,
            Type parametersType,
            Type resultType,
            bool strict,
            Type toolType,
            ToolSafetyContract safety,
            MethodInfo method = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new SchemaGenerationException(
                    parametersType,
                    "Action contract has an empty action name.");
            name = name.Trim().ToLowerInvariant();
            var parameters = ToolContractSchemaBuilder.BuildParameters(parametersType);
            var argumentGroups = ToolContractRegistry.BuildArgumentGroups(
                toolType,
                parameters,
                name);
            var overrides = toolType
                .GetCustomAttributes<HeraActionSafetyAttribute>()
                .Concat(method == null
                    ? Array.Empty<HeraActionSafetyAttribute>()
                    : method.GetCustomAttributes<HeraActionSafetyAttribute>())
                .Where(attribute => string.IsNullOrWhiteSpace(attribute.Action)
                    || string.Equals(
                        attribute.Action.Trim(),
                        name,
                        StringComparison.OrdinalIgnoreCase));
            foreach (var safetyOverride in overrides)
                safety = ToolContractSafety.Apply(safety, safetyOverride, null);
            safety = ToolContractSafety.EnsureClassified(safety, toolType);
            actions[name] = new ToolActionContract
            {
                Name = name,
                Description = description ?? "",
                Aliases = ToolContractSchemaBuilder.NormalizeNames(aliases),
                ParametersType = parametersType,
                ResultType = resultType,
                Parameters = parameters,
                ArgumentGroups = argumentGroups,
                InputSchema = ToolContractSchemaBuilder.BuildInputSchema(
                    parameters,
                    name,
                    argumentGroups),
                OutputSchema = ToolContractSchemaBuilder.BuildOutputSchema(resultType),
                Safety = safety,
                IsStrict = strict,
            };
        }
    }
}
