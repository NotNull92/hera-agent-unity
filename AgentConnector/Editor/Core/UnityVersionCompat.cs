using System;
using UnityEngine;

namespace HeraAgent
{
    internal static class UnityVersionCompat
    {
        public const string Docs2022_3 = "2022.3";
        public const string Docs2023_2 = "2023.2";
        public const string Docs6000_0 = "6000.0";
        public const string Docs6000_3 = "6000.3";
        public const string Docs6000_5 = "6000.5";

        public static string CurrentDocsVersion()
        {
            return DocsVersionFor(Application.unityVersion);
        }

        public static string DocsVersionFor(string unityVersion)
        {
            var parsed = Parse(unityVersion);
            if (!parsed.HasValue) return Docs6000_0;

            var v = parsed.Value;
            if (v.Major == 2022) return Docs2022_3;
            if (v.Major == 2023) return Docs2023_2;
            if (v.Major == 6000)
            {
                if (v.Minor >= 5) return Docs6000_5;
                if (v.Minor >= 3) return Docs6000_3;
                return Docs6000_0;
            }

            return Docs6000_0;
        }

        public static bool DocsVersionAtLeast(string docsVersion, string minimumDocsVersion)
        {
            return DocsVersionRank(docsVersion) >= DocsVersionRank(minimumDocsVersion);
        }

        private static int DocsVersionRank(string docsVersion)
        {
            if (docsVersion == Docs2022_3) return 202203;
            if (docsVersion == Docs2023_2) return 202302;
            if (docsVersion == Docs6000_0) return 600000;
            if (docsVersion == Docs6000_3) return 600003;
            if (docsVersion == Docs6000_5) return 600005;
            return 600000;
        }

        private static ParsedVersion? Parse(string unityVersion)
        {
            if (string.IsNullOrEmpty(unityVersion)) return null;

            var parts = unityVersion.Split('.');
            if (parts.Length < 2) return null;
            if (!TryParseLeadingInt(parts[0], out var major)) return null;
            if (!TryParseLeadingInt(parts[1], out var minor)) return null;
            return new ParsedVersion(major, minor);
        }

        private static bool TryParseLeadingInt(string value, out int result)
        {
            result = 0;
            if (string.IsNullOrEmpty(value)) return false;

            var end = 0;
            while (end < value.Length && char.IsDigit(value[end])) end++;
            if (end == 0) return false;
            return int.TryParse(value.Substring(0, end), out result);
        }

        private struct ParsedVersion
        {
            public readonly int Major;
            public readonly int Minor;

            public ParsedVersion(int major, int minor)
            {
                Major = major;
                Minor = minor;
            }
        }
    }
}
