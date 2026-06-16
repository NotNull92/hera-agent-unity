using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
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
            var identifier = await ResolveAddIdentifierAsync();
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
            var info = $"Updated hera-agent-unity to {newVersion}.";
            Debug.Log($"[Hera] {info}");
            EditorUtility.DisplayDialog("Hera Update Complete", info, "OK");
        }

        /// <summary>
        /// Builds the identifier that Client.Add expects for the currently
        /// installed hera-agent-unity package. Git/local packages need their
        /// source path/URL; registry packages can use the package name.
        /// Returns null if the package cannot be found at all.
        /// </summary>
        private static async Task<string> ResolveAddIdentifierAsync()
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

            var pkg = request.Result?.FirstOrDefault(
                p => string.Equals(p.name, PackageName, StringComparison.OrdinalIgnoreCase));

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

        private const string PackageName = "com.notnull92.hera-agent-unity";
        private const string DefaultPackageUrl = "https://github.com/NotNull92/hera-agent-unity.git?path=AgentConnector";
    }
}
