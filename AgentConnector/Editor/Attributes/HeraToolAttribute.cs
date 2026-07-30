using System;

namespace HeraAgent
{
    /// <summary>
    /// Marks a static class as a CLI tool handler.
    /// The class must have a static HandleCommand(Newtonsoft.Json.Linq.JObject) method.
    /// Class name is auto-converted to snake_case for the command name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HeraToolAttribute : Attribute
    {
        public string Description { get; set; } = "";
        public string Name { get; set; }
        public string Group { get; set; } = "";
        public bool EnableDebugLogging { get; set; } = false;
        public string[] Groups { get; set; } = Array.Empty<string>();
        public bool Enabled { get; set; } = true;
        public bool ReadOnly { get; set; } = false;
        public bool Destructive { get; set; } = false;
        public bool Idempotent { get; set; } = false;
        public bool MayReloadDomain { get; set; } = false;
        public bool RequiresPlayMode { get; set; } = false;
        public string Title { get; set; }
        public string[] Profiles { get; set; } = Array.Empty<string>();
        public HeraRiskClass RiskClass { get; set; } = HeraRiskClass.Unspecified;
        public bool RequiresConfirmation { get; set; }
        public bool Reversible { get; set; }
        public bool SupportsCancellation { get; set; }
        public ToolContractMode ContractMode { get; set; } = ToolContractMode.Legacy;

        /// <summary>
        /// CLI invocation strings demonstrating typical usage. Paired by index
        /// with <see cref="ExampleDescriptions"/>; if the lengths differ,
        /// missing descriptions become empty strings.
        /// </summary>
        public string[] Examples { get; set; } = Array.Empty<string>();

        /// <summary>
        /// One-line descriptions matching <see cref="Examples"/> by index.
        /// Empty array is allowed; the schema then exposes call-only entries.
        /// </summary>
        public string[] ExampleDescriptions { get; set; } = Array.Empty<string>();
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class HeraActionSafetyAttribute : Attribute
    {
        public HeraActionSafetyAttribute()
        {
        }

        public HeraActionSafetyAttribute(string action)
        {
            Action = action;
        }

        public string Action { get; set; }
        public bool ReadOnly { get; set; } = false;
        public bool Destructive { get; set; } = false;
        public bool Idempotent { get; set; } = false;
        public bool MayReloadDomain { get; set; } = false;
        public bool RequiresPlayMode { get; set; } = false;
        public HeraRiskClass RiskClass { get; set; } = HeraRiskClass.Unspecified;
        public bool RequiresConfirmation { get; set; }
        public bool Reversible { get; set; }
        public bool SupportsCancellation { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class HeraSafetyRuleAttribute : Attribute
    {
        public HeraSafetyRuleAttribute(
            string operation,
            string parameter,
            string value)
        {
            Operation = operation;
            Parameter = parameter;
            Value = value;
        }

        public string Operation { get; }
        public string Parameter { get; }
        public string Value { get; }
        public HeraRiskClass RiskClass { get; set; } = HeraRiskClass.Unspecified;
        public bool ReadOnly { get; set; }
        public bool Destructive { get; set; }
        public bool Idempotent { get; set; }
        public bool MayReloadDomain { get; set; }
        public bool RequiresPlayMode { get; set; }
        public bool RequiresConfirmation { get; set; }
        public bool Reversible { get; set; }
        public bool SupportsCancellation { get; set; }
    }

    /// <summary>
    /// Marks a property in a nested Parameters class as a tool parameter.
    /// Used for auto-generating help text and parameter schemas.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class ToolParameterAttribute : Attribute
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; } = false;
        public string DefaultValue { get; set; }
        public string EnumType { get; set; }
        public string Default { get; set; }
        public string OutputSchema { get; set; }
        public string[] Aliases { get; set; } = Array.Empty<string>();
        public bool Deprecated { get; set; }
        public string Format { get; set; }
        public string SchemaJson { get; set; }
        public bool AllowNull { get; set; }

        public ToolParameterAttribute()
        {
        }

        public ToolParameterAttribute(string description)
        {
            Description = description;
        }

        public ToolParameterAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    public enum ToolArgumentGroupMode
    {
        AtMostOne = 0,
        ExactlyOne = 1,
        AtLeastOne = 2,
        RequiredWhen = 3,
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class HeraArgumentGroupAttribute : Attribute
    {
        public HeraArgumentGroupAttribute(
            ToolArgumentGroupMode mode,
            params string[] terms)
        {
            Mode = mode;
            Terms = terms ?? Array.Empty<string>();
        }

        public ToolArgumentGroupMode Mode { get; }
        public string[] Terms { get; }
        public string Action { get; set; }
        public string MissingErrorCode { get; set; } = "MISSING_ARGUMENT";
        public string ConflictErrorCode { get; set; } = "ARGUMENT_CONFLICT";
        public string Path { get; set; } = "/";
        public string Expected { get; set; }
    }
}
