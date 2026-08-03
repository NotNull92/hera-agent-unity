using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HeraAgent
{
    public static class ProjectIdentity
    {
        public static string CurrentRoot => ResolveRoot(Application.dataPath);

        public static string CurrentId =>
            ToolContractCanonicalJson.ComputeProjectId(CurrentRoot);

        public static int CurrentProcessId
        {
            get
            {
                using (var process = Process.GetCurrentProcess())
                    return process.Id;
            }
        }

        public static bool OwnsState(JObject state, int currentProcessId)
        {
            if (state == null) return false;

            var projectId = state.Value<string>("project_id");
            if (!string.IsNullOrEmpty(projectId))
                return string.Equals(projectId, CurrentId, StringComparison.Ordinal);

            var ownerPid = state.Value<int?>("owner_pid") ?? 0;
            if (ownerPid != 0)
                return ownerPid == currentProcessId;

            return false;
        }

        internal static string ResolveRoot(string dataPath)
        {
            if (string.IsNullOrWhiteSpace(dataPath))
                throw new ArgumentException("Unity data path is required.", nameof(dataPath));

            var fullPath = Path.GetFullPath(dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(Path.GetFileName(fullPath), "Assets", StringComparison.OrdinalIgnoreCase)
                ? Directory.GetParent(fullPath)?.FullName ?? fullPath
                : fullPath;
        }
    }
}
