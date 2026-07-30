using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal static class ToolContractSchemaBuilder
    {
        internal static IReadOnlyList<ToolParameterContract> BuildParameters(Type parametersType)
        {
            if (parametersType == null)
                return Array.Empty<ToolParameterContract>();

            return parametersType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => new
                {
                    Property = property,
                    Attribute = property.GetCustomAttribute<ToolParameterAttribute>(),
                })
                .Where(entry => entry.Attribute != null)
                .Select(entry => BuildParameter(entry.Property, entry.Attribute))
                .OrderBy(parameter => parameter.Property.MetadataToken)
                .ToList();
        }

        internal static JObject BuildInputSchema(
            IReadOnlyList<ToolParameterContract> parameters,
            string action = null,
            IReadOnlyList<ToolArgumentGroupContract> argumentGroups = null)
        {
            var properties = new JObject();
            var required = new JArray();

            if (!string.IsNullOrEmpty(action))
            {
                properties["action"] = new JObject
                {
                    ["type"] = "string",
                    ["const"] = action,
                };
                required.Add("action");
            }

            foreach (var parameter in parameters)
            {
                properties[parameter.Name] = parameter.Schema.DeepClone();
                if (parameter.Required)
                    required.Add(parameter.Name);
            }

            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = false,
            };
            if (required.Count > 0)
                schema["required"] = required;
            AddArgumentGroups(schema, argumentGroups);
            return SchemaUtility.CanonicalizeSchema(schema);
        }

        internal static JObject BuildOutputSchema(Type resultType)
        {
            var dataSchema = resultType != null && resultType != typeof(object)
                ? SchemaUtility.GenerateSchema(resultType)
                : new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject(),
                };
            return SchemaUtility.CanonicalizeSchema(new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["success"] = new JObject { ["type"] = "boolean" },
                    ["message"] = new JObject { ["type"] = "string" },
                    ["data"] = dataSchema,
                },
            });
        }

        private static void AddArgumentGroups(
            JObject schema,
            IReadOnlyList<ToolArgumentGroupContract> argumentGroups)
        {
            foreach (var group in argumentGroups ?? Array.Empty<ToolArgumentGroupContract>())
            {
                if (group.Mode == ToolArgumentGroupMode.ExactlyOne)
                {
                    var oneOf = schema["oneOf"] as JArray ?? new JArray();
                    foreach (var term in group.Terms)
                        oneOf.Add(BuildPresentTerm(term));
                    schema["oneOf"] = oneOf;
                    continue;
                }

                var allOf = schema["allOf"] as JArray ?? new JArray();
                if (group.Mode == ToolArgumentGroupMode.AtLeastOne)
                {
                    allOf.Add(new JObject
                    {
                        ["anyOf"] = new JArray(group.Terms.Select(BuildPresentTerm)),
                    });
                    schema["allOf"] = allOf;
                    continue;
                }
                if (group.Mode == ToolArgumentGroupMode.RequiredWhen)
                {
                    var alternatives = new JArray
                    {
                        new JObject
                        {
                            ["not"] = BuildPresentTerm(group.Terms[0]),
                        },
                    };
                    foreach (var term in group.Terms.Skip(1))
                        alternatives.Add(BuildPresentTerm(term));
                    allOf.Add(new JObject { ["anyOf"] = alternatives });
                    schema["allOf"] = allOf;
                    continue;
                }

                for (var left = 0; left < group.Terms.Count; left++)
                {
                    for (var right = left + 1; right < group.Terms.Count; right++)
                    {
                        allOf.Add(new JObject
                        {
                            ["not"] = new JObject
                            {
                                ["allOf"] = new JArray(
                                    BuildPresentTerm(group.Terms[left]),
                                    BuildPresentTerm(group.Terms[right])),
                            },
                        });
                    }
                }
                schema["allOf"] = allOf;
            }
        }

        private static JObject BuildPresentTerm(ToolArgumentTermContract term)
        {
            var propertySchema = new JObject();
            if (term.HasExpectedValue)
                propertySchema["const"] = term.ExpectedValue.DeepClone();
            else if (term.ValueType == typeof(string))
                propertySchema["pattern"] = "\\S";
            else
                propertySchema["not"] = new JObject { ["type"] = "null" };

            return new JObject
            {
                ["required"] = new JArray(term.Name),
                ["properties"] = new JObject
                {
                    [term.Name] = propertySchema,
                },
            };
        }

        private static ToolParameterContract BuildParameter(
            PropertyInfo property,
            ToolParameterAttribute attribute)
        {
            var name = string.IsNullOrWhiteSpace(attribute.Name)
                ? StringCaseUtility.ToSnakeCase(property.Name)
                : attribute.Name.Trim();
            JObject schema;
            if (!string.IsNullOrWhiteSpace(attribute.SchemaJson))
            {
                try
                {
                    schema = JObject.Parse(attribute.SchemaJson);
                    ValidateSchema(schema, property);
                }
                catch (Exception exception)
                {
                    throw new SchemaGenerationException(
                        property.PropertyType,
                        $"Invalid SchemaJson for '{property.DeclaringType?.FullName}.{property.Name}': "
                        + exception.Message);
                }
            }
            else
            {
                schema = SchemaUtility.GenerateSchema(property.PropertyType);
            }

            if (!string.IsNullOrWhiteSpace(attribute.Description))
                schema["description"] = attribute.Description;
            if (!string.IsNullOrWhiteSpace(attribute.Format))
                schema["format"] = attribute.Format;
            if (attribute.Deprecated)
                schema["deprecated"] = true;
            if (attribute.AllowNull)
                schema = AllowNull(schema);

            return new ToolParameterContract
            {
                Name = name,
                ValueType = property.PropertyType,
                Property = property,
                Description = attribute.Description,
                Required = attribute.Required,
                Aliases = NormalizeNames(attribute.Aliases),
                Deprecated = attribute.Deprecated,
                Format = attribute.Format,
                AllowNull = attribute.AllowNull,
                Schema = SchemaUtility.CanonicalizeSchema(schema),
            };
        }

        private static void ValidateSchema(JObject schema, PropertyInfo property)
        {
            try
            {
                ValidateSchemaObject(schema);
            }
            catch (Exception exception)
            {
                throw new SchemaGenerationException(
                    property.PropertyType,
                    $"Invalid SchemaJson for '{property.DeclaringType?.FullName}.{property.Name}': "
                    + exception.Message);
            }
        }

        private static void ValidateSchemaObject(JObject schema)
        {
            ValidateType(schema["type"]);
            ValidateString(schema, "description");
            ValidateString(schema, "format");
            ValidateString(schema, "pattern");
            ValidateBoolean(schema, "deprecated");
            ValidateNumber(schema, "minimum");
            ValidateNumber(schema, "maximum");
            ValidateNonNegativeInteger(schema, "minItems");
            ValidateNonNegativeInteger(schema, "maxItems");

            if (schema["pattern"] is JValue pattern)
                _ = new Regex(pattern.Value<string>());

            if (schema["enum"] != null)
            {
                if (!(schema["enum"] is JArray enumValues) || enumValues.Count == 0)
                    throw new InvalidOperationException("'enum' must be a non-empty array.");
                if (enumValues.Select(value => value.ToString()).Distinct(StringComparer.Ordinal).Count()
                    != enumValues.Count)
                    throw new InvalidOperationException("'enum' values must be unique.");
            }

            if (schema["required"] != null)
            {
                if (!(schema["required"] is JArray required)
                    || required.Count == 0
                    || required.Any(value => value.Type != JTokenType.String)
                    || required.Select(value => value.Value<string>())
                        .Distinct(StringComparer.Ordinal).Count() != required.Count)
                {
                    throw new InvalidOperationException(
                        "'required' must be a non-empty array of unique strings.");
                }
            }

            if (schema["properties"] != null)
            {
                if (!(schema["properties"] is JObject properties))
                    throw new InvalidOperationException("'properties' must be an object.");
                foreach (var property in properties.Properties())
                {
                    if (!(property.Value is JObject child))
                        throw new InvalidOperationException(
                            $"'properties.{property.Name}' must be a schema object.");
                    ValidateSchemaObject(child);
                }
            }

            ValidateChildSchema(schema, "items");
            ValidateChildSchema(schema, "not");
            ValidateSchemaArray(schema, "oneOf");
            ValidateSchemaArray(schema, "anyOf");
            ValidateSchemaArray(schema, "allOf");

            var additionalProperties = schema["additionalProperties"];
            if (additionalProperties != null
                && additionalProperties.Type != JTokenType.Boolean
                && !(additionalProperties is JObject))
            {
                throw new InvalidOperationException(
                    "'additionalProperties' must be a boolean or schema object.");
            }
            if (additionalProperties is JObject additionalSchema)
                ValidateSchemaObject(additionalSchema);

            if (schema["minimum"] != null
                && schema["maximum"] != null
                && schema["minimum"].Value<double>() > schema["maximum"].Value<double>())
            {
                throw new InvalidOperationException("'minimum' cannot exceed 'maximum'.");
            }
            if (schema["minItems"] != null
                && schema["maxItems"] != null
                && schema["minItems"].Value<int>() > schema["maxItems"].Value<int>())
            {
                throw new InvalidOperationException("'minItems' cannot exceed 'maxItems'.");
            }
        }

        private static void ValidateType(JToken type)
        {
            if (type == null)
                return;
            var allowed = new HashSet<string>(
                new[] { "null", "boolean", "object", "array", "number", "string", "integer" },
                StringComparer.Ordinal);
            if (type.Type == JTokenType.String)
            {
                if (!allowed.Contains(type.Value<string>()))
                    throw new InvalidOperationException($"Unsupported schema type '{type}'.");
                return;
            }
            if (!(type is JArray types)
                || types.Count == 0
                || types.Any(value => value.Type != JTokenType.String)
                || types.Select(value => value.Value<string>())
                    .Distinct(StringComparer.Ordinal).Count() != types.Count
                || types.Any(value => !allowed.Contains(value.Value<string>())))
            {
                throw new InvalidOperationException(
                    "'type' must be a supported string or non-empty array of unique strings.");
            }
        }

        private static void ValidateString(JObject schema, string name)
        {
            if (schema[name] != null && schema[name].Type != JTokenType.String)
                throw new InvalidOperationException($"'{name}' must be a string.");
        }

        private static void ValidateBoolean(JObject schema, string name)
        {
            if (schema[name] != null && schema[name].Type != JTokenType.Boolean)
                throw new InvalidOperationException($"'{name}' must be a boolean.");
        }

        private static void ValidateNumber(JObject schema, string name)
        {
            if (schema[name] != null
                && schema[name].Type != JTokenType.Integer
                && schema[name].Type != JTokenType.Float)
            {
                throw new InvalidOperationException($"'{name}' must be a number.");
            }
        }

        private static void ValidateNonNegativeInteger(JObject schema, string name)
        {
            if (schema[name] == null)
                return;
            if (schema[name].Type != JTokenType.Integer || schema[name].Value<long>() < 0)
                throw new InvalidOperationException($"'{name}' must be a non-negative integer.");
        }

        private static void ValidateChildSchema(JObject schema, string name)
        {
            if (schema[name] == null)
                return;
            if (!(schema[name] is JObject child))
                throw new InvalidOperationException($"'{name}' must be a schema object.");
            ValidateSchemaObject(child);
        }

        private static void ValidateSchemaArray(JObject schema, string name)
        {
            if (schema[name] == null)
                return;
            if (!(schema[name] is JArray schemas)
                || schemas.Count == 0
                || schemas.Any(value => !(value is JObject)))
            {
                throw new InvalidOperationException(
                    $"'{name}' must be a non-empty array of schema objects.");
            }
            foreach (var child in schemas.Cast<JObject>())
                ValidateSchemaObject(child);
        }

        private static JObject AllowNull(JObject schema)
        {
            var type = schema["type"];
            if (type is JValue value && value.Type == JTokenType.String)
            {
                schema["type"] = new JArray(value.Value<string>(), "null");
                return schema;
            }
            if (type is JArray types)
            {
                if (!types.Values<string>().Contains("null", StringComparer.Ordinal))
                    types.Add("null");
                return schema;
            }
            if (schema["oneOf"] is JArray oneOf)
            {
                oneOf.Add(new JObject { ["type"] = "null" });
                return schema;
            }
            if (schema["anyOf"] is JArray anyOf)
            {
                anyOf.Add(new JObject { ["type"] = "null" });
                return schema;
            }
            schema = new JObject
            {
                ["anyOf"] = new JArray(
                    schema,
                    new JObject { ["type"] = "null" }),
            };
            return schema;
        }

        internal static string[] NormalizeNames(IEnumerable<string> names)
        {
            return (names ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
