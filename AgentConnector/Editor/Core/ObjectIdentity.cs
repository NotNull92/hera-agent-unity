using System;
using System.Globalization;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HeraAgent
{
    /// <summary>
    /// Durable object handles that survive domain reloads, shared by target
    /// resolution (<see cref="TargetResolver"/>), ObjectReference values
    /// (<see cref="SerializedPropertyValue"/>), and the Editor selection tool.
    /// Two self-describing string forms are recognized:
    /// <list type="bullet">
    /// <item><c>guid:&lt;32hex&gt;</c> — main asset by GUID; an optional
    /// <c>:&lt;fileId&gt;</c> suffix addresses a sub-asset (a sprite in a
    /// sliced sheet, a material inside an FBX).</item>
    /// <item><c>GlobalObjectId_V1-…</c> — Unity's own GlobalObjectId string,
    /// addressing assets and scene objects uniformly.</item>
    /// </list>
    /// Both prefixes are unambiguous, so bare-string interpretation order
    /// elsewhere (digits → instance id, Assets/ → asset, else hierarchy path)
    /// is unaffected.
    /// </summary>
    internal static class ObjectIdentity
    {
        private const string GuidPrefix = "guid:";
        private const string GlobalIdPrefix = "GlobalObjectId_V1-";

        /// <summary>True when <paramref name="s"/> uses a durable form this class resolves.</summary>
        public static bool IsDurableForm(string s)
            => !string.IsNullOrEmpty(s)
               && (s.StartsWith(GuidPrefix, StringComparison.Ordinal)
                   || s.StartsWith(GlobalIdPrefix, StringComparison.Ordinal));

        /// <summary>
        /// Resolve a durable-form string to a loaded object. Returns false with
        /// <paramref name="err"/> set when the form is recognized but does not
        /// resolve; call <see cref="IsDurableForm"/> first to route.
        /// </summary>
        public static bool TryResolve(string s, out Object obj, out string err)
        {
            obj = null;
            err = null;

            if (s.StartsWith(GlobalIdPrefix, StringComparison.Ordinal))
            {
                if (!GlobalObjectId.TryParse(s, out var gid))
                {
                    err = $"could not parse GlobalObjectId '{s}'.";
                    return false;
                }
                obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                if (obj == null)
                {
                    err = $"GlobalObjectId did not resolve (its scene may not be loaded, or the object is gone): '{s}'.";
                    return false;
                }
                return true;
            }

            // guid:<32hex>[:<fileId>]
            var body = s.Substring(GuidPrefix.Length);
            string guid = body;
            long? fileId = null;
            int colon = body.IndexOf(':');
            if (colon >= 0)
            {
                guid = body.Substring(0, colon);
                var fileIdText = body.Substring(colon + 1);
                if (!long.TryParse(fileIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFileId))
                {
                    err = $"invalid fileId '{fileIdText}' in '{s}'.";
                    return false;
                }
                fileId = parsedFileId;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                err = $"no asset for guid '{guid}'.";
                return false;
            }

            if (fileId == null)
            {
                obj = AssetDatabase.LoadMainAssetAtPath(path);
                if (obj == null) err = $"could not load main asset at '{path}' (guid '{guid}').";
                return obj != null;
            }

            foreach (var candidate in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (candidate == null) continue;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out _, out long candidateFileId)
                    && candidateFileId == fileId.Value)
                {
                    obj = candidate;
                    return true;
                }
            }
            err = $"no sub-asset with fileId {fileId.Value} in '{path}' (guid '{guid}').";
            return false;
        }

        /// <summary>
        /// The durable GlobalObjectId string for a loaded object, or null for
        /// objects Unity cannot identify durably (e.g. transient objects in an
        /// unsaved scene report a null-guid id, which would not resolve back).
        /// </summary>
        public static string DurableIdOf(Object obj)
        {
            if (obj == null) return null;
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            return gid.identifierType == 0 ? null : gid.ToString();
        }
    }
}
