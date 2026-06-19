using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "unity_docs",
        Description = "Look up an offline Unity ScriptReference page by class / property / method name. Returns { title, signature, summary, manual_url, scriptreference_url, unity_version } — typically 250-400 bytes — so an AI agent can verify an API exists at this Unity version before piping it through exec. The data set ships inside the UPM package itself (no docs folder on the user's machine, no network).",
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
            "Property (maps to Rigidbody-mass key)",
            "Method (maps to Rigidbody.AddForce key)",
            "Static property on a value type",
            "Editor API; UnityEditor. prefix is stripped before lookup",
        })]
    public static class UnityDocs
    {
        public class Parameters
        {
            [ToolParameter("Unity type, property, or method (Rigidbody, Rigidbody.mass, GameObject.AddComponent, Vector3.zero, UnityEditor.AssetDatabase.Refresh).", Required = true)]
            public string Query { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            if (parameters == null) return new ErrorResponse("MISSING_PARAM", "Parameters cannot be null.");
            var p = new ToolParams(parameters);
            var argsToken = p.GetRaw("args") as JArray;

            string query = p.Get("query")
                ?? (argsToken != null && argsToken.Count >= 1 ? argsToken[0].ToString() : null);
            if (string.IsNullOrEmpty(query))
                return new ErrorResponse("MISSING_PARAM", "'query' required. Examples: 'Rigidbody', 'Rigidbody.mass', 'GameObject.AddComponent'.");

            // Surface a load-time failure (bundled file missing / unreadable)
            // with a structured code so the caller can tell it apart from a
            // genuine query miss.
            if (UnityDocsStore.Count == 0)
            {
                var err = UnityDocsStore.LoadError;
                return new ErrorResponse(
                    "DOCS_BUNDLE_UNAVAILABLE",
                    err ?? "Bundled Unity docs data is unavailable on this connector install.",
                    suggestions: new List<string>
                    {
                        "Reinstall the AgentConnector UPM package — the docs file ships inside it.",
                        "If you're working from a local checkout, run `go run ./tools/build-unity-docs` to regenerate Editor/Data/unity_docs_6.0.jsonl.gz.bytes.",
                    });
            }

            var queryNorm = NormalizeQuery(query);
            foreach (var key in CandidateKeys(queryNorm))
            {
                var entry = UnityDocsStore.Lookup(key);
                if (entry == null) continue;
                // Minimal response shape — caller already knows `query`, the
                // scriptreference_url is derivable from the key, the data set
                // is single-version (6.0). manual_url + scriptreference_url
                // are dropped from the default reply to keep typical lookups
                // ~30 tokens; the original entry is still in the in-memory
                // dict if a follow-up tool ever needs the long form.
                return new SuccessResponse(
                    $"unity_docs: {entry.title ?? key}",
                    new
                    {
                        title = entry.title,
                        signature = entry.signature,
                        summary = entry.summary,
                    });
            }

            var suggests = UnityDocsStore.SuggestSimilar(queryNorm);
            var data = suggests.Count > 0 ? (object)new { did_you_mean = suggests } : null;
            var hints = new List<string>();
            foreach (var s in suggests) hints.Add($"unity_docs {s}");
            if (hints.Count == 0)
                hints.Add($"Indexed entries: {UnityDocsStore.Count}");

            return new ErrorResponse(
                "DOC_NOT_FOUND",
                $"No ScriptReference page matches '{query}'.",
                data: data,
                suggestions: hints);
        }

        /// <summary>
        /// Strips the UnityEngine./UnityEditor. namespace prefix the docs
        /// data omits. Deeper namespaces (UnityEngine.AI.NavMeshAgent →
        /// AI.NavMeshAgent) survive because the docs preserve them.
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
        /// Enumerates the dictionary-key candidates a docs query could map
        /// to. Order: raw → last-dot-to-dash. Unity's docs encode property
        /// pages with a dash (Rigidbody-mass) and methods/classes with a dot
        /// (Rigidbody.AddForce, Rigidbody), so try the literal form first
        /// (methods + classes) then the dashed form (properties).
        /// </summary>
        static IEnumerable<string> CandidateKeys(string queryNorm)
        {
            yield return queryNorm;

            int lastDot = queryNorm.LastIndexOf('.');
            if (lastDot > 0 && lastDot < queryNorm.Length - 1)
            {
                yield return queryNorm.Substring(0, lastDot)
                    + "-"
                    + queryNorm.Substring(lastDot + 1);
            }
        }
    }
}
