using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraActionSafety("find", ReadOnly = true, Idempotent = true)]
    [HeraActionSafety("deps", ReadOnly = true, Idempotent = true)]
    [HeraActionSafety("mkdir", Idempotent = true, MayReloadDomain = true)]
    [HeraActionSafety("create", MayReloadDomain = true)]
    [HeraActionSafety("copy", MayReloadDomain = true)]
    [HeraActionSafety("move", Destructive = true, MayReloadDomain = true)]
    [HeraActionSafety("delete", Destructive = true, MayReloadDomain = true)]
    [HeraActionContract("find", typeof(ManageAssets.FindParameters), ResultType = typeof(ManageAssets.FindResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("deps", typeof(ManageAssets.DepsParameters), ResultType = typeof(ManageAssets.DepsResult), RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("mkdir", typeof(ManageAssets.PathParameters), ResultType = typeof(ManageAssets.MkdirResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("create", typeof(ManageAssets.CreateParameters), ResultType = typeof(ManageAssets.CreateResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("copy", typeof(ManageAssets.TransferParameters), ResultType = typeof(ManageAssets.TransferResult), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("move", typeof(ManageAssets.TransferParameters), ResultType = typeof(ManageAssets.TransferResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraActionContract("delete", typeof(ManageAssets.PathParameters), ResultType = typeof(ManageAssets.PathResult), RiskClass = HeraRiskClass.Destructive)]
    [HeraArgumentGroup(
        ToolArgumentGroupMode.AtLeastOne,
        "filter",
        "type",
        Action = "find",
        Path = "/filter",
        Expected = "filter or type")]
    [HeraTool(
        Name = "manage_assets",
        Description = "Compact AssetDatabase operations: find, deps, mkdir, create, copy, move, delete. create instantiates a ScriptableObject subclass as an Assets/ .asset (optional initial field values via --params '{\"properties\":{...}}'). deps answers both dependency directions — forward is what an asset uses, reverse is what uses it, which is the question to ask before delete or move. Mutating paths are constrained to Assets/.",
        Destructive = true,
        MayReloadDomain = true,
        Examples = new[]
        {
            "manage_assets find --type Texture2D --filter icon --limit 20",
            "manage_assets deps --path Assets/Prefabs/Player.prefab --direction forward",
            "manage_assets deps --path Assets/Art/Hero.mat --direction reverse",
            "manage_assets mkdir --path Assets/Generated/UI",
            "manage_assets create --type GameConfig --path Assets/Config/Game.asset",
            "manage_assets copy --path Assets/A.prefab --new_path Assets/B.prefab",
            "manage_assets move --path Assets/Old.asset --new_path Assets/New.asset",
            "manage_assets delete --path Assets/Generated/Temp.asset",
        },
        ExampleDescriptions = new[]
        {
            "Find project assets with a compact path/type/guid payload",
            "List what a prefab uses (add --recursive for the transitive set)",
            "List what still references a material — ask before deleting or moving it",
            "Create an Assets/ folder recursively; existing folders are accepted",
            "Create a ScriptableObject asset of the named subclass (add --params '{\"properties\":{\"m_Field\":1}}' to set fields)",
            "Copy one asset file to another Assets/ path",
            "Move or rename one asset file",
            "Delete one asset file or folder under Assets/",
        },
        Profiles = new[] { "assets" },
        RiskClass = HeraRiskClass.Destructive,
        ContractMode = ToolContractMode.Strict)]
    public static class ManageAssets
    {
        public sealed class FindParameters
        {
            [ToolParameter("AssetDatabase.FindAssets filter text.")]
            public string Filter { get; set; }

            [ToolParameter("Asset type filter (Texture2D, Material, Prefab).")]
            public string Type { get; set; }

            [ToolParameter(
                "Maximum results (default 50, max 500).",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":500}")]
            public int? Limit { get; set; }

            [ToolParameter("Whether folders are included (default false).")]
            public bool? IncludeFolders { get; set; }
        }

        public class PathParameters
        {
            [ToolParameter("Asset path under Assets/, or a durable handle for an existing asset (guid:<32hex>[:<fileId>] or a GlobalObjectId).", Required = true)]
            public string Path { get; set; }
        }

        public sealed class DepsParameters
        {
            [ToolParameter("Asset path to inspect, or a durable handle (guid:<32hex>[:<fileId>] or a GlobalObjectId).", Required = true)]
            public string Path { get; set; }

            [ToolParameter(
                "forward = what this asset uses. reverse = what uses this asset.",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"forward\",\"reverse\"]}")]
            public string Direction { get; set; }

            [ToolParameter("forward only: follow dependencies transitively (default false).")]
            public bool? Recursive { get; set; }

            [ToolParameter(
                "reverse only: which assets to scan. 'assets' (default) scans Assets/; 'all' also scans Packages/.",
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"assets\",\"all\"]}")]
            public string Scope { get; set; }

            [ToolParameter(
                "Maximum results (default 100, max 1000).",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":1000}")]
            public int? Limit { get; set; }
        }

        public sealed class CreateParameters : PathParameters
        {
            [ToolParameter("ScriptableObject subclass name.", Required = true)]
            public string Type { get; set; }

            [ToolParameter(
                "Raw SerializedProperty name to value map.",
                SchemaJson = "{\"type\":\"object\",\"additionalProperties\":true}")]
            public JObject Properties { get; set; }
        }

        public sealed class TransferParameters : PathParameters
        {
            [ToolParameter("Destination asset path under Assets/. A handle names an existing asset, so it is not accepted here.", Required = true)]
            public string NewPath { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class AssetSummary
        {
            public string Path { get; set; }
            public string Guid { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class FindResult
        {
            public string Query { get; set; }
            public int Total { get; set; }
            public int Returned { get; set; }
            public bool Truncated { get; set; }
            public AssetSummary[] Assets { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public class PathResult
        {
            public string Path { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class DepsResult
        {
            public string Path { get; set; }
            public string Direction { get; set; }
            public int Total { get; set; }
            public int Returned { get; set; }
            public bool Truncated { get; set; }
            public AssetSummary[] Assets { get; set; }

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public bool? Recursive { get; set; }

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string Scope { get; set; }

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public int? Scanned { get; set; }

            [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public long? ElapsedMs { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class MkdirResult : PathResult
        {
            public bool Created { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class CreateResult : PathResult
        {
            public string Type { get; set; }
            public string Guid { get; set; }
            public string[] Applied { get; set; }
        }

        [Newtonsoft.Json.JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
        public sealed class TransferResult : PathResult
        {
            public string NewPath { get; set; }
        }

        public class Parameters
        {
            [ToolParameter("Action: find, deps, mkdir, create, copy, move, delete.", Required = true)]
            public string Action { get; set; }

            [ToolParameter("For deps: 'forward' (what this asset uses) or 'reverse' (what uses this asset).", Required = false)]
            public string Direction { get; set; }

            [ToolParameter("For deps forward: follow dependencies transitively.", Required = false)]
            public bool Recursive { get; set; }

            [ToolParameter("For deps reverse: 'assets' (default) or 'all' to also scan Packages/.", Required = false)]
            public string Scope { get; set; }

            [ToolParameter("Path for mkdir/create/copy/move/delete, under Assets/. For create, the .asset destination ('.asset' is appended if omitted).", Required = false)]
            public string Path { get; set; }

            [ToolParameter("Destination path for copy/move, under Assets/.", Required = false)]
            public string NewPath { get; set; }

            [ToolParameter("AssetDatabase.FindAssets filter text.", Required = false)]
            public string Filter { get; set; }

            [ToolParameter("For find: asset type filter (Texture2D, Material, Prefab). For create: the ScriptableObject subclass to instantiate — short name 'GameConfig' or fully-qualified 'My.Namespace.GameConfig'.", Required = false)]
            public string Type { get; set; }

            [ToolParameter("For create only: JSON map of raw SerializedProperty name → value to set on the new asset, e.g. {\"m_Speed\":5}. Pass via --params.", Required = false)]
            public object Properties { get; set; }

            [ToolParameter("Maximum find results (default 50, max 500).", Required = false)]
            public int Limit { get; set; }

            [ToolParameter("Whether find includes folders (default false).", Required = false)]
            public bool IncludeFolders { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params ?? new JObject());
            var action = (p.GetRaw("args") as JArray)?[0]?.ToString() ?? p.Get("action");
            if (string.IsNullOrWhiteSpace(action))
                return new ErrorResponse("MISSING_PARAM", "'action' required: find, deps, mkdir, create, copy, move, or delete.");

            switch (action.ToLowerInvariant())
            {
                case "find": return Find(p);
                case "deps": return Deps(p);
                case "mkdir": return Mkdir(p.Get("path"));
                case "create": return Create(p);
                case "copy": return Copy(p.Get("path"), p.Get("new_path"));
                case "move": return Move(p.Get("path"), p.Get("new_path"));
                case "delete": return Delete(p.Get("path"));
                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action '{action}'. Valid: find, deps, mkdir, create, copy, move, delete.");
            }
        }

        // Both directions come from AssetDatabase rather than Unity Search.
        // Search answers the reverse question in milliseconds, but its index
        // lags the asset database — queried right after an asset is written it
        // returns nothing — and an empty reverse result is exactly what an
        // agent reads as "safe to delete". AssetDatabase is authoritative and
        // never stale; the reverse scan's cost is reported instead of hidden.
        private static object Deps(ToolParams p)
        {
            var rawPath = p.Get("path");
            if (string.IsNullOrWhiteSpace(rawPath))
                return new ErrorResponse("MISSING_PARAM", "'path' required (the asset to inspect).");
            var path = rawPath.Replace('\\', '/').Trim();

            var direction = p.Get("direction", "").Trim().ToLowerInvariant();
            if (direction != "forward" && direction != "reverse")
                return new ErrorResponse("MISSING_PARAM",
                    "'direction' required: 'forward' lists what this asset uses, 'reverse' lists what uses it. They answer opposite questions, so there is no safe default.");

            if (!AssetPathGuard.TryResolveAssetHandle(
                    path, out path, out _, out var handleCode, out var handleError))
                return new ErrorResponse(handleCode, handleError);

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                return new ErrorResponse("ASSET_NOT_FOUND", $"No asset at '{path}'.");

            var limit = Mathf.Clamp(p.GetInt("limit", 100).Value, 1, 1000);
            return direction == "forward"
                ? DepsForward(path, p.GetBool("recursive"), limit)
                : DepsReverse(path, p.Get("scope", "assets").Trim().ToLowerInvariant(), limit);
        }

        private static object DepsForward(string path, bool recursive, int limit)
        {
            var deps = AssetDatabase.GetDependencies(path, recursive);
            var kept = new List<string>(deps.Length);
            foreach (var d in deps)
            {
                // Unity includes the queried asset in its own dependency set;
                // "what this uses" must not list the asset itself.
                if (d == path) continue;
                kept.Add(d);
            }

            var assets = Describe(kept, limit);
            return new SuccessResponse(
                $"{kept.Count} dependency(ies) of {path}.",
                new
                {
                    path,
                    direction = "forward",
                    recursive,
                    total = kept.Count,
                    returned = assets.Count,
                    truncated = kept.Count > assets.Count,
                    assets,
                });
        }

        private static object DepsReverse(string path, string scope, int limit)
        {
            bool assetsOnly = scope != "all";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var hits = new List<string>();
            int scanned = 0;

            foreach (var candidate in AssetDatabase.GetAllAssetPaths())
            {
                if (candidate == path) continue;
                if (assetsOnly && !candidate.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (AssetDatabase.IsValidFolder(candidate)) continue;
                scanned++;
                foreach (var d in AssetDatabase.GetDependencies(candidate, false))
                {
                    if (d != path) continue;
                    hits.Add(candidate);
                    break;
                }
            }
            watch.Stop();

            var assets = Describe(hits, limit);
            bool truncated = hits.Count > assets.Count;
            var response = new SuccessResponse(
                hits.Count == 0
                    ? $"Nothing under {(assetsOnly ? "Assets/" : "Assets/ or Packages/")} references {path}."
                    : $"{hits.Count} asset(s) reference {path}.",
                new
                {
                    path,
                    direction = "reverse",
                    scope = assetsOnly ? "assets" : "all",
                    total = hits.Count,
                    returned = assets.Count,
                    truncated,
                    scanned,
                    elapsed_ms = watch.ElapsedMilliseconds,
                    assets,
                });
            if (truncated)
                response.agent_hint = $"Showing {assets.Count} of {hits.Count}. This list is INCOMPLETE — raise --limit before treating the asset as safe to delete or move.";
            else if (hits.Count == 0 && assetsOnly)
                response.agent_hint = "Packages/ was not scanned. Pass --scope all if a package could reference this asset.";
            return response;
        }

        private static List<object> Describe(List<string> paths, int limit)
        {
            var described = new List<object>(Math.Min(paths.Count, limit));
            foreach (var path in paths)
            {
                if (described.Count >= limit) break;
                described.Add(new
                {
                    path,
                    guid = AssetDatabase.AssetPathToGUID(path),
                    name = Path.GetFileNameWithoutExtension(path),
                    type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name,
                });
            }
            return described;
        }

        private static object Find(ToolParams p)
        {
            var filter = p.Get("filter", "").Trim();
            var type = p.Get("type", "").Trim();
            if (string.IsNullOrEmpty(filter) && string.IsNullOrEmpty(type))
                return new ErrorResponse("MISSING_PARAM", "'find' requires --filter, --type, or both to avoid oversized project scans.");

            var query = BuildQuery(filter, type);
            var limit = Mathf.Clamp(p.GetInt("limit", 50).Value, 1, 500);
            var includeFolders = p.GetBool("include_folders");
            var guids = AssetDatabase.FindAssets(query);
            var assets = new List<object>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;
                var isFolder = AssetDatabase.IsValidFolder(path);
                if (isFolder && !includeFolders)
                    continue;

                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                assets.Add(new
                {
                    path,
                    guid,
                    name = Path.GetFileNameWithoutExtension(path),
                    type = isFolder ? "Folder" : assetType?.Name,
                });

                if (assets.Count >= limit)
                    break;
            }

            return new SuccessResponse("Assets found", new
            {
                query,
                total = guids.Length,
                returned = assets.Count,
                truncated = guids.Length > assets.Count,
                assets,
            });
        }

        private static object Mkdir(string rawPath)
        {
            if (!AssetPathGuard.TryNormalizeAssetFolder(rawPath, out var path, out var error))
                return new ErrorResponse("INVALID_PATH", error);

            if (path == "Assets" || AssetDatabase.IsValidFolder(path))
                return new SuccessResponse("Folder exists", new { path, created = false });

            var current = "Assets";
            var parts = path.Substring("Assets/".Length).Split('/');
            foreach (var part in parts)
            {
                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    var guid = AssetDatabase.CreateFolder(current, part);
                    if (string.IsNullOrEmpty(guid))
                        return new ErrorResponse("ASSET_FOLDER_CREATE_FAILED", $"Unity could not create folder '{next}'.");
                }
                current = next;
            }

            AssetDatabase.Refresh();
            return new SuccessResponse("Folder created", new { path, created = true });
        }

        private static object Create(ToolParams p)
        {
            var (type, typeErr) = ResolveScriptableObjectType(p.Get("type"));
            if (typeErr != null) return typeErr;

            if (!AssetPathGuard.TryPrepareNewAssetFile(
                    p.Get("path"), ".asset", appendExtension: true,
                    out var path, out var pathCode, out var pathErr))
                return new ErrorResponse(pathCode, pathErr);

            var instance = ScriptableObject.CreateInstance(type);
            if (instance == null)
                return new ErrorResponse("ASSET_CREATE_FAILED", $"Unity could not instantiate '{type.FullName}'.");

            // Optional initial field values, reusing the manage_components
            // property-set path so create + populate is one call.
            List<string> applied = null;
            List<object> failed = null;
            var validInitialProperties = true;
            if (p.GetRaw("properties") is JObject props && props.Count > 0)
            {
                applied = new List<string>();
                failed = new List<object>();
                using var so = new SerializedObject(instance);
                foreach (var kv in props)
                {
                    var prop = so.FindProperty(kv.Key);
                    if (prop == null)
                    {
                        failed.Add(new { property = kv.Key, error = "no serialized property" });
                        continue;
                    }
                    var (ok, applyErr) = SerializedPropertyValue.Apply(prop, kv.Value);
                    if (ok) applied.Add(kv.Key);
                    else failed.Add(new { property = kv.Key, error = applyErr });
                }

                validInitialProperties = failed.Count == 0;
                if (validInitialProperties)
                    so.ApplyModifiedProperties();
            }

            if (!validInitialProperties)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                return new ErrorResponse("INVALID_INITIAL_PROPERTIES",
                    "One or more initial properties are invalid; no asset was created.",
                    new { failed });
            }

            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var guid = AssetDatabase.AssetPathToGUID(path);
            object data = applied == null
                ? new { path, type = type.FullName, guid }
                : new { path, type = type.FullName, guid, applied };
            return new SuccessResponse("Asset created", data);
        }

        private static (Type type, ErrorResponse err) ResolveScriptableObjectType(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return (null, new ErrorResponse("MISSING_PARAM", "'type' required for create (a ScriptableObject subclass name)."));

            Type fullMatch = null;
            var shortMatches = new List<Type>();
            foreach (var t in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (t.IsAbstract || t.IsGenericTypeDefinition)
                    continue;
                if (t.FullName == name)
                {
                    fullMatch = t;
                    break;
                }
                if (t.Name == name)
                    shortMatches.Add(t);
            }

            if (fullMatch != null) return (fullMatch, null);
            if (shortMatches.Count == 1) return (shortMatches[0], null);
            if (shortMatches.Count > 1)
                return (null, new ErrorResponse("AMBIGUOUS_TYPE",
                    $"'{name}' matches {shortMatches.Count} ScriptableObject types — use the fully-qualified name.",
                    data: shortMatches.ConvertAll(t => t.FullName)));
            return (null, new ErrorResponse("TYPE_NOT_FOUND",
                $"No non-abstract ScriptableObject subclass named '{name}'. Provide its class name or fully-qualified name."));
        }

        private static object Copy(string rawPath, string rawNewPath)
        {
            if (!NormalizeFilePair(rawPath, rawNewPath, out var path, out var newPath, out var response))
                return response;
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                return new ErrorResponse("ASSET_NOT_FOUND", $"No asset file at '{path}'.");
            if (AssetDatabase.LoadMainAssetAtPath(newPath) != null)
                return new ErrorResponse("ASSET_EXISTS", $"Destination already exists: '{newPath}'.");
            if (!ParentExists(newPath, out var parent))
                return new ErrorResponse("PARENT_FOLDER_MISSING", $"Parent folder '{parent}' does not exist.");

            if (!AssetDatabase.CopyAsset(path, newPath))
                return new ErrorResponse("ASSET_COPY_FAILED", $"Unity could not copy '{path}' to '{newPath}'.");

            AssetDatabase.Refresh();
            return new SuccessResponse("Asset copied", new { path, new_path = newPath });
        }

        private static object Move(string rawPath, string rawNewPath)
        {
            if (!NormalizeFilePair(rawPath, rawNewPath, out var path, out var newPath, out var response))
                return response;
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                return new ErrorResponse("ASSET_NOT_FOUND", $"No asset file at '{path}'.");
            if (AssetDatabase.LoadMainAssetAtPath(newPath) != null)
                return new ErrorResponse("ASSET_EXISTS", $"Destination already exists: '{newPath}'.");
            if (!ParentExists(newPath, out var parent))
                return new ErrorResponse("PARENT_FOLDER_MISSING", $"Parent folder '{parent}' does not exist.");

            var moveError = AssetDatabase.MoveAsset(path, newPath);
            if (!string.IsNullOrEmpty(moveError))
                return new ErrorResponse("ASSET_MOVE_FAILED", moveError);

            AssetDatabase.Refresh();
            return new SuccessResponse("Asset moved", new { path, new_path = newPath });
        }

        private static object Delete(string rawPath)
        {
            if (!AssetPathGuard.TryNormalizeExistingAssetPath(
                    rawPath, out var path, out _, out var code, out var error))
                return new ErrorResponse(code, error);
            if (path == "Assets")
                return new ErrorResponse("INVALID_PATH", "Refusing to delete the Assets root.");
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.IsValidFolder(path))
                return new ErrorResponse("ASSET_NOT_FOUND", $"No asset or folder at '{path}'.");

            if (!AssetDatabase.DeleteAsset(path))
                return new ErrorResponse("ASSET_DELETE_FAILED", $"Unity could not delete '{path}'.");

            AssetDatabase.Refresh();
            return new SuccessResponse("Asset deleted", new { path });
        }

        private static string BuildQuery(string filter, string type)
        {
            if (string.IsNullOrEmpty(type))
                return filter;
            if (filter.IndexOf("t:", StringComparison.OrdinalIgnoreCase) >= 0)
                return filter;
            return string.IsNullOrEmpty(filter) ? "t:" + type : filter + " t:" + type;
        }

        private static bool NormalizeFilePair(
            string rawPath,
            string rawNewPath,
            out string path,
            out string newPath,
            out object response)
        {
            path = null;
            newPath = null;
            response = null;
            // The source names an asset that exists, so a durable handle is a
            // valid way to name it. The destination does not exist yet, so it
            // never is.
            if (!AssetPathGuard.TryNormalizeExistingAssetFile(
                    rawPath, out path, out _, out var code, out var error))
            {
                response = new ErrorResponse(code, error);
                return false;
            }
            if (ObjectIdentity.IsDurableForm(rawNewPath))
            {
                response = new ErrorResponse("INVALID_PATH",
                    $"'{rawNewPath}' is a handle for an existing asset; a destination needs an Assets/ path.");
                return false;
            }
            if (!AssetPathGuard.TryNormalizeAssetFile(rawNewPath, out newPath, out error))
            {
                response = new ErrorResponse("INVALID_PATH", error);
                return false;
            }
            return true;
        }

        private static bool ParentExists(string assetPath, out string parent)
        {
            parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            return !string.IsNullOrEmpty(parent) && AssetDatabase.IsValidFolder(parent);
        }
    }
}
