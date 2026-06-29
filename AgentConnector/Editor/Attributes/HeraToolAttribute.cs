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
        public string EnumType { get; set; } = "";
        public string Default { get; set; } = "";
        public string OutputSchema { get; set; } = "";

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
}
