using System;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace HeraAgent
{
    /// <summary>
    /// Low-level editor refresh helper used by the refresh_unity tool.
    /// Keeps the [HeraTool] wrapper thin and makes the refresh/compile logic
    /// reusable from other editor code.
    /// </summary>
    public static class AssetRefresh
    {
        public class Result
        {
            public bool refreshTriggered;
            public bool compileRequested;
            public bool force;
            public ErrorResponse error;
        }

        /// <summary>
        /// Refreshes the asset database and optionally requests a script compilation.
        /// </summary>
        /// <param name="mode">"force" or "if_dirty" (default).</param>
        /// <param name="compile">"request" or "none" (default).</param>
        /// <param name="force">If true, allow refresh while entering play mode.</param>
        public static Result Refresh(string mode, string compile, bool force)
        {
            var result = new Result { force = force };

            if (!force && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                result.error = new ErrorResponse("PLAYMODE_REFRESH_BLOCKED", "Cannot refresh while Unity is in or entering play mode. Exit play mode first, or pass --force if this is intentional.");
                return result;
            }

            AssetDatabase.Refresh(string.Equals(mode, "force", StringComparison.OrdinalIgnoreCase)
                ? ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport
                : ImportAssetOptions.ForceSynchronousImport);
            result.refreshTriggered = true;

            if (string.Equals(compile, "request", StringComparison.OrdinalIgnoreCase))
            {
                Heartbeat.MarkCompileRequested();
                CompilationPipeline.RequestScriptCompilation();
                result.compileRequested = true;
            }

            return result;
        }
    }
}
