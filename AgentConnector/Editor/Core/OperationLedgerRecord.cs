using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal sealed class OperationLedgerRecord
    {
        [JsonProperty("schema_version")]
        public int SchemaVersion = 1;

        [JsonProperty("operation_id")]
        public string OperationId;

        [JsonProperty("tool")]
        public string Tool;

        [JsonProperty("action")]
        public string Action;

        [JsonProperty("arguments_hash")]
        public string ArgumentsHash;

        [JsonProperty("risk_class")]
        public string RiskClass;

        [JsonProperty("idempotent")]
        public bool Idempotent;

        [JsonProperty("approval_verified")]
        public bool ApprovalVerified;

        [JsonProperty("state")]
        public string State;

        [JsonProperty("started_at_ms")]
        public long StartedAtMs;

        [JsonProperty("committed_at_ms")]
        public long? CommittedAtMs;

        [JsonProperty("response")]
        public JToken Response;

        [JsonProperty("response_hash")]
        public string ResponseHash;

        [JsonProperty("domain_epoch")]
        public string DomainEpoch;
    }

    internal sealed class OperationLedgerDecision
    {
        internal bool Execute;
        internal object Response;

        internal static OperationLedgerDecision Proceed() =>
            new OperationLedgerDecision { Execute = true };

        internal static OperationLedgerDecision Replay(object response) =>
            new OperationLedgerDecision { Response = response };
    }
}
