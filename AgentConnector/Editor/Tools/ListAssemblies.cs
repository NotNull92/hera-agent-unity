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
            "list_assemblies --include_version true",
            "list_assemblies --include_location true",
        },
        ExampleDescriptions = new[]
        {
            "List project + Unity assembly names (system DLLs filtered out; bare name strings)",
            "Filter by substring (case-insensitive)",
            "Return {name, version} objects instead of bare names",
            "Include DLL file paths (implies version; default off — most AI agents don't need them)",
        })]
    public static class ListAssemblies
    {
        public class Parameters
        {
            [ToolParameter("Case-insensitive substring filter on assembly name.")]
            public string Filter { get; set; }

            [ToolParameter("Include System.*, mscorlib, netstandard etc. Default false because AI agents rarely need them.")]
            public bool IncludeSystem { get; set; }

            [ToolParameter("Return {name, version} objects instead of bare name strings. Default false — most assemblies report 0.0.0.0, so the version is dropped to roughly halve the payload.")]
            public bool IncludeVersion { get; set; }

            [ToolParameter("Include each assembly's DLL location (full path), implies version. Default false — most AI agents don't need them.")]
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
            var includeVersion = p.GetBool("include_version");
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

                if (includeLocation)
                {
                    var version = asm.GetName().Version?.ToString();
                    string location = null;
                    try { location = asm.Location; } catch { /* dynamic / in-memory */ }
                    result.Add(new { name, version, location });
                }
                else if (includeVersion)
                {
                    var version = asm.GetName().Version?.ToString();
                    result.Add(new { name, version });
                }
                else
                {
                    // Bare name — most assemblies report 0.0.0.0, so the version
                    // field is noise the agent rarely reads. Opt in with
                    // --include_version when it actually matters.
                    result.Add(name);
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
