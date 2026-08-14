using System;
using System.IO;
using HeraAgent.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class ExecCompileCacheTests
    {
        [MenuItem("HeraAgent/Tests/ExecCompileCache")]
        public static void RunTests()
        {
            var root = Path.Combine(Path.GetTempPath(), "hera-exec-compile-cache-tests-" + Guid.NewGuid().ToString("N"));
            var allPassed = true;

            try
            {
                Directory.CreateDirectory(root);

                allPassed &= ExpectEqual(
                    "legacy DotNetSdkRoslyn csc",
                    CreateFile(root, "Legacy", "DotNetSdkRoslyn", "csc.dll"),
                    ExecCompileCache.FindBundledCsc(Path.Combine(root, "Legacy")));

                allPassed &= ExpectEqual(
                    "versioned DotNetSdk csc",
                    CreateFile(root, "Modern", "DotNetSdk", "sdk", "8.0.318", "Roslyn", "bincore", "csc.dll"),
                    ExecCompileCache.FindBundledCsc(Path.Combine(root, "Modern")));

                allPassed &= ExpectEqual(
                    "legacy NetCoreRuntime dotnet",
                    CreateFile(root, "LegacyDotnet", "NetCoreRuntime", DotnetName()),
                    ExecCompileCache.FindBundledDotnet(Path.Combine(root, "LegacyDotnet"), DotnetName()));

                allPassed &= ExpectEqual(
                    "modern DotNetSdk dotnet",
                    CreateFile(root, "ModernDotnet", "DotNetSdk", DotnetName()),
                    ExecCompileCache.FindBundledDotnet(Path.Combine(root, "ModernDotnet"), DotnetName()));

                var currentEditor = Path.Combine(root, "Current", "Editor", "Data");
                var staleEditor = Path.Combine(root, "Stale", "Editor", "Data");
                var staleCsc = CreateFile(staleEditor, "DotNetSdkRoslyn", "csc.dll");
                var externalCsc = CreateFile(root, "External", "dotnet", "sdk", "8.0.318", "Roslyn", "bincore", "csc.dll");

                allPassed &= ExpectTrue(
                    "stale Unity bundled csc rejected",
                    ExecCompileCache.IsBundledToolPathForDifferentEditor(staleCsc, currentEditor));
                allPassed &= ExpectFalse(
                    "external SDK csc allowed",
                    ExecCompileCache.IsBundledToolPathForDifferentEditor(externalCsc, currentEditor));

                var configuredCscA = CreateFile(root, "ConfiguredA", "csc.dll");
                var configuredCscB = CreateFile(root, "ConfiguredB", "csc.dll");
                var configuredDotnetA = CreateFile(root, "ConfiguredA", DotnetName());
                var configuredDotnetB = CreateFile(root, "ConfiguredB", DotnetName());
                ExecCompileCache.Invalidate();
                allPassed &= ExpectEqual(
                    "configured csc changes without domain reload",
                    configuredCscA,
                    ExecCompileCache.ResolveCscFromConfiguration(configuredCscA));
                allPassed &= ExpectEqual(
                    "changed configured csc replaces cached path",
                    configuredCscB,
                    ExecCompileCache.ResolveCscFromConfiguration(configuredCscB));
                allPassed &= ExpectEqual(
                    "configured dotnet changes without domain reload",
                    configuredDotnetA,
                    ExecCompileCache.ResolveDotnetFromConfiguration(configuredDotnetA));
                allPassed &= ExpectEqual(
                    "changed configured dotnet replaces cached path",
                    configuredDotnetB,
                    ExecCompileCache.ResolveDotnetFromConfiguration(configuredDotnetB));

                allPassed &= StaleReferenceMetadataIsIgnored(root);

                var compiler = CreateFile(root, "Compiler", "csc.dll");
                var dotnet = CreateFile(root, "Runtime", DotnetName());
                File.WriteAllText(compiler, "compiler-v1");
                File.WriteAllText(dotnet, "dotnet-v1");
                var firstIdentity = ExecCompileCache.BuildCompilationIdentity(compiler, dotnet, "latest");
                var firstKey = ExecCompileCache.ComputeKey("return 1;", "latest", firstIdentity);

                File.WriteAllText(compiler, "compiler-v2-with-a-different-length");
                var secondIdentity = ExecCompileCache.BuildCompilationIdentity(compiler, dotnet, "latest");
                var secondKey = ExecCompileCache.ComputeKey("return 1;", "latest", secondIdentity);

                allPassed &= ExpectTrue(
                    "cache identity has explicit format version",
                    firstIdentity.Contains("exec-cache-v2"));
                allPassed &= ExpectFalse(
                    "compiler fingerprint changes cache key",
                    string.Equals(firstKey, secondKey, StringComparison.Ordinal));
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }

            if (allPassed)
                Debug.Log("[ExecCompileCacheTests] ALL PASSED");
            else
                Debug.LogError("[ExecCompileCacheTests] SOME TESTS FAILED");
        }

        private static bool StaleReferenceMetadataIsIgnored(string root)
        {
            var metaPath = Path.Combine(ExecCompileCache.CacheDir, "refs-meta.json");
            var staleRsp = CreateFile(root, "stale-refs.rsp");
            byte[] original = null;
            var existed = File.Exists(metaPath);
            try
            {
                if (existed)
                    original = File.ReadAllBytes(metaPath);
                Directory.CreateDirectory(ExecCompileCache.CacheDir);
                File.WriteAllText(metaPath, new JObject
                {
                    ["hash"] = "stale-reference-hash",
                    ["rspPath"] = staleRsp,
                    ["assemblyCount"] = 1,
                    ["locations"] = new JArray("stale-reference.dll"),
                }.ToString(Formatting.None));

                ExecCompileCache.Invalidate();
                return ExpectFalse(
                    "stale reference metadata ignored after invalidation",
                    string.Equals(
                        ExecCompileCache.GetRefHash(),
                        "stale-reference-hash",
                        StringComparison.Ordinal));
            }
            finally
            {
                if (existed)
                    File.WriteAllBytes(metaPath, original);
                else if (File.Exists(metaPath))
                    File.Delete(metaPath);
                ExecCompileCache.Invalidate();
            }
        }

        private static string DotnetName()
        {
            return Application.platform == RuntimePlatform.WindowsEditor ? "dotnet.exe" : "dotnet";
        }

        private static string CreateFile(string root, params string[] parts)
        {
            var path = root;
            foreach (var part in parts)
                path = Path.Combine(path, part);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, string.Empty);
            return path;
        }

        private static bool ExpectEqual(string label, string expected, string actual)
        {
            if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError($"[FAIL] {label}: expected '{expected}', got '{actual}'");
            return false;
        }

        private static bool ExpectTrue(string label, bool actual)
        {
            if (actual)
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError("[FAIL] " + label + ": expected true");
            return false;
        }

        private static bool ExpectFalse(string label, bool actual)
        {
            if (!actual)
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError("[FAIL] " + label + ": expected false");
            return false;
        }
    }
}
