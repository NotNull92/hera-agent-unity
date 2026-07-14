using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraActionSafety("find", ReadOnly = true, Idempotent = true)]
    [HeraActionSafety("mkdir", Idempotent = true, MayReloadDomain = true)]
    [HeraActionSafety("create", MayReloadDomain = true)]
    [HeraActionSafety("copy", MayReloadDomain = true)]
    [HeraActionSafety("move", Destructive = true, MayReloadDomain = true)]
    [HeraActionSafety("delete", Destructive = true, MayReloadDomain = true)]
    [HeraTool(
        Name = "manage_assets",
        Description = "Compact AssetDatabase operations: find, mkdir, create, copy, move, delete. create instantiates a ScriptableObject subclass as an Assets/ .asset (optional initial field values via --params '{\"properties\":{...}}'). Paths are constrained to Assets/.",
        Destructive = true,
        MayReloadDomain = true,
        Examples = new[]
        {
            "manage_assets find --type Texture2D --filter icon --limit 20",
            "manage_assets mkdir --path Assets/Generated/UI",
            "manage_assets create --type GameConfig --path Assets/Config/Game.asset",
            "manage_assets copy --path Assets/A.prefab --new_path Assets/B.prefab",
            "manage_assets move --path Assets/Old.asset --new_path Assets/New.asset",
            "manage_assets delete --path Assets/Generated/Temp.asset",
        },
        ExampleDescriptions = new[]
        {
            "Find project assets with a compact path/type/guid payload",
            "Create an Assets/ folder recursively; existing folders are accepted",
            "Create a ScriptableObject asset of the named subclass (add --params '{\"properties\":{\"m_Field\":1}}' to set fields)",
            "Copy one asset file to another Assets/ path",
            "Move or rename one asset file",
            "Delete one asset file or folder under Assets/",
        })]
    public static class ManageAssets
    {
        public class Parameters
        {
            [ToolParameter("Action: find, mkdir, create, copy, move, delete.", Required = true)]
            public string Action { get; set; }

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
                return new ErrorResponse("MISSING_PARAM", "'action' required: find, mkdir, create, copy, move, or delete.");

            switch (action.ToLowerInvariant())
            {
                case "find": return Find(p);
                case "mkdir": return Mkdir(p.Get("path"));
                case "create": return Create(p);
                case "copy": return Copy(p.Get("path"), p.Get("new_path"));
                case "move": return Move(p.Get("path"), p.Get("new_path"));
                case "delete": return Delete(p.Get("path"));
                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action '{action}'. Valid: find, mkdir, create, copy, move, delete.");
            }
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
            if (!AssetPathGuard.TryNormalizeAssetPath(rawPath, out var path, out var error))
                return new ErrorResponse("INVALID_PATH", error);
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
            if (!AssetPathGuard.TryNormalizeAssetFile(rawPath, out path, out var error))
            {
                response = new ErrorResponse("INVALID_PATH", error);
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
