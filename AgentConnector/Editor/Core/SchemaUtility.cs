using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    public sealed class SchemaGenerationException : InvalidOperationException
    {
        public Type UnsupportedType { get; }

        public SchemaGenerationException(Type unsupportedType, string reason = null)
            : base(
                $"[Hera] I can't generate a JSON Schema for " +
                $"'{unsupportedType?.FullName ?? "<null>"}'." +
                (string.IsNullOrWhiteSpace(reason) ? "" : " " + reason))
        {
            UnsupportedType = unsupportedType;
        }
    }

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
            var schemaType = GenerateSchema(type)["type"];
            if (schemaType?.Type == JTokenType.String)
                return schemaType.Value<string>();
            if (schemaType is JArray types)
                return types.Values<string>().First(value => value != "null");
            throw new SchemaGenerationException(type, "The generated schema has no JSON type.");
        }

        public static JObject GenerateSchema(Type type)
        {
            if (type == null)
                throw new SchemaGenerationException(null, "A CLR type is required.");
            return CanonicalizeSchema(BuildSchema(type, new HashSet<Type>()));
        }

        public static JObject CanonicalizeSchema(JObject schema)
        {
            if (schema == null) return null;
            return (JObject)CanonicalizeToken(schema);
        }

        private static JObject BuildSchema(Type type, ISet<Type> visiting)
        {
            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null)
            {
                var nullableSchema = BuildSchema(nullableType, visiting);
                if (nullableSchema["type"]?.Type == JTokenType.String)
                {
                    nullableSchema["type"] = new JArray(
                        nullableSchema.Value<string>("type"),
                        "null");
                    if (nullableSchema["enum"] is JArray enumValues)
                        enumValues.Add(JValue.CreateNull());
                    return nullableSchema;
                }

                return new JObject
                {
                    ["anyOf"] = new JArray(
                        nullableSchema,
                        new JObject { ["type"] = "null" }),
                };
            }

            if (type == typeof(string) || type == typeof(char))
                return new JObject { ["type"] = "string" };
            if (IsIntegral(type))
                return new JObject { ["type"] = "integer" };
            if (IsFloatingPoint(type))
                return new JObject { ["type"] = "number" };
            if (type == typeof(bool))
                return new JObject { ["type"] = "boolean" };
            if (type.IsEnum)
            {
                return new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray(Enum.GetNames(type)),
                };
            }

            if (type.IsArray)
            {
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = BuildSchema(type.GetElementType(), visiting),
                };
            }

            var listType = FindGenericType(type, typeof(IList<>));
            if (listType != null)
            {
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = BuildSchema(listType.GetGenericArguments()[0], visiting),
                };
            }

            var dictionaryType = FindGenericType(type, typeof(IDictionary<,>));
            if (dictionaryType != null)
            {
                var arguments = dictionaryType.GetGenericArguments();
                if (arguments[0] != typeof(string))
                {
                    throw new SchemaGenerationException(
                        type,
                        "Only dictionaries with string keys are supported.");
                }

                return new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject(),
                    ["additionalProperties"] = BuildSchema(arguments[1], visiting),
                };
            }

            if (typeof(JToken).IsAssignableFrom(type))
            {
                throw new SchemaGenerationException(
                    type,
                    "JToken and JObject parameters require an explicit schema fragment.");
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                throw new SchemaGenerationException(
                    type,
                    "UnityEngine.Object graphs are not reflected recursively.");
            }

            if (type == typeof(object))
            {
                return new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject(),
                    ["additionalProperties"] = true,
                };
            }

            if (!IsDtoType(type))
                throw new SchemaGenerationException(type);
            if (!visiting.Add(type))
                throw new SchemaGenerationException(type, "Recursive DTO graphs are unsupported.");

            try
            {
                var properties = new JObject();
                foreach (var property in type
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.CanRead
                        && property.GetIndexParameters().Length == 0)
                    .Select(property => new
                    {
                        Property = property,
                        Name = GetSerializedPropertyName(property),
                    })
                    .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    properties[property.Name] = BuildSchema(
                        property.Property.PropertyType,
                        visiting);
                }

                if (!properties.Properties().Any())
                {
                    throw new SchemaGenerationException(
                        type,
                        "DTO object types must expose at least one readable public property.");
                }

                return new JObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                };
            }
            finally
            {
                visiting.Remove(type);
            }
        }

        private static bool IsIntegral(Type type)
        {
            return type == typeof(sbyte)
                || type == typeof(byte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong);
        }

        private static string GetSerializedPropertyName(PropertyInfo property)
        {
            var explicitName = property.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName;
            return string.IsNullOrWhiteSpace(explicitName)
                ? StringCaseUtility.ToSnakeCase(property.Name)
                : explicitName;
        }

        private static bool IsFloatingPoint(Type type)
        {
            return type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal);
        }

        private static bool IsDtoType(Type type)
        {
            return !type.IsAbstract
                && !type.IsInterface
                && !type.IsPointer
                && !typeof(Delegate).IsAssignableFrom(type)
                && type != typeof(DateTime)
                && type != typeof(DateTimeOffset)
                && type != typeof(Guid)
                && (type.IsClass || type.IsValueType);
        }

        private static Type FindGenericType(Type type, Type genericDefinition)
        {
            if (type.IsGenericType
                && type.GetGenericTypeDefinition() == genericDefinition)
            {
                return type;
            }

            return type.GetInterfaces().FirstOrDefault(candidate =>
                candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == genericDefinition);
        }

        private static JToken CanonicalizeToken(JToken token)
        {
            if (token is JObject obj)
            {
                var canonical = new JObject();
                foreach (var property in obj.Properties()
                    .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    canonical[property.Name] = CanonicalizeToken(property.Value);
                }
                return canonical;
            }

            if (token is JArray array)
                return new JArray(array.Select(CanonicalizeToken));

            return token.DeepClone();
        }
    }
}
