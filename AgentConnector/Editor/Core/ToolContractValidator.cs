using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal static class ToolContractValidator
    {
        internal static ToolValidationResult Validate(
            ToolContract contract,
            JObject input,
            string action = null)
        {
            var normalized = input == null ? new JObject() : (JObject)input.DeepClone();
            if (contract == null)
                return Invalid(normalized, "UNKNOWN_COMMAND", "/", "known tool", null);

            ToolActionContract actionContract = null;
            if (!string.IsNullOrWhiteSpace(action))
                contract.Actions.TryGetValue(action.ToLowerInvariant(), out actionContract);

            var strict = actionContract?.IsStrict == true
                || (actionContract == null && contract.Mode == ToolContractMode.Strict);
            var parameters = actionContract?.Parameters ?? contract.Parameters;
            if (!strict)
                return Valid(normalized);

            if (actionContract != null)
            {
                var suppliedAction = normalized.Value<string>("action");
                if (string.IsNullOrWhiteSpace(suppliedAction))
                {
                    return Invalid(
                        normalized,
                        "MISSING_ARGUMENT",
                        "/action",
                        actionContract.Name,
                        null);
                }
                if (!string.Equals(
                    suppliedAction,
                    actionContract.Name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Invalid(
                        normalized,
                        "UNKNOWN_ACTION",
                        "/action",
                        actionContract.Name,
                        suppliedAction);
                }
                normalized["action"] = actionContract.Name;
            }

            var diagnostics = new List<ToolContractDiagnostic>();
            var positionalError = NormalizePositionals(normalized, parameters, actionContract != null);
            if (positionalError != null)
                return positionalError;

            foreach (var parameter in parameters)
            {
                foreach (var alias in parameter.Aliases)
                {
                    if (!normalized.TryGetValue(alias, out var aliasValue))
                        continue;
                    if (normalized.ContainsKey(parameter.Name))
                    {
                        return Invalid(
                            normalized,
                            "ARGUMENT_CONFLICT",
                            "/" + alias,
                            parameter.Name + " or " + alias,
                            new[] { parameter.Name, alias });
                    }

                    normalized[parameter.Name] = aliasValue;
                    normalized.Remove(alias);
                    if (parameter.Deprecated)
                    {
                        diagnostics.Add(new ToolContractDiagnostic
                        {
                            Code = "DEPRECATED_ARGUMENT",
                            Path = "/" + alias,
                            Message = $"'{alias}' is deprecated; use '{parameter.Name}'.",
                        });
                    }
                }
            }

            var allowed = new HashSet<string>(
                parameters.Select(parameter => parameter.Name),
                StringComparer.Ordinal);
            if (actionContract != null)
                allowed.Add("action");

            foreach (var property in normalized.Properties().ToList())
            {
                if (!allowed.Contains(property.Name))
                {
                    return Invalid(
                        normalized,
                        "UNKNOWN_ARGUMENT",
                        "/" + property.Name,
                        string.Join(", ", allowed.OrderBy(name => name, StringComparer.Ordinal)),
                        property.Value.Type.ToString());
                }
            }

            foreach (var parameter in parameters)
            {
                if (!normalized.TryGetValue(parameter.Name, out var value))
                {
                    if (parameter.Required)
                    {
                        return Invalid(
                            normalized,
                            "MISSING_ARGUMENT",
                            "/" + parameter.Name,
                            ExpectedType(parameter),
                            null);
                    }
                    continue;
                }
                if (value.Type == JTokenType.Null)
                {
                    if (parameter.AllowNull)
                        continue;
                    return Invalid(
                        normalized,
                        "ARGUMENT_TYPE_MISMATCH",
                        "/" + parameter.Name,
                        ExpectedType(parameter),
                        "null");
                }

                if (!TryNormalizeScalar(value, parameter.ValueType, out var converted))
                {
                    return Invalid(
                        normalized,
                        "ARGUMENT_TYPE_MISMATCH",
                        "/" + parameter.Name,
                        ExpectedType(parameter),
                        value.Type.ToString());
                }
                normalized[parameter.Name] = converted;

                var enumValues = parameter.Schema["enum"] as JArray;
                if (enumValues != null
                    && converted.Type == JTokenType.String)
                {
                    var canonical = enumValues.FirstOrDefault(item =>
                        item.Type == JTokenType.String
                        && string.Equals(
                            item.Value<string>(),
                            converted.Value<string>(),
                            StringComparison.OrdinalIgnoreCase));
                    if (canonical != null)
                    {
                        converted = canonical.DeepClone();
                        normalized[parameter.Name] = converted;
                    }
                }
                if (enumValues != null
                    && !enumValues.Any(item => JToken.DeepEquals(item, converted)))
                {
                    return Invalid(
                        normalized,
                        "INVALID_ARGUMENT",
                        "/" + parameter.Name,
                        enumValues.ToString(Newtonsoft.Json.Formatting.None),
                        converted.ToString());
                }

                var pattern = parameter.Schema.Value<string>("pattern");
                if (!string.IsNullOrEmpty(pattern)
                    && (converted.Type != JTokenType.String
                        || !Regex.IsMatch(converted.Value<string>(), pattern)))
                {
                    return Invalid(
                        normalized,
                        "INVALID_ARGUMENT",
                        "/" + parameter.Name,
                        pattern,
                        converted.ToString());
                }

                if (!string.IsNullOrWhiteSpace(parameter.Format)
                    && !MatchesFormat(converted, parameter.Format))
                {
                    return Invalid(
                        normalized,
                        "ARGUMENT_FORMAT_INVALID",
                        "/" + parameter.Name,
                        parameter.Format,
                        converted.ToString());
                }

                var schemaError = ValidateSchemaValue(
                    parameter.Schema,
                    converted,
                    "/" + parameter.Name);
                if (schemaError != null)
                {
                    return new ToolValidationResult
                    {
                        Normalized = normalized,
                        Error = schemaError,
                        Diagnostics = diagnostics,
                    };
                }
            }

            var groupError = ValidateArgumentGroups(
                normalized,
                actionContract?.ArgumentGroups ?? contract.ArgumentGroups);
            if (groupError != null)
                return groupError;

            return new ToolValidationResult
            {
                Normalized = normalized,
                Diagnostics = diagnostics,
            };
        }

        private static ToolValidationResult ValidateArgumentGroups(
            JObject normalized,
            IReadOnlyList<ToolArgumentGroupContract> groups)
        {
            foreach (var group in groups ?? Array.Empty<ToolArgumentGroupContract>())
            {
                var active = group.Terms.Count(term => IsActive(normalized, term));
                if ((group.Mode == ToolArgumentGroupMode.ExactlyOne
                        || group.Mode == ToolArgumentGroupMode.AtLeastOne)
                    && active == 0)
                {
                    return Invalid(
                        normalized,
                        group.MissingErrorCode,
                        group.Path,
                        group.Expected,
                        null);
                }
                if ((group.Mode == ToolArgumentGroupMode.ExactlyOne
                        || group.Mode == ToolArgumentGroupMode.AtMostOne)
                    && active > 1)
                {
                    return Invalid(
                        normalized,
                        group.ConflictErrorCode,
                        group.Path,
                        group.Expected,
                        group.Terms.Where(term => IsActive(normalized, term))
                            .Select(term => term.Name)
                            .ToArray());
                }
                if (group.Mode == ToolArgumentGroupMode.RequiredWhen
                    && IsActive(normalized, group.Terms[0])
                    && !group.Terms.Skip(1).Any(term => IsActive(normalized, term)))
                {
                    return Invalid(
                        normalized,
                        group.MissingErrorCode,
                        group.Path,
                        group.Expected,
                        null);
                }
            }
            return null;
        }

        private static bool IsActive(JObject normalized, ToolArgumentTermContract term)
        {
            if (!normalized.TryGetValue(term.Name, out var value)
                || value.Type == JTokenType.Null)
                return false;
            if (term.HasExpectedValue)
                return JToken.DeepEquals(value, term.ExpectedValue);
            if (value.Type == JTokenType.String)
                return !string.IsNullOrWhiteSpace(value.Value<string>());
            return true;
        }

        private static ToolValidationResult NormalizePositionals(
            JObject normalized,
            IReadOnlyList<ToolParameterContract> parameters,
            bool actionContract)
        {
            if (!(normalized["args"] is JArray args))
                return null;

            var start = actionContract ? 1 : 0;
            for (var index = start; index < args.Count; index++)
            {
                var parameterIndex = index - start;
                if (parameterIndex >= parameters.Count)
                {
                    return Invalid(
                        normalized,
                        "UNKNOWN_ARGUMENT",
                        "/args/" + index,
                        $"at most {parameters.Count} positional arguments",
                        args[index].Type.ToString());
                }
                var name = parameters[parameterIndex].Name;
                if (normalized.ContainsKey(name))
                {
                    return Invalid(
                        normalized,
                        "ARGUMENT_CONFLICT",
                        "/args/" + index,
                        name + " once",
                        args[index].ToString());
                }
                normalized[name] = args[index];
            }
            normalized.Remove("args");
            return null;
        }

        private static bool TryNormalizeScalar(JToken value, Type declaredType, out JToken converted)
        {
            var type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
            converted = value;

            if (type == typeof(JToken))
                return true;
            if (type == typeof(JObject))
                return value.Type == JTokenType.Object;
            if (type == typeof(JArray))
                return value.Type == JTokenType.Array;
            if (type == typeof(string))
            {
                if (value.Type != JTokenType.String)
                    return false;
                return true;
            }
            if (type == typeof(bool))
            {
                var boolean = ParamCoercion.CoerceBoolNullable(value);
                if (boolean.HasValue)
                {
                    converted = boolean.Value;
                    return true;
                }
                return false;
            }
            if (IsInteger(type))
            {
                if (value.Type == JTokenType.Integer)
                    return true;
                if (value.Type == JTokenType.String
                    && long.TryParse(value.Value<string>(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var integer))
                {
                    converted = integer;
                    return true;
                }
                return false;
            }
            if (IsNumber(type))
            {
                if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                    return true;
                if (value.Type == JTokenType.String
                    && double.TryParse(value.Value<string>(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var number))
                {
                    converted = number;
                    return true;
                }
                return false;
            }
            if (type.IsEnum)
                return value.Type == JTokenType.String;
            if (type.IsArray)
                return value.Type == JTokenType.Array;
            return value.Type == JTokenType.Object;
        }

        private static object ExpectedType(ToolParameterContract parameter)
        {
            if (parameter.Schema?["type"] != null)
                return parameter.Schema["type"];
            if (parameter.Schema?["oneOf"] is JArray oneOf)
            {
                return oneOf
                    .OfType<JObject>()
                    .Select(branch => branch["type"])
                    .Where(type => type != null)
                    .Select(type => type.DeepClone())
                    .ToArray();
            }
            return SchemaUtility.GetJsonTypeName(parameter.ValueType);
        }

        private static ErrorResponse ValidateSchemaValue(
            JObject schema,
            JToken value,
            string path)
        {
            if (schema == null)
                return null;

            if (!MatchesSchemaType(schema["type"], value))
                return SchemaError("ARGUMENT_TYPE_MISMATCH", path, schema["type"], value.Type);

            if (schema["enum"] is JArray enumValues
                && !enumValues.Any(candidate => JToken.DeepEquals(candidate, value)))
            {
                return SchemaError("INVALID_ARGUMENT", path, enumValues, value);
            }

            var pattern = schema.Value<string>("pattern");
            if (!string.IsNullOrEmpty(pattern)
                && (value.Type != JTokenType.String
                    || !Regex.IsMatch(value.Value<string>(), pattern)))
            {
                return SchemaError("INVALID_ARGUMENT", path, pattern, value);
            }

            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
            {
                var number = value.Value<double>();
                if (schema["minimum"] != null && number < schema["minimum"].Value<double>())
                    return SchemaError("INVALID_ARGUMENT", path, schema["minimum"], value);
                if (schema["maximum"] != null && number > schema["maximum"].Value<double>())
                    return SchemaError("INVALID_ARGUMENT", path, schema["maximum"], value);
            }

            if (value is JArray array)
            {
                if (schema["minItems"] != null && array.Count < schema["minItems"].Value<int>())
                    return SchemaError("INVALID_ARGUMENT", path, schema["minItems"], array.Count);
                if (schema["maxItems"] != null && array.Count > schema["maxItems"].Value<int>())
                    return SchemaError("INVALID_ARGUMENT", path, schema["maxItems"], array.Count);
                if (schema["items"] is JObject itemSchema)
                {
                    for (var index = 0; index < array.Count; index++)
                    {
                        var itemError = ValidateSchemaValue(
                            itemSchema,
                            array[index],
                            path + "/" + index);
                        if (itemError != null)
                            return itemError;
                    }
                }
            }

            if (value is JObject obj)
            {
                if (schema["required"] is JArray required)
                {
                    foreach (var name in required.Values<string>())
                    {
                        if (!obj.ContainsKey(name))
                            return SchemaError(
                                "MISSING_ARGUMENT",
                                path + "/" + EscapePointer(name),
                                "required property",
                                null);
                    }
                }

                var properties = schema["properties"] as JObject;
                foreach (var property in obj.Properties())
                {
                    if (properties?[property.Name] is JObject propertySchema)
                    {
                        var propertyError = ValidateSchemaValue(
                            propertySchema,
                            property.Value,
                            path + "/" + EscapePointer(property.Name));
                        if (propertyError != null)
                            return propertyError;
                        continue;
                    }

                    if (schema["additionalProperties"]?.Type == JTokenType.Boolean
                        && !schema["additionalProperties"].Value<bool>())
                    {
                        return SchemaError(
                            "UNKNOWN_ARGUMENT",
                            path + "/" + EscapePointer(property.Name),
                            properties?.Properties().Select(item => item.Name).ToArray()
                                ?? Array.Empty<string>(),
                            property.Value.Type);
                    }
                    if (schema["additionalProperties"] is JObject additionalSchema)
                    {
                        var additionalError = ValidateSchemaValue(
                            additionalSchema,
                            property.Value,
                            path + "/" + EscapePointer(property.Name));
                        if (additionalError != null)
                            return additionalError;
                    }
                }
            }

            if (schema["allOf"] is JArray allOf)
            {
                foreach (var branch in allOf.OfType<JObject>())
                {
                    var branchError = ValidateSchemaValue(branch, value, path);
                    if (branchError != null)
                        return branchError;
                }
            }

            if (schema["anyOf"] is JArray anyOf
                && !anyOf.OfType<JObject>().Any(branch =>
                    ValidateSchemaValue(branch, value, path) == null))
            {
                return SchemaError("ARGUMENT_TYPE_MISMATCH", path, anyOf, value.Type);
            }

            if (schema["oneOf"] is JArray oneOf
                && oneOf.OfType<JObject>().Count(branch =>
                    ValidateSchemaValue(branch, value, path) == null) != 1)
            {
                return SchemaError("ARGUMENT_TYPE_MISMATCH", path, oneOf, value.Type);
            }

            if (schema["not"] is JObject notSchema
                && ValidateSchemaValue(notSchema, value, path) == null)
            {
                return SchemaError("INVALID_ARGUMENT", path, "value excluded by schema", value);
            }

            return null;
        }

        private static bool MatchesSchemaType(JToken declared, JToken value)
        {
            if (declared == null)
                return true;
            if (declared.Type == JTokenType.String)
                return MatchesSchemaTypeName(declared.Value<string>(), value);
            return declared is JArray types
                && types.Values<string>().Any(type => MatchesSchemaTypeName(type, value));
        }

        private static bool MatchesSchemaTypeName(string declared, JToken value)
        {
            switch (declared)
            {
                case "null": return value.Type == JTokenType.Null;
                case "boolean": return value.Type == JTokenType.Boolean;
                case "object": return value.Type == JTokenType.Object;
                case "array": return value.Type == JTokenType.Array;
                case "number":
                    return value.Type == JTokenType.Integer || value.Type == JTokenType.Float;
                case "integer": return value.Type == JTokenType.Integer;
                case "string": return value.Type == JTokenType.String;
                default: return false;
            }
        }

        private static ErrorResponse SchemaError(
            string code,
            string path,
            object expected,
            object actual)
        {
            return new ErrorResponse(
                code,
                $"Validation failed at '{path}'.",
                new { path, expected, actual });
        }

        private static string EscapePointer(string value)
        {
            return value.Replace("~", "~0").Replace("/", "~1");
        }

        private static bool MatchesFormat(JToken value, string format)
        {
            if (value.Type != JTokenType.String)
                return false;
            var text = value.Value<string>();
            switch (format)
            {
                case "uri":
                    return Uri.TryCreate(text, UriKind.Absolute, out _);
                case "date-time":
                    return DateTimeOffset.TryParse(
                        text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
                default:
                    return true;
            }
        }

        private static bool IsInteger(Type type)
        {
            return type == typeof(byte) || type == typeof(sbyte)
                || type == typeof(short) || type == typeof(ushort)
                || type == typeof(int) || type == typeof(uint)
                || type == typeof(long) || type == typeof(ulong);
        }

        private static bool IsNumber(Type type)
        {
            return type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        }

        private static ToolValidationResult Valid(JObject normalized)
        {
            return new ToolValidationResult { Normalized = normalized };
        }

        private static ToolValidationResult Invalid(
            JObject normalized,
            string code,
            string path,
            object expected,
            object actual)
        {
            return new ToolValidationResult
            {
                Normalized = normalized,
                Error = new ErrorResponse(
                    code,
                    $"Validation failed at '{path}'.",
                    new { path, expected, actual }),
            };
        }
    }
}
