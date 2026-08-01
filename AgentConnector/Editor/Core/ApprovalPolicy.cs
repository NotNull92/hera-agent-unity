using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal sealed class ApprovalSummary
    {
        [JsonProperty("tool")] public string Tool;
        [JsonProperty("action")] public string Action;
        [JsonProperty("target")] public string Target;
        [JsonProperty("side_effect")] public string SideEffect;
        [JsonProperty("reversible")] public bool Reversible;
        [JsonProperty("may_reload_domain")] public bool MayReloadDomain;
        [JsonProperty("external_impact")] public bool ExternalImpact;
        [JsonProperty("operation_id")] public string OperationId;
    }

    internal sealed class ApprovalPreflight
    {
        [JsonProperty("token")] public string Token;
        [JsonProperty("operation_id")] public string OperationId;
        [JsonProperty("expires_at_ms")] public long ExpiresAtMs;
        [JsonProperty("summary")] public ApprovalSummary Summary;
    }

    internal static class ApprovalPolicy
    {
        internal static readonly ApprovalAuthority Authority = ApprovalAuthority.CreateProcessLocal();

        internal static object Preflight(JObject request)
        {
            var operationId = request?.Value<string>("operation_id");
            var tool = request?.Value<string>("tool");
            var arguments = request?["arguments"] as JObject;
            if (!CommandRequestContext.IsSafeOperationId(operationId ?? "")
                || string.IsNullOrWhiteSpace(tool)
                || arguments == null)
            {
                return new ErrorResponse(
                    "INVALID_APPROVAL_PREFLIGHT",
                    "Approval preflight requires a valid operation_id, tool, and arguments object.");
            }

            var contract = ToolContractRegistry.Get(tool);
            if (contract == null)
                return new ErrorResponse("UNKNOWN_TOOL", $"Tool not found: {tool}");

            var requestedAction = request.Value<string>("action");
            var argumentAction = arguments.Value<string>("action");
            var action = NormalizeAction(contract, requestedAction ?? argumentAction);
            if (requestedAction != null
                && argumentAction != null
                && !string.Equals(
                    action,
                    NormalizeAction(contract, argumentAction),
                    StringComparison.Ordinal))
            {
                return new ErrorResponse(
                    "APPROVAL_MISMATCH",
                    "Approval action does not match the request arguments.");
            }

            var hasAction = !string.IsNullOrEmpty(action)
                && contract.Actions.ContainsKey(action);
            if (!string.IsNullOrEmpty(action) && !hasAction && contract.Actions.Count > 0)
                return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action: {action}");

            var normalized = (JObject)arguments.DeepClone();
            if (hasAction)
                normalized["action"] = action;
            var validation = ToolContractValidator.Validate(
                contract,
                normalized,
                hasAction ? action : null);
            if (!validation.IsValid)
                return validation.Error;

            var fallback = contract.Safety;
            if (hasAction)
                fallback = contract.Actions[action].Safety;
            var safety = ToolContractSafety.Resolve(
                fallback,
                contract.SafetyRules,
                validation.Normalized);
            if (!safety.RequiresConfirmation)
            {
                return new ErrorResponse(
                    "APPROVAL_NOT_REQUIRED",
                    "This operation does not require approval.");
            }

            var binding = new ApprovalBinding
            {
                OperationId = operationId,
                Tool = contract.Name,
                Action = hasAction ? action : null,
                ArgumentsHash = ToolContractCanonicalJson.ComputeArgumentsHash(arguments),
                RiskClass = ToolCatalogBuilder.RiskName(safety.RiskClass),
                ProjectId = ToolCatalogRuntime.ProjectId,
            };
            var grant = Authority.Issue(binding);
            return new SuccessResponse("Approval preflight", new ApprovalPreflight
            {
                Token = grant.Token,
                OperationId = binding.OperationId,
                ExpiresAtMs = grant.ExpiresAtMs,
                Summary = new ApprovalSummary
                {
                    Tool = binding.Tool,
                    Action = binding.Action,
                    Target = Target(arguments),
                    SideEffect = safety.SideEffectScope,
                    Reversible = safety.Reversible,
                    MayReloadDomain = safety.MayReloadDomain,
                    ExternalImpact = safety.RiskClass == HeraRiskClass.PackageChange
                        || safety.RiskClass == HeraRiskClass.ExternalProcess,
                    OperationId = binding.OperationId,
                },
            });
        }

        internal static ApprovalBinding Binding(
            CommandRequestContext context,
            string tool,
            string action,
            ToolSafetyContract safety) => new ApprovalBinding
            {
                OperationId = context.OperationId,
                Tool = tool,
                Action = action,
                ArgumentsHash = context.ArgumentsHash,
                RiskClass = ToolCatalogBuilder.RiskName(safety.RiskClass),
                ProjectId = ToolCatalogRuntime.ProjectId,
            };

        static string NormalizeAction(ToolContract contract, string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return null;
            action = action.Trim().ToLowerInvariant();
            if (contract.Actions.ContainsKey(action))
                return action;
            foreach (var entry in contract.Actions)
            {
                if (entry.Value.Aliases.Contains(action, StringComparer.Ordinal))
                    return entry.Key;
            }
            return action;
        }

        static string Target(JObject arguments)
        {
            foreach (var name in new[] { "path", "target", "id", "name", "asset_path", "package" })
            {
                if (arguments.TryGetValue(name, out var value))
                    return value.ToString(Formatting.None);
            }
            var keys = arguments.Properties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            return keys.Length == 0
                ? "current Unity project"
                : "parameters: " + string.Join(",", keys);
        }
    }
}
