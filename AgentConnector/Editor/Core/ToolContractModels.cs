using System;
using System.Collections.Generic;
using System.Reflection;
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
}
