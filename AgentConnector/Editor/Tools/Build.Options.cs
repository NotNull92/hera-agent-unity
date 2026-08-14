using UnityEditor;

namespace HeraAgent.Tools
{
    public static partial class Build
    {
        internal static BuildPlayerOptions CreatePlayerOptions(
            string[] scenes,
            BuildTarget target,
            string outputPath)
        {
            var options = BuildOptions.None;
            if (EditorUserBuildSettings.development)
                options |= BuildOptions.Development;
            if (EditorUserBuildSettings.allowDebugging)
                options |= BuildOptions.AllowDebugging;
            if (EditorUserBuildSettings.buildScriptsOnly)
                options |= BuildOptions.BuildScriptsOnly;

            return new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                locationPathName = outputPath,
                options = options,
            };
        }
    }
}
