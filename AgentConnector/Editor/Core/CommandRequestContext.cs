using System;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    public sealed class CommandRequestContext
    {
        public string ProtocolVersion { get; }
        public string OperationId { get; }
        public string ArgumentsHash { get; }
        public string ApprovalToken { get; }
        public string ClientKind { get; }
        public string CatalogHash { get; }
        public CancellationToken CancellationToken { get; }

        CommandRequestContext(
            string protocolVersion,
            string operationId,
            string argumentsHash,
            string approvalToken,
            string clientKind,
            string catalogHash,
            CancellationToken cancellationToken)
        {
            ProtocolVersion = protocolVersion;
            OperationId = operationId;
            ArgumentsHash = argumentsHash;
            ApprovalToken = approvalToken;
            ClientKind = clientKind;
            CatalogHash = catalogHash;
            CancellationToken = cancellationToken;
        }

        public static bool TryCreate(
            JObject metadata,
            JObject arguments,
            out CommandRequestContext context,
            out ErrorResponse error)
        {
            metadata ??= new JObject();
            var operationId = metadata["operation_id"]?.ToString();
            if (string.IsNullOrWhiteSpace(operationId))
                operationId = "op_" + Guid.NewGuid().ToString("N");
            if (!IsSafeOperationId(operationId))
            {
                context = null;
                error = new ErrorResponse(
                    "INVALID_OPERATION_ID",
                    "operation_id must contain only letters, digits, '_' or '-' and be 8-128 characters.");
                return false;
            }

            var computedHash = ToolContractCanonicalJson.ComputeArgumentsHash(arguments ?? new JObject());
            var suppliedHash = metadata["arguments_hash"]?.ToString();
            if (!string.IsNullOrEmpty(suppliedHash)
                && !string.Equals(suppliedHash, computedHash, StringComparison.Ordinal))
            {
                context = null;
                error = new ErrorResponse(
                    "ARGUMENTS_HASH_MISMATCH",
                    "arguments_hash does not match the canonical request parameters.");
                return false;
            }

            context = new CommandRequestContext(
                metadata["protocol_version"]?.ToString(),
                operationId,
                computedHash,
                metadata["approval_token"]?.Type == JTokenType.Null
                    ? null
                    : metadata["approval_token"]?.ToString(),
                metadata["client_kind"]?.ToString() ?? "unknown",
                metadata["catalog_hash"]?.ToString(),
                CancellationToken.None);
            error = null;
            return true;
        }

        internal ErrorResponse ValidateProtocol()
        {
            if (string.IsNullOrWhiteSpace(ProtocolVersion)
                || string.Equals(
                    ProtocolVersion,
                    ProtocolContracts.ExecutionProtocolVersion,
                    StringComparison.Ordinal))
            {
                return null;
            }

            return new ErrorResponse(
                "EXECUTION_PROTOCOL_UNSUPPORTED",
                "The request uses an unsupported Hera execution protocol version.",
                new
                {
                    request_protocol_version = ProtocolVersion,
                    current_protocol_version = ProtocolContracts.ExecutionProtocolVersion,
                });
        }

        internal ErrorResponse ValidateCatalog()
        {
            if (string.IsNullOrWhiteSpace(CatalogHash))
                return null;

            var current = ToolCatalogRuntime.CatalogHash;
            if (string.Equals(CatalogHash, current, StringComparison.Ordinal))
                return null;

            return new ErrorResponse(
                "CATALOG_STALE",
                "The request was validated against an older Unity tool catalog. Refresh the catalog and retry with a new operation ID.",
                new
                {
                    request_catalog_hash = CatalogHash,
                    current_catalog_hash = current,
                    domain_epoch = ToolCatalogRuntime.DomainEpoch,
                });
        }

        internal static bool IsSafeOperationId(string value)
        {
            if (value.Length < 8 || value.Length > 128)
                return false;
            foreach (var character in value)
            {
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                    return false;
            }
            return true;
        }
    }
}
