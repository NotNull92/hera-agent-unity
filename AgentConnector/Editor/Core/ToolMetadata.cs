using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    /// <summary>
    /// Represents metadata for a tool parameter including schema information
    /// </summary>
    public class ToolParameterMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public string DefaultValue { get; set; }
        public string EnumType { get; set; }
        public string Default { get; set; }
        public string Type { get; set; }
        public string OutputSchema { get; set; }
        public JObject Schema { get; set; }

        public ToolParameterMetadata(ToolParameterAttribute attr, Type propertyType, string propertyName)
        {
            Name = string.IsNullOrEmpty(attr.Name)
                ? StringCaseUtility.ToSnakeCase(propertyName)
                : attr.Name;
            Description = attr.Description;
            Required = attr.Required;
            DefaultValue = attr.DefaultValue;
            EnumType = attr.EnumType;
            Default = attr.Default;
            Type = SchemaUtility.GetJsonTypeName(propertyType);
            OutputSchema = attr.OutputSchema;
            Schema = GenerateSchema(attr, propertyType);
        }

        private JObject GenerateSchema(ToolParameterAttribute attr, Type propertyType)
        {
            var schema = new JObject
            {
                ["type"] = GetTypeName(propertyType)
            };

            if (!string.IsNullOrEmpty(attr.Description))
            {
                schema["description"] = attr.Description;
            }

            if (attr.Required)
            {
                schema["required"] = true;
            }

            if (!string.IsNullOrEmpty(attr.Default))
            {
                schema["default"] = ConvertDefaultValue(attr.Default, propertyType);
            }
            else if (!string.IsNullOrEmpty(attr.DefaultValue))
            {
                schema["default"] = attr.DefaultValue;
            }

            if (!string.IsNullOrEmpty(attr.EnumType))
            {
                var enumValues = GetEnumValues(attr.EnumType);
                if (enumValues != null && enumValues.Count > 0)
                {
                    schema["enum"] = new JArray(enumValues);
                }
            }

            return schema;
        }

        private JToken ConvertDefaultValue(string defaultValue, Type type)
        {
            try
            {
                if (type == typeof(string)) return defaultValue;
                if (type == typeof(int)) return int.Parse(defaultValue);
                if (type == typeof(float)) return float.Parse(defaultValue);
                if (type == typeof(bool)) return bool.Parse(defaultValue);
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private List<string> GetEnumValues(string enumName)
        {
            try
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var enumType = assembly.GetType(enumName);
                        if (enumType != null && enumType.IsEnum)
                            return System.Enum.GetNames(enumType).ToList();
                    }
                    catch
                    {
                        // ReflectionTypeLoadException on assemblies with broken
                        // references — skip the assembly, keep scanning.
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[Hera] I couldn't load enum values for '{enumName}'. " +
                    $"The schema will be missing allowed values: {ex.Message}");
            }
            return null;
        }
    }

    /// <summary>
    /// Represents metadata for a CLI tool including parameter schemas.
    /// Only ParametersSchema and OutputSchema are consumed downstream
    /// (ToolDiscovery.GetToolSchema / GetToolSchemas).
    /// </summary>
    public class ToolMetadata
    {
        public string Name { get; set; }
        public JObject ParametersSchema { get; set; }
        public JObject OutputSchema { get; set; }

        public ToolMetadata(Type toolType)
        {
            var toolAttr = toolType.GetCustomAttributes(typeof(HeraToolAttribute), false)
                .FirstOrDefault() as HeraToolAttribute;
            Name = toolAttr?.Name ?? StringCaseUtility.ToSnakeCase(toolType.Name);

            var paramsType = toolType.GetNestedType("Parameters") ?? toolType;
            var parameters = paramsType.GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(ToolParameterAttribute), false).Length > 0)
                .Select(p => new ToolParameterMetadata(
                    p.GetCustomAttributes(typeof(ToolParameterAttribute), false).First() as ToolParameterAttribute,
                    p.PropertyType,
                    p.Name))
                .ToList();

            ParametersSchema = GenerateParametersSchema(parameters);
            OutputSchema = GenerateOutputSchema(parameters);
        }

        private JObject GenerateOutputSchema(List<ToolParameterMetadata> parameters)
        {
            var hasOutputSchema = parameters.Any(p => !string.IsNullOrEmpty(p.OutputSchema));
            
            if (!hasOutputSchema)
            {
                // Default output schema
                return new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["success"] = new JObject { ["type"] = "boolean", ["description"] = "Whether the operation succeeded" },
                        ["message"] = new JObject { ["type"] = "string", ["description"] = "Success or error message" },
                        ["data"] = new JObject { ["type"] = "object", ["description"] = "Tool-specific output data" }
                    }
                };
            }

            // Use custom output schema if provided
            var customOutputSchema = parameters.FirstOrDefault(p => !string.IsNullOrEmpty(p.OutputSchema));
            if (customOutputSchema != null)
            {
                try
                {
                    return JObject.Parse(customOutputSchema.OutputSchema);
                }
                catch
                {
                    return new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["success"] = new JObject { ["type"] = "boolean" },
                            ["message"] = new JObject { ["type"] = "string" }
                        }
                    };
                }
            }

            return new JObject { ["type"] = "object" };
        }

        private JObject GenerateParametersSchema(List<ToolParameterMetadata> parameters)
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject()
            };

            foreach (var param in parameters)
            {
                schema["properties"][param.Name] = param.Schema;
            }

            var requiredParams = parameters.Where(p => p.Required).Select(p => p.Name).ToList();
            if (requiredParams.Count > 0)
            {
                schema["required"] = new JArray(requiredParams);
            }

            return schema;
        }
    }

    /// <summary>
    /// Registry for tool metadata management. Tools are registered lazily
    /// on first GetTool() call from ToolDiscovery and cached.
    /// </summary>
    public static class ToolMetadataRegistry
    {
        private static readonly Dictionary<string, ToolMetadata> _tools = new Dictionary<string, ToolMetadata>();

        public static void Register(Type toolType)
        {
            var metadata = new ToolMetadata(toolType);
            _tools[metadata.Name] = metadata;
        }

        public static ToolMetadata GetTool(string toolName)
        {
            _tools.TryGetValue(toolName, out var metadata);
            return metadata;
        }
    }
}