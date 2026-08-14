using HeraAgent.Tools;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class BuildToolTests
    {
        public static void RunTests()
        {
            var development = EditorUserBuildSettings.development;
            var allowDebugging = EditorUserBuildSettings.allowDebugging;
            var buildScriptsOnly = EditorUserBuildSettings.buildScriptsOnly;
            try
            {
                EditorUserBuildSettings.development = true;
                EditorUserBuildSettings.allowDebugging = true;
                EditorUserBuildSettings.buildScriptsOnly = true;

                var options = Build.CreatePlayerOptions(
                    new[] { "Assets/Main.unity" },
                    BuildTarget.StandaloneWindows64,
                    "Builds/Test.exe");
                var expected = BuildOptions.Development
                    | BuildOptions.AllowDebugging
                    | BuildOptions.BuildScriptsOnly;
                if (options.options == expected)
                    Debug.Log("[BuildToolTests] ALL PASSED");
                else
                    Debug.LogError(
                        $"[BuildToolTests] options={options.options}, expected={expected}");
            }
            finally
            {
                EditorUserBuildSettings.development = development;
                EditorUserBuildSettings.allowDebugging = allowDebugging;
                EditorUserBuildSettings.buildScriptsOnly = buildScriptsOnly;
            }
        }
    }
}
