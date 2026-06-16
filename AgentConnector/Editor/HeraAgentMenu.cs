using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
// `using UnityEditor;` also pulls in the legacy AssetStore PackageInfo type;
// alias the PackageManager one so bare `PackageInfo` is unambiguous.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
// `using System.Diagnostics;` brings in its own Debug type.
using Debug = UnityEngine.Debug;
using UnityEngine;

namespace HeraAgent.Editor
{
    /// <summary>
    /// Top-level HeraAgent editor menu items.
    /// Kept separate from the settings window so lightweight menu additions
    /// do not pull in the full UIToolkit editor window unless needed.
    /// </summary>
    public static class HeraAgentMenu
    {
        // Priority 0 makes this item appear first in the HeraAgent dropdown.
        // Unity sorts menu items by priority ascending; default priority is 1000.
        [MenuItem("HeraAgent/Hera Update", priority = 0)]
        public static void UpdateHeraAgent()
        {
            if (!EditorUtility.DisplayDialog(
                    "Hera Update",
                    "Update hera-agent-unity connector to the latest version via UPM?",
                    "Update",
                    "Cancel"))
            {
                Debug.Log("[Hera] Update cancelled by user.");
                return;
            }

            _ = UpdateHeraAgentAsync();
        }

        private static async Task UpdateHeraAgentAsync()
        {
            var currentPkg = await FindCurrentPackageAsync();
            var identifier = BuildAddIdentifier(currentPkg);
            if (string.IsNullOrEmpty(identifier))
            {
                identifier = DefaultPackageUrl;
                Debug.Log($"[Hera] Could not resolve current package source; falling back to {identifier}");
            }

            var request = Client.Add(identifier);
            while (!request.IsCompleted)
            {
                if (request.Status == StatusCode.InProgress)
                {
                    await Task.Delay(50);
                    continue;
                }

                // Any terminal status (Success/Failure) is captured below.
                break;
            }

            if (request.Status >= StatusCode.Failure)
            {
                var message = request.Error?.message ?? "Unknown error";
                Debug.LogError($"[Hera] Update failed: {message}");
                EditorUtility.DisplayDialog("Hera Update Failed", message, "OK");
                return;
            }

            var newVersion = request.Result?.version ?? "latest";
            if (currentPkg != null && string.Equals(currentPkg.version, newVersion, StringComparison.Ordinal))
            {
                var alreadyLatest = "hera-agent-unity is already up to date.";
                Debug.Log($"[Hera] {alreadyLatest}");
                EditorUtility.DisplayDialog("Hera Update", alreadyLatest, "OK");
                LogCliStatus();
                return;
            }

            var info = $"Updated hera-agent-unity to {newVersion}.";
            Debug.Log($"[Hera] {info}");
            EditorUtility.DisplayDialog("Hera Update Complete", info, "OK");
            LogCliStatus();
        }

        /// <summary>
        /// Builds the identifier that Client.Add expects for the currently
        /// installed hera-agent-unity package. Git/local packages need their
        /// source path/URL; registry packages can use the package name.
        /// Returns null if the package cannot be found at all.
        /// </summary>
        private static string BuildAddIdentifier(PackageInfo pkg)
        {
            if (pkg == null)
                return null;

            switch (pkg.source)
            {
                case PackageSource.Git:
                case PackageSource.Local:
                case PackageSource.LocalTarball:
                    // packageId is "name@source"; Client.Add needs the source part.
                    if (!string.IsNullOrEmpty(pkg.packageId) && pkg.packageId.Contains("@"))
                        return pkg.packageId.Substring(pkg.packageId.IndexOf("@") + 1);
                    break;

                case PackageSource.Registry:
                case PackageSource.BuiltIn:
                case PackageSource.Unknown:
                default:
                    // Registry packages can be refreshed by name alone.
                    return pkg.name;
            }

            return pkg.name;
        }

        private static async Task<PackageInfo> FindCurrentPackageAsync()
        {
            var request = Client.List(offlineMode: false, includeIndirectDependencies: false);
            while (!request.IsCompleted)
            {
                if (request.Status == StatusCode.InProgress)
                {
                    await Task.Delay(50);
                    continue;
                }

                break;
            }

            if (request.Status >= StatusCode.Failure)
            {
                Debug.LogWarning($"[Hera] Client.List failed: {request.Error?.message ?? "unknown"}");
                return null;
            }

            return request.Result?.FirstOrDefault(
                p => string.Equals(p.name, PackageName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Runs `hera-agent-unity status` and logs the result to the Unity console.
        /// Non-fatal: if the CLI is not on PATH we just log a warning.
        /// </summary>
        private static void LogCliStatus()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "hera-agent-unity",
                    Arguments = "status",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        Debug.LogWarning("[Hera] Could not start hera-agent-unity status process.");
                        return;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(10000);

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        var firstLine = output.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        Debug.Log($"[Hera] CLI status: {firstLine}");
                    }
                    else if (!string.IsNullOrWhiteSpace(error))
                    {
                        // `status` is a human-target command and may write to stderr even on success.
                        var firstLine = error.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        Debug.Log($"[Hera] CLI status: {firstLine}");
                    }
                    else
                        Debug.Log("[Hera] CLI status returned no output.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Hera] Failed to run hera-agent-unity status: {ex.Message}");
            }
        }

        private const string PackageName = "com.notnull92.hera-agent-unity";
        private const string DefaultPackageUrl = "https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector";
    }
}
