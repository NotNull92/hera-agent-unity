using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "unity_docs",
        Description = "Look up an offline Unity ScriptReference page. Query is a Unity type / property / method ('Rigidbody', 'Rigidbody.mass', 'GameObject.AddComponent', 'Vector3.zero', 'UnityEngine.Time.deltaTime'). Returns { title, signature, summary, manual_url, scriptreference_url, unity_version } — typically 250-400 bytes — so an AI agent can verify an API exists at this Unity version before running it through exec. Docs root is provided by the CLI (env / asset-config / autodetect); on a miss the response carries did_you_mean suggestions from the ScriptReference filename index.",
        Examples = new[]
        {
            "unity_docs Rigidbody",
            "unity_docs Rigidbody.mass",
            "unity_docs Rigidbody.AddForce",
            "unity_docs Vector3.zero",
            "unity_docs UnityEditor.AssetDatabase.Refresh",
        },
        ExampleDescriptions = new[]
        {
            "Class page",
            "Property (mapped to Rigidbody-mass.html)",
            "Method (mapped to Rigidbody.AddForce.html)",
            "Static property on a value type",
            "Editor API; UnityEditor. prefix is stripped before lookup",
        })]
    public static class UnityDocs
    {
        public class Parameters
        {
            [ToolParameter("Unity type, property, or method (Rigidbody, Rigidbody.mass, GameObject.AddComponent, Vector3.zero, UnityEditor.AssetDatabase.Refresh).", Required = true)]
            public string Query { get; set; }

            [ToolParameter("Absolute path to the offline Unity Documentation 'en' folder (contains ScriptReference/, Manual/). Filled in automatically by the CLI from --docs-path / HERA_AGENT_UNITY_DOCS / asset-config / autodetect — supply manually only when calling the connector outside the standard CLI path.", Required = true)]
            public string DocsRoot { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            if (parameters == null) return new ErrorResponse("Parameters cannot be null.");
            var p = new ToolParams(parameters);
            var argsToken = p.GetRaw("args") as JArray;

            string query = p.Get("query")
                ?? (argsToken != null && argsToken.Count >= 1 ? argsToken[0].ToString() : null);
            if (string.IsNullOrEmpty(query))
                return new ErrorResponse("'query' required. Examples: 'Rigidbody', 'Rigidbody.mass', 'GameObject.AddComponent'.");

            string docsRoot = p.Get("docs_root") ?? p.Get("docsRoot");
            if (string.IsNullOrEmpty(docsRoot))
                return new ErrorResponse(
                    "DOCS_NOT_CONFIGURED",
                    "No Unity Documentation directory configured. The CLI normally fills this in automatically.",
                    suggestions: new List<string>
                    {
                        "hera-agent-unity asset-config unity-docs --detect",
                        "hera-agent-unity asset-config unity-docs <path-to-Documentation/en>",
                        "or set HERA_AGENT_UNITY_DOCS=<path>",
                    });

            if (!Directory.Exists(Path.Combine(docsRoot, "ScriptReference")))
                return new ErrorResponse(
                    "DOCS_NOT_CONFIGURED",
                    $"Configured docs_root has no ScriptReference/ subfolder: {docsRoot}",
                    suggestions: new List<string>
                    {
                        "Expected layout: <docs_root>/ScriptReference/*.html",
                        "Reset with: hera-agent-unity asset-config unity-docs --clear",
                    });

            var queryNorm = NormalizeQuery(query);
            foreach (var filename in CandidateFilenames(queryNorm))
            {
                var fullPath = Path.Combine(docsRoot, "ScriptReference", filename);
                if (!File.Exists(fullPath)) continue;

                var (doc, err) = UnityDocsParser.ReadAndParse(docsRoot, filename);
                if (err != null) return new ErrorResponse($"Failed to read {filename}: {err}");
                return new SuccessResponse(
                    $"unity_docs: {doc.title ?? filename}",
                    new
                    {
                        query,
                        query_normalized = queryNorm,
                        title = doc.title,
                        signature = doc.signature,
                        summary = doc.summary,
                        manual_url = doc.manual_url,
                        scriptreference_url = doc.scriptreference_url,
                        unity_version = doc.unity_version,
                    });
            }

            // Miss — surface Levenshtein suggestions
            var suggests = UnityDocsIndex.SuggestSimilar(docsRoot, queryNorm);
            var data = suggests.Count > 0 ? (object)new { did_you_mean = suggests } : null;
            var hints = new List<string>();
            foreach (var s in suggests) hints.Add($"unity_docs {s}");
            if (hints.Count == 0)
                hints.Add($"Indexed ScriptReference files: {UnityDocsIndex.Count(docsRoot)}");

            return new ErrorResponse(
                "DOC_NOT_FOUND",
                $"No ScriptReference page matches '{query}'.",
                data: data,
                suggestions: hints);
        }

        /// <summary>
        /// Strips the UnityEngine./UnityEditor. namespace prefix the docs
        /// filenames omit. Deeper namespaces (UnityEngine.AI.NavMeshAgent →
        /// AI.NavMeshAgent.html) are preserved because the docs preserve them.
        /// </summary>
        static string NormalizeQuery(string query)
        {
            if (query.StartsWith("UnityEngine."))
                return query.Substring("UnityEngine.".Length);
            if (query.StartsWith("UnityEditor."))
                return query.Substring("UnityEditor.".Length);
            return query;
        }

        /// <summary>
        /// Enumerates the file-name candidates a docs query could resolve to.
        /// Order: raw → last-dot-to-dash. Unity's docs encode property pages
        /// with a dash (Rigidbody-mass.html) and methods/classes with a dot
        /// (Rigidbody.AddForce.html, Rigidbody.html) — try the literal form
        /// first to catch methods and classes, then the dashed form for
        /// properties.
        /// </summary>
        static IEnumerable<string> CandidateFilenames(string queryNorm)
        {
            yield return queryNorm + ".html";

            int lastDot = queryNorm.LastIndexOf('.');
            if (lastDot > 0 && lastDot < queryNorm.Length - 1)
            {
                var head = queryNorm.Substring(0, lastDot);
                var tail = queryNorm.Substring(lastDot + 1);
                yield return head + "-" + tail + ".html";
            }
        }
    }
}
