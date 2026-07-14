using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class AssetConfigPersistenceTests
    {
        [MenuItem("HeraAgent/Tests/AssetConfigPersistence")]
        public static void RunTests()
        {
            var root = Path.Combine(Path.GetTempPath(), "hera-asset-config-tests-" + Guid.NewGuid().ToString("N"));
            var allPassed = true;
            try
            {
                Directory.CreateDirectory(root);
                var path = Path.Combine(root, "asset-config.json");
                File.WriteAllText(path, "{\"assets\":[{\"id\":\"dotween\",\"installed\":false,\"vendor\":{\"license\":\"paid\"}}],\"custom_top_level\":true}");

                AssetConfigFile.Update(path, current =>
                {
                    current["assets"][0]["installed"] = true;
                    return current;
                });

                var persisted = JObject.Parse(File.ReadAllText(path));
                allPassed &= ExpectTrue("preserves unknown top-level fields", persisted.Value<bool>("custom_top_level"));
                allPassed &= ExpectEqual("updates known asset field", true, persisted["assets"][0].Value<bool>("installed"));
                allPassed &= ExpectEqual("preserves unknown asset field", "paid", persisted["assets"][0]["vendor"].Value<string>("license"));

                var malformed = Path.Combine(root, "malformed.json");
                File.WriteAllText(malformed, "{");
                allPassed &= ExpectThrows("rejects malformed config", () => AssetConfigFile.Update(malformed, current => current));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                allPassed = false;
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }

            if (allPassed)
                Debug.Log("[AssetConfigPersistenceTests] ALL PASSED");
            else
                Debug.LogError("[AssetConfigPersistenceTests] SOME TESTS FAILED");
        }

        private static bool ExpectEqual<T>(string label, T expected, T actual)
        {
            return ExpectTrue(label, Equals(expected, actual));
        }

        private static bool ExpectThrows(string label, Action action)
        {
            try
            {
                action();
                return ExpectTrue(label, false);
            }
            catch (InvalidDataException)
            {
                return ExpectTrue(label, true);
            }
        }

        private static bool ExpectTrue(string label, bool actual)
        {
            if (actual)
            {
                Debug.Log("[PASS] " + label);
                return true;
            }

            Debug.LogError("[FAIL] " + label);
            return false;
        }
    }
}
