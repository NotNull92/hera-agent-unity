using System;

namespace HeraAgent
{
    /// <summary>
    /// Shared helpers for JSON Schema generation used by ToolDiscovery and ToolMetadata.
    /// </summary>
    public static class SchemaUtility
    {
        /// <summary>
        /// Maps a C# Type to its JSON Schema type name.
        /// </summary>
        public static string GetJsonTypeName(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int) || type == typeof(int?)) return "integer";
            if (type == typeof(float) || type == typeof(float?)) return "number";
            if (type == typeof(bool) || type == typeof(bool?)) return "boolean";
            if (type.IsArray) return "array";
            return "string";
        }
    }
}
