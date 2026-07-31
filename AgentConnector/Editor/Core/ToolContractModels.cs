using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    public enum ToolContractMode
    {
        Legacy = 0,
        Strict = 1,
    }

    public enum HeraRiskClass
    {
        Unspecified = 0,
        ReadOnly = 1,
        Write = 2,
        Destructive = 3,
        ArbitraryCode = 4,
        PackageChange = 5,
        ExternalProcess = 6,
    }

    internal sealed class ToolParameterContract
    {
        public string Name { get; set; }
        public Type ValueType { get; set; }
        public PropertyInfo Property { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public string[] Aliases { get; set; } = Array.Empty<string>();
        public bool Deprecated { get; set; }
        public string Format { get; set; }
        public bool AllowNull { get; set; }
        public JObject Schema { get; set; }
    }

    internal sealed class ToolActionContract
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] Aliases { get; set; } = Array.Empty<string>();
        public Type ParametersType { get; set; }
        public Type ResultType { get; set; }
        public IReadOnlyList<ToolParameterContract> Parameters { get; set; }
        public JObject InputSchema { get; set; }
        public JObject OutputSchema { get; set; }
        public ToolSafetyContract Safety { get; set; }
        public bool IsStrict { get; set; }
        public IReadOnlyList<ToolArgumentGroupContract> ArgumentGroups { get; set; }
            = Array.Empty<ToolArgumentGroupContract>();
    }

    internal sealed class ToolContract
    {
        public string Name { get; set; }
        public Type ToolType { get; set; }
        public ToolContractMode Mode { get; set; }
        public IReadOnlyList<ToolParameterContract> Parameters { get; set; }
        public IReadOnlyDictionary<string, ToolActionContract> Actions { get; set; }
        public JObject InputSchema { get; set; }
        public JObject OutputSchema { get; set; }
        public ToolSafetyContract Safety { get; set; }
        public IReadOnlyList<ToolSafetyRule> SafetyRules { get; set; }
            = Array.Empty<ToolSafetyRule>();
        public IReadOnlyList<string> Profiles { get; set; }
            = Array.Empty<string>();
        public IReadOnlyList<ToolArgumentGroupContract> ArgumentGroups { get; set; }
            = Array.Empty<ToolArgumentGroupContract>();
    }

    internal sealed class ToolArgumentTermContract
    {
        public string Name { get; set; }
        public Type ValueType { get; set; }
        public bool HasExpectedValue { get; set; }
        public JToken ExpectedValue { get; set; }
    }

    internal sealed class ToolArgumentGroupContract
    {
        public ToolArgumentGroupMode Mode { get; set; }
        public IReadOnlyList<ToolArgumentTermContract> Terms { get; set; }
        public string MissingErrorCode { get; set; }
        public string ConflictErrorCode { get; set; }
        public string Path { get; set; }
        public string Expected { get; set; }
    }

    public sealed class ToolContractDiagnostic
    {
        public string Code { get; set; }
        public string Path { get; set; }
        public string Message { get; set; }
    }

    internal sealed class ToolValidationResult
    {
        public bool IsValid => Error == null;
        public JObject Normalized { get; set; }
        public ErrorResponse Error { get; set; }
        public IReadOnlyList<ToolContractDiagnostic> Diagnostics { get; set; }
            = Array.Empty<ToolContractDiagnostic>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class ToolCatalogEnvelope
    {
        [JsonProperty("schema_version")]
        public string SchemaVersion { get; set; }

        [JsonProperty("catalog_hash")]
        public string CatalogHash { get; set; }

        [JsonProperty("domain_epoch")]
        public string DomainEpoch { get; set; }

        [JsonProperty("project_id")]
        public string ProjectId { get; set; }

        [JsonProperty("tools")]
        public IReadOnlyList<ToolCatalogEntry> Tools { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class ToolCatalogEntry
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("source")] public ToolCatalogSource Source { get; set; }
        [JsonProperty("contract_mode")] public string ContractMode { get; set; }
        [JsonProperty("profiles")] public IReadOnlyList<string> Profiles { get; set; }
        [JsonProperty("aliases")] public IReadOnlyList<string> Aliases { get; set; }
        [JsonProperty("examples")] public IReadOnlyList<ToolCatalogExample> Examples { get; set; }
        [JsonProperty("input_schema")] public JObject InputSchema { get; set; }
        [JsonProperty("output_schema")] public JObject OutputSchema { get; set; }
        [JsonProperty("actions")] public IReadOnlyList<ToolCatalogAction> Actions { get; set; }
        [JsonProperty("safety")] public ToolCatalogSafety Safety { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class ToolCatalogSource
    {
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("assembly")] public string Assembly { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class ToolCatalogExample
    {
        [JsonProperty("call")] public string Call { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class ToolCatalogAction
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("aliases")] public IReadOnlyList<string> Aliases { get; set; }
        [JsonProperty("input_schema")] public JObject InputSchema { get; set; }
        [JsonProperty("output_schema")] public JObject OutputSchema { get; set; }
        [JsonProperty("safety")] public ToolCatalogSafety Safety { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal class ToolCatalogSafety
    {
        [JsonProperty("risk_class")] public string RiskClass { get; set; }
        [JsonProperty("read_only")] public bool ReadOnly { get; set; }
        [JsonProperty("destructive")] public bool Destructive { get; set; }
        [JsonProperty("idempotent")] public bool Idempotent { get; set; }
        [JsonProperty("may_reload_domain")] public bool MayReloadDomain { get; set; }
        [JsonProperty("requires_play_mode")] public bool RequiresPlayMode { get; set; }
        [JsonProperty("requires_confirmation")] public bool RequiresConfirmation { get; set; }
        [JsonProperty("reversible")] public bool Reversible { get; set; }
        [JsonProperty("supports_cancellation")] public bool SupportsCancellation { get; set; }
        [JsonProperty("side_effect_scope")] public string SideEffectScope { get; set; }
        [JsonProperty("rules")] public IReadOnlyList<ToolCatalogSafetyRule> Rules { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class ToolCatalogSafetyRule : ToolCatalogSafety
    {
        [JsonProperty("operation")] public string Operation { get; set; }
        [JsonProperty("when")] public JObject When { get; set; }
    }
}
