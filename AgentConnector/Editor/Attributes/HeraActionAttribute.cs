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
    }
}
