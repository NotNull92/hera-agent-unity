using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HeraAgent
{
    public static class AssetPathGuard
    {
        public static bool TryNormalizeAssetFolder(string raw, out string assetPath, out string error)
        {
            return TryNormalize(raw, allowAssetsRoot: true, out assetPath, out error);
        }

        public static bool TryNormalizeAssetFile(string raw, out string assetPath, out string error)
        {
            return TryNormalize(raw, allowAssetsRoot: false, out assetPath, out error);
        }

        public static bool TryNormalizeAssetPath(string raw, out string assetPath, out string error)
        {
            if (TryNormalizeAssetFile(raw, out assetPath, out error))
                return true;
            return TryNormalizeAssetFolder(raw, out assetPath, out error);
        }

        public static bool TryPrepareNewAssetFile(
            string raw,
            string extension,
            bool appendExtension,
            out string assetPath,
            out string errorCode,
            out string error)
        {
            assetPath = null;
            errorCode = null;
            error = null;

            if (!TryNormalizeAssetFile(raw, out assetPath, out error))
            {
                errorCode = "INVALID_PATH";
                return false;
            }

            if (string.IsNullOrEmpty(extension) || extension[0] != '.')
                throw new ArgumentException("extension must start with '.'", nameof(extension));

            if (!assetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                if (!appendExtension)
                {
                    errorCode = "INVALID_PATH";
                    error = $"path must end with '{extension}' (got '{assetPath}').";
                    return false;
                }
                assetPath += extension;
            }

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null || AssetDatabase.IsValidFolder(assetPath))
            {
                errorCode = "ASSET_EXISTS";
                error = $"An asset already exists at '{assetPath}'.";
                return false;
            }

            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
            {
                errorCode = "PARENT_FOLDER_MISSING";
                error = $"Parent folder '{parent}' does not exist.";
                return false;
            }

            return true;
        }

        static bool TryNormalize(string raw, bool allowAssetsRoot, out string assetPath, out string error)
        {
            assetPath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "path is required.";
                return false;
            }

            var normalized = raw.Replace('\\', '/').Trim().TrimEnd('/');
            if (normalized == "Assets")
            {
                if (!allowAssetsRoot)
                {
                    error = "path must name a file under Assets/ (got 'Assets').";
                    return false;
                }
            }
            else if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = $"path must be under Assets/ (got '{normalized}').";
                return false;
            }

            var assetsFull = Path.GetFullPath(Application.dataPath);
            var projectFull = Path.GetFullPath(Path.Combine(assetsFull, ".."));
            var candidateFull = Path.GetFullPath(Path.Combine(projectFull, normalized));
            var comparison = Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!candidateFull.Equals(assetsFull, comparison)
                && !candidateFull.StartsWith(assetsFull + Path.DirectorySeparatorChar, comparison)
                && !candidateFull.StartsWith(assetsFull + Path.AltDirectorySeparatorChar, comparison))
            {
                error = $"path escapes Assets/ (got '{normalized}').";
                return false;
            }

            assetPath = candidateFull.Equals(assetsFull, comparison)
                ? "Assets"
                : "Assets/" + candidateFull.Substring(assetsFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
            return true;
        }
    }
}
