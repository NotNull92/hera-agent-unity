using System;
using System.Linq;

namespace HeraAgent
{
    internal static class ToolContractProfiles
    {
        internal static string[] Normalize(string[] profiles)
        {
            return (profiles ?? Array.Empty<string>())
                .Where(profile => !string.IsNullOrWhiteSpace(profile))
                .Select(profile => profile.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(profile => profile, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
