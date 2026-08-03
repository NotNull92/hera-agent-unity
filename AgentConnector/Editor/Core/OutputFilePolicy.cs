using System;
using System.IO;
using UnityEngine;

namespace HeraAgent
{
    public static class OutputFilePolicy
    {
        public static bool TryResolvePng(
            string rawPath,
            string defaultPath,
            bool overwrite,
            out string fullPath,
            out string errorCode,
            out string error)
        {
            fullPath = null;
            errorCode = null;
            error = null;

            try
            {
                var requested = string.IsNullOrWhiteSpace(rawPath) ? defaultPath : rawPath.Trim();
                if (string.IsNullOrWhiteSpace(requested))
                    throw new ArgumentException("output path is required");

                fullPath = Path.IsPathRooted(requested)
                    ? Path.GetFullPath(requested)
                    : Path.GetFullPath(Path.Combine(ProjectIdentity.CurrentRoot, requested));

                if (!string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase))
                {
                    errorCode = "INVALID_OUTPUT_PATH";
                    error = "output path must end with .png";
                    return false;
                }

                if (!File.Exists(fullPath))
                    return true;

                if (!overwrite)
                {
                    errorCode = "OUTPUT_PATH_EXISTS";
                    error = $"output file already exists: {fullPath}. Choose another path or set overwrite=true.";
                    return false;
                }

                if (!IsUnderTrustedRoot(fullPath))
                {
                    errorCode = "EXTERNAL_OVERWRITE_BLOCKED";
                    error = "existing files may only be overwritten under the Unity project or system temp directory";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorCode = "INVALID_OUTPUT_PATH";
                error = ex.Message;
                return false;
            }
        }

        internal static void WriteAllBytes(string path, byte[] contents, bool overwrite)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var mayReplace = overwrite && IsUnderTrustedRoot(path);
            using (var stream = new FileStream(
                path,
                mayReplace ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                stream.Write(contents, 0, contents.Length);
                stream.Flush(true);
            }
        }

        internal static bool IsUnderTrustedRoot(string path)
        {
            return IsUnder(path, ProjectIdentity.CurrentRoot)
                || IsUnder(path, Path.GetTempPath());
        }

        internal static bool IsUnder(string path, string root)
        {
            var candidate = Path.GetFullPath(path);
            var boundary = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var comparison = Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return candidate.StartsWith(boundary + Path.DirectorySeparatorChar, comparison)
                || string.Equals(candidate, boundary, comparison);
        }
    }
}
