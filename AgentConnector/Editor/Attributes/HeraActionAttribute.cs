using System;

namespace HeraAgent
{
    /// <summary>
    /// Marks a public static method on a <see cref="HeraToolAttribute"/> class as
    /// an action handler. The method must accept a single <c>JObject</c> parameter
    /// and return <c>object</c>, <c>Task&lt;object&gt;</c>, or <c>Task</c>.
    ///
    /// If <see cref="Name"/> is omitted the method name is converted to snake_case
    /// to form the action key (e.g. <c>GetRect</c> → <c>get_rect</c>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class HeraActionAttribute : Attribute
    {
        /// <summary>
        /// Optional action name override. If null, the method name is snake_cased.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optional human-readable description for schema/listing purposes.
        /// </summary>
        public string Description { get; set; }
        public Type ParametersType { get; set; }
        public Type ResultType { get; set; }
        public string[] Aliases { get; set; } = Array.Empty<string>();
        public HeraRiskClass RiskClass { get; set; } = HeraRiskClass.Unspecified;
        public bool RequiresConfirmation { get; set; }
        public bool Reversible { get; set; }
        public bool SupportsCancellation { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class HeraActionContractAttribute : Attribute
    {
        public HeraActionContractAttribute(string action, Type parametersType)
        {
            Action = action;
            ParametersType = parametersType;
        }

        public string Action { get; }
        public Type ParametersType { get; }
        public Type ResultType { get; set; }
        public string Description { get; set; }
        public string[] Aliases { get; set; } = Array.Empty<string>();
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
}
