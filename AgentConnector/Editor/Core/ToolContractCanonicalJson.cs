using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HeraAgent
{
    internal static class ToolContractCanonicalJson
    {
        internal static JObject Canonicalize(JObject value)
        {
            return value == null ? null : SchemaUtility.CanonicalizeSchema(value);
        }

        internal static string ComputeCatalogHash(JObject catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            var material = new JObject
            {
                ["schema_version"] = catalog["schema_version"]?.DeepClone(),
                ["tools"] = catalog["tools"]?.DeepClone(),
            };
            return Hash(Canonicalize(material).ToString(Formatting.None));
        }

        internal static string ComputeProjectId(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("Project path is required.", nameof(projectPath));
            var normalized = Path.GetFullPath(projectPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
            if (Application.platform == RuntimePlatform.WindowsEditor)
                normalized = normalized.ToLowerInvariant();
            return Hash(normalized);
        }

        internal static string ComputeArgumentsHash(JObject arguments)
        {
            return Hash(Canonicalize(arguments ?? new JObject()).ToString(Formatting.None));
        }

        internal static string ComputeTokenHash(JToken value)
        {
            return Hash(value.ToString(Formatting.None));
        }

        static string Hash(string value)
        {
            using var sha256 = SHA256.Create();
            var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            var hex = BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();
            return "sha256:" + hex;
        }
    }

    internal static class ToolCatalogRuntime
    {
        internal static readonly string DomainEpoch = CreateDomainEpoch();
        internal static readonly string[] Features =
        {
            "approval_v1",
            "domain_epoch_v1",
            "operation_ledger_v1",
            "task_bridge_v1",
            "tool_catalog_v1",
        };

        internal static string ProjectId =>
            ToolContractCanonicalJson.ComputeProjectId(
                Directory.GetParent(Application.dataPath)?.FullName
                ?? Application.dataPath);

        internal static string CreateDomainEpoch()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
