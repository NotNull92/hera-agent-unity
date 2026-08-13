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

        /// <summary>
        /// Turn a parameter that may be a durable handle (<c>guid:&lt;32hex&gt;</c>,
        /// <c>guid:&lt;32hex&gt;:&lt;fileId&gt;</c>, or a GlobalObjectId) into a
        /// concrete asset path. A plain path passes through untouched.
        /// <paramref name="resolved"/> carries the specific object a sub-asset
        /// handle named, so an action that operates on a typed object can use it
        /// while an action that operates on the asset file uses the path.
        /// Resolution is addressing only — every containment rule still runs
        /// afterwards on the resolved path.
        /// </summary>
        public static bool TryResolveAssetHandle(
            string raw,
            out string assetPath,
            out UnityEngine.Object resolved,
            out string errorCode,
            out string error)
        {
            assetPath = raw;
            resolved = null;
            errorCode = null;
            error = null;

            if (!ObjectIdentity.IsDurableForm(raw))
                return true;

            if (!ObjectIdentity.TryResolve(raw, out var obj, out var resolveError))
            {
                errorCode = "ASSET_NOT_FOUND";
                error = resolveError;
                return false;
            }

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
            {
                errorCode = "NOT_AN_ASSET";
                error = $"'{raw}' resolves to a scene object, not a project asset.";
                return false;
            }

            assetPath = path;
            resolved = obj;
            return true;
        }

        /// <summary>
        /// Resolve a durable handle if present, then apply the ordinary
        /// existing-file containment rule. When a handle resolves outside
        /// <c>Assets/</c>, the failure names the resolved path — a bare
        /// "must be under Assets/" against an opaque guid is unreadable.
        /// </summary>
        public static bool TryNormalizeExistingAssetFile(
            string raw,
            out string assetPath,
            out UnityEngine.Object resolved,
            out string errorCode,
            out string error)
        {
            if (!TryResolveAssetHandle(raw, out var candidate, out resolved, out errorCode, out error))
            {
                assetPath = null;
                return false;
            }

            if (TryNormalizeAssetFile(candidate, out assetPath, out error))
                return true;

            errorCode = "INVALID_PATH";
            if (!ReferenceEquals(candidate, raw))
                error = $"'{raw}' resolves to '{candidate}', which is outside Assets/.";
            return false;
        }

        /// <summary>File-or-folder variant of <see cref="TryNormalizeExistingAssetFile"/>.</summary>
        public static bool TryNormalizeExistingAssetPath(
            string raw,
            out string assetPath,
            out UnityEngine.Object resolved,
            out string errorCode,
            out string error)
        {
            if (!TryResolveAssetHandle(raw, out var candidate, out resolved, out errorCode, out error))
            {
                assetPath = null;
                return false;
            }

            if (TryNormalizeAssetPath(candidate, out assetPath, out error))
                return true;

            errorCode = "INVALID_PATH";
            if (!ReferenceEquals(candidate, raw))
                error = $"'{raw}' resolves to '{candidate}', which is outside Assets/.";
            return false;
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

            // A durable handle names an asset that already exists, so it can
            // never name the file this call is about to create.
            if (ObjectIdentity.IsDurableForm(raw))
            {
                errorCode = "INVALID_PATH";
                error = $"'{raw}' is a handle for an existing asset; a new file needs an Assets/ path.";
                return false;
            }

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
