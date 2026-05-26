using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "list_assemblies",
        Description = "List assemblies loaded in the current Unity Editor AppDomain. AI agents call this to find which Unity packages, project assemblies, and third-party libraries are available before writing exec code.",
        Examples = new[]
        {
            "list_assemblies",
            "list_assemblies --filter Unity.Entities",
            "list_assemblies --include_system true",
            "list_assemblies --include_location true",
        },
        ExampleDescriptions = new[]
        {
            "List project + Unity assemblies (system DLLs filtered out, location omitted)",
            "Filter by substring (case-insensitive)",
            "Include System.* and mscorlib in the result",
            "Include DLL file paths (default off — most AI agents don't need them)",
        })]
    public static class ListAssemblies
    {
        public class Parameters
        {
            [ToolParameter("Case-insensitive substring filter on assembly name.")]
            public string Filter { get; set; }

            [ToolParameter("Include System.*, mscorlib, netstandard etc. Default false because AI agents rarely need them.")]
            public bool IncludeSystem { get; set; }

            [ToolParameter("Include each assembly's DLL location (full path). Default false — saves ~50% of response size.")]
            public bool IncludeLocation { get; set; }
        }

        private static readonly string[] SystemPrefixes =
        {
            "System.", "mscorlib", "netstandard", "Microsoft.", "Mono.",
            "WindowsBase", "PresentationCore", "PresentationFramework",
        };

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var filter = p.Get("filter");
            var includeSystem = p.GetBool("include_system");
            var includeLocation = p.GetBool("include_location");

            var sortedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.GetName().Name))
                .OrderBy(a => a.GetName().Name, StringComparer.OrdinalIgnoreCase);

            var result = new List<object>();
            foreach (var asm in sortedAssemblies)
            {
                var name = asm.GetName().Name;

                if (!includeSystem && SystemPrefixes.Any(prefix =>
                    name.StartsWith(prefix, StringComparison.Ordinal) || string.Equals(name, prefix.TrimEnd('.'), StringComparison.Ordinal)))
                    continue;

                if (!string.IsNullOrEmpty(filter) &&
                    name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var version = asm.GetName().Version?.ToString();
                if (includeLocation)
                {
                    string location = null;
                    try { location = asm.Location; } catch { /* dynamic / in-memory */ }
                    result.Add(new { name, version, location });
                }
                else
                {
                    result.Add(new { name, version });
                }
            }

            return new SuccessResponse($"{result.Count} assemblies", new
            {
                count = result.Count,
                assemblies = result,
            });
        }
    }
}
