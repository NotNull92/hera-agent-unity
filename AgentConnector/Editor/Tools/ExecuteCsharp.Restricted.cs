using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;

namespace HeraAgent.Tools
{
    public static partial class ExecuteCsharp
    {
        private static readonly string[] RestrictedSourceTokens =
        {
            "unsafe", "stackalloc", "fixed", "extern", "dynamic", "typeof",
            "GetType", "File", "Directory", "FileInfo", "DirectoryInfo",
            "DriveInfo", "FileStream", "StreamReader", "StreamWriter", "WebClient",
            "HttpClient", "Socket", "Process", "Environment", "AppDomain",
            "Activator", "Assembly", "MethodInfo", "FieldInfo", "PropertyInfo",
            "Marshal", "GCHandle", "EditorPrefs", "PlayerPrefs",
        };

        private static readonly Dictionary<short, OpCode> RestrictedOpCodes = BuildRestrictedOpCodes();

        private static ErrorResponse ValidateRestrictedSource(string code, IEnumerable<string> extraUsings)
        {
            var source = MaskRestrictedLiteralsAndComments(
                code + "\n" + string.Join("\n", extraUsings ?? Array.Empty<string>()));
            if (source.IndexOf('#') >= 0)
                return RestrictedError("EXEC_RESTRICTED_SOURCE_DENIED", "source", "preprocessor directive");

            foreach (var token in RestrictedSourceTokens)
            {
                if (Regex.IsMatch(source, @"\b" + Regex.Escape(token) + @"\b"))
                    return RestrictedError("EXEC_RESTRICTED_SOURCE_DENIED", "source", token);
            }

            return null;
        }

        private static ErrorResponse ValidateRestrictedMetadata(byte[] bytes)
        {
            try
            {
                using var stream = new MemoryStream(bytes, false);
                var assemblyType = Type.GetType("Mono.Cecil.AssemblyDefinition, Unity.Cecil");
                var readAssembly = assemblyType?.GetMethod(
                    "ReadAssembly",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Stream) },
                    null);
                if (readAssembly == null)
                    return RestrictedError("EXEC_RESTRICTED_METADATA_DENIED", "metadata", "Unity.Cecil unavailable");

                var assembly = readAssembly.Invoke(null, new object[] { stream });
                try
                {
                    var module = assemblyType.GetProperty("MainModule")?.GetValue(assembly);
                    if (module == null)
                        return RestrictedError("EXEC_RESTRICTED_METADATA_DENIED", "metadata", "missing module");
                    var references = module.GetType().GetProperty("AssemblyReferences")?.GetValue(module) as IEnumerable;
                    if (references == null)
                        return RestrictedError("EXEC_RESTRICTED_METADATA_DENIED", "metadata", "missing assembly references");
                    foreach (var reference in references)
                    {
                        var name = reference.GetType().GetProperty("Name")?.GetValue(reference) as string;
                        if (string.IsNullOrEmpty(name) || !IsRestrictedAssemblyAllowed(name))
                            return RestrictedError("EXEC_RESTRICTED_METADATA_DENIED", "metadata", name ?? "unnamed assembly");
                    }

                    var roots = module.GetType().GetProperty("Types")?.GetValue(module) as IEnumerable;
                    if (roots == null)
                        return RestrictedError("EXEC_RESTRICTED_METADATA_DENIED", "metadata", "missing type metadata");
                    foreach (var type in AllRestrictedTypes(roots))
                    {
                        var methods = type.GetType().GetProperty("Methods")?.GetValue(type) as IEnumerable;
                        if (methods == null) continue;
                        foreach (var method in methods)
                        {
                            if (RestrictedMetadataBool(method, "IsPInvokeImpl") ||
                                RestrictedMetadataBool(method, "HasPInvokeInfo"))
                            {
                                var fullName = method.GetType().GetProperty("FullName")?.GetValue(method) as string;
                                return RestrictedError("EXEC_RESTRICTED_METADATA_DENIED", "metadata", fullName ?? "P/Invoke");
                            }
                        }
                    }
                }
                finally
                {
                    (assembly as IDisposable)?.Dispose();
                }
            }
            catch (Exception ex)
            {
                return RestrictedError("EXEC_RESTRICTED_METADATA_DENIED", "metadata", ex.GetType().Name);
            }

            return null;
        }

        private static ErrorResponse ValidateRestrictedIl(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (Exception ex)
            {
                return RestrictedError("EXEC_RESTRICTED_IL_DENIED", "il", ex.GetType().Name);
            }

            foreach (var type in types)
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Static | BindingFlags.Instance |
                                                    BindingFlags.DeclaredOnly))
            {
                var error = ValidateRestrictedMethodIl(method, assembly);
                if (error != null) return error;
            }

            return null;
        }

        private static ErrorResponse ValidateRestrictedMethodIl(MethodInfo method, Assembly compiledAssembly)
        {
            var body = method.GetMethodBody();
            if (body == null) return null;
            var il = body.GetILAsByteArray();
            var position = 0;
            while (position < il.Length)
            {
                short value = il[position++];
                if (value == 0xfe)
                {
                    if (position >= il.Length)
                        return RestrictedError("EXEC_RESTRICTED_IL_DENIED", "il", "truncated opcode");
                    value = (short)(0xfe00 | il[position++]);
                }
                if (!RestrictedOpCodes.TryGetValue(value, out var opcode))
                    return RestrictedError("EXEC_RESTRICTED_IL_DENIED", "il", "unknown opcode");
                if (opcode == OpCodes.Calli || opcode == OpCodes.Jmp || opcode == OpCodes.Localloc ||
                    opcode == OpCodes.Cpblk || opcode == OpCodes.Initblk || opcode == OpCodes.Ldftn ||
                    opcode == OpCodes.Ldvirtftn)
                    return RestrictedError("EXEC_RESTRICTED_IL_DENIED", "il", opcode.Name);

                var operandSize = RestrictedOperandSize(opcode.OperandType, il, position);
                if (operandSize < 0 || position + operandSize > il.Length)
                    return RestrictedError("EXEC_RESTRICTED_IL_DENIED", "il", "invalid operand");

                if (opcode.OperandType == OperandType.InlineField ||
                    opcode.OperandType == OperandType.InlineMethod ||
                    opcode.OperandType == OperandType.InlineTok ||
                    opcode.OperandType == OperandType.InlineType)
                {
                    var token = BitConverter.ToInt32(il, position);
                    MemberInfo member;
                    try
                    {
                        member = method.Module.ResolveMember(
                            token,
                            method.DeclaringType?.GetGenericArguments(),
                            method.GetGenericArguments());
                    }
                    catch (Exception ex)
                    {
                        return RestrictedError("EXEC_RESTRICTED_IL_DENIED", "il", ex.GetType().Name);
                    }
                    var memberError = ValidateRestrictedMember(member, compiledAssembly);
                    if (memberError != null) return memberError;
                }

                position += operandSize;
            }

            return null;
        }

        private static ErrorResponse ValidateRestrictedMember(MemberInfo member, Assembly compiledAssembly)
        {
            if (member is Type referencedType)
            {
                if (!IsRestrictedRuntimeAssemblyAllowed(referencedType.Assembly) ||
                    IsRestrictedTypeBlocked(referencedType.FullName ?? referencedType.Name))
                    return RestrictedError("EXEC_RESTRICTED_IL_DENIED", "il", referencedType.FullName ?? referencedType.Name);
                return null;
            }

            var declaringType = member?.DeclaringType;
            if (declaringType == null || declaringType.Assembly == compiledAssembly) return null;
            var name = declaringType.FullName ?? declaringType.Name;
            if (!IsRestrictedRuntimeAssemblyAllowed(declaringType.Assembly) ||
                IsRestrictedTypeBlocked(name) ||
                (name == "System.Object" && member.Name == "GetType") ||
                (name == "UnityEngine.Application" &&
                 (member.Name == "Quit" || member.Name == "OpenURL" || member.Name == "ExternalEval")))
                return RestrictedError("EXEC_RESTRICTED_IL_DENIED", "il", name + "." + member.Name);
            return null;
        }

        private static bool IsRestrictedAssemblyAllowed(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == name)
                    return IsRestrictedRuntimeAssemblyAllowed(assembly);
            }
            return false;
        }

        private static bool IsRestrictedRuntimeAssemblyAllowed(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            if (name != "mscorlib" && name != "netstandard" && name != "System" &&
                !name.StartsWith("System.", StringComparison.Ordinal) &&
                !name.StartsWith("UnityEngine", StringComparison.Ordinal))
                return false;
            try
            {
                var root = Path.GetFullPath(UnityEditor.EditorApplication.applicationContentsPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var location = Path.GetFullPath(assembly.Location);
                return location.StartsWith(root, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRestrictedTypeBlocked(string name)
        {
            return name.StartsWith("System.IO.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Net.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Diagnostics.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Reflection.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Runtime.InteropServices.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Runtime.Loader.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Runtime.Serialization.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Threading.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Linq.Expressions.", StringComparison.Ordinal) ||
                   name.StartsWith("System.CodeDom.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Configuration.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Data.", StringComparison.Ordinal) ||
                   name.StartsWith("System.DirectoryServices.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Management.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Messaging.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Security.", StringComparison.Ordinal) ||
                   name.StartsWith("System.ServiceModel.", StringComparison.Ordinal) ||
                   name.StartsWith("System.Xml.", StringComparison.Ordinal) ||
                   name.StartsWith("Microsoft.Win32.", StringComparison.Ordinal) ||
                   name.StartsWith("Microsoft.CSharp.", StringComparison.Ordinal) ||
                   name.StartsWith("UnityEditor.", StringComparison.Ordinal) ||
                   name.StartsWith("UnityEngine.Networking.", StringComparison.Ordinal) ||
                   name.StartsWith("UnityEngine.Windows.", StringComparison.Ordinal) ||
                   name.StartsWith("UnityEngine.AndroidJava", StringComparison.Ordinal) ||
                   name.StartsWith("UnityEngine.iOS.", StringComparison.Ordinal) ||
                   name == "System.Environment" || name == "System.AppDomain" ||
                   name == "System.Activator" || name == "System.Console" ||
                   name == "System.Delegate" || name == "System.GC" || name == "System.Type" ||
                   name == "System.Runtime.CompilerServices.Unsafe" ||
                   name == "System.Runtime.CompilerServices.RuntimeHelpers" ||
                   name == "UnityEngine.PlayerPrefs" || name == "UnityEngine.WWW" ||
                   name == "UnityEngine.Microphone" || name == "UnityEngine.WebCamTexture" ||
                   name == "UnityEngine.LocationService";
        }

        private static IEnumerable<object> AllRestrictedTypes(IEnumerable roots)
        {
            foreach (var type in roots)
            {
                yield return type;
                var nestedTypes = type.GetType().GetProperty("NestedTypes")?.GetValue(type) as IEnumerable;
                if (nestedTypes == null) continue;
                foreach (var nested in AllRestrictedTypes(nestedTypes))
                    yield return nested;
            }
        }

        private static bool RestrictedMetadataBool(object instance, string property)
        {
            return instance.GetType().GetProperty(property)?.GetValue(instance) is bool value && value;
        }

        private static Dictionary<short, OpCode> BuildRestrictedOpCodes()
        {
            var result = new Dictionary<short, OpCode>();
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is OpCode opcode)
                    result[opcode.Value] = opcode;
            }
            return result;
        }

        private static int RestrictedOperandSize(OperandType type, byte[] il, int position)
        {
            switch (type)
            {
                case OperandType.InlineNone: return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: return 1;
                case OperandType.InlineVar: return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR: return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR: return 8;
                case OperandType.InlineSwitch:
                    if (position + 4 > il.Length) return -1;
                    var count = BitConverter.ToInt32(il, position);
                    return count < 0 || count > (il.Length - position - 4) / 4 ? -1 : 4 + count * 4;
                default: return -1;
            }
        }

        private static string MaskRestrictedLiteralsAndComments(string source)
        {
            var chars = source.ToCharArray();
            var state = 0;
            for (var i = 0; i < chars.Length; i++)
            {
                var current = chars[i];
                var next = i + 1 < chars.Length ? chars[i + 1] : '\0';
                if (state == 0)
                {
                    if (current == '/' && next == '/') { MaskRestrictedPair(chars, ref i); state = 1; }
                    else if (current == '/' && next == '*') { MaskRestrictedPair(chars, ref i); state = 2; }
                    else if (current == '@' && next == '"') { MaskRestrictedPair(chars, ref i); state = 4; }
                    else if (current == '"') { chars[i] = ' '; state = 3; }
                    else if (current == '\'') { chars[i] = ' '; state = 5; }
                    continue;
                }
                if (state == 1 && current == '\n') { state = 0; continue; }
                if (state == 2 && current == '*' && next == '/') { MaskRestrictedPair(chars, ref i); state = 0; continue; }
                if (state == 3 && current == '\\') { MaskRestrictedPair(chars, ref i); continue; }
                if (state == 3 && current == '"') { chars[i] = ' '; state = 0; continue; }
                if (state == 4 && current == '"' && next == '"') { MaskRestrictedPair(chars, ref i); continue; }
                if (state == 4 && current == '"') { chars[i] = ' '; state = 0; continue; }
                if (state == 5 && current == '\\') { MaskRestrictedPair(chars, ref i); continue; }
                if (state == 5 && current == '\'') { chars[i] = ' '; state = 0; continue; }
                if (current != '\n' && current != '\r') chars[i] = ' ';
            }
            return new string(chars);
        }

        private static void MaskRestrictedPair(char[] chars, ref int index)
        {
            chars[index] = ' ';
            chars[++index] = ' ';
        }

        private static ErrorResponse RestrictedError(string code, string stage, string violation)
        {
            return new ErrorResponse(
                code,
                $"Restricted exec rejected the snippet during {stage} validation: {violation}.",
                data: new { security_mode = "restricted", stage, violation },
                suggestions: new List<string> { "Use a dedicated Hera tool, or explicitly select full mode when unrestricted C# is required." });
        }
    }
}
