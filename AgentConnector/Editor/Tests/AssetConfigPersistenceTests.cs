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
                allPassed &= TestHeraSettingsPreservesLastGoodSnapshot(root);
                allPassed &= TestAssetConfigLockRecovery(root);
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

        private static bool TestHeraSettingsPreservesLastGoodSnapshot(string root)
        {
            var path = Path.Combine(root, "settings-cache.json");
            var initialStamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var failingStamp = initialStamp.AddSeconds(1);
            var now = new DateTimeOffset(initialStamp);
            try
            {
                File.WriteAllText(path,
                    "{\"game_feel_ui_mode\":true,\"game_feel_mode\":true,"
                    + "\"ui_slop_mode\":true,"
                    + "\"defaultCscPath\":\"csc-one\",\"defaultDotnetPath\":\"dotnet-one\","
                    + "\"assets\":[{\"id\":\"dotween\",\"enabled\":true}]}");
                File.SetLastWriteTimeUtc(path, initialStamp);
                HeraSettings.ResetForTests();
                HeraSettings.RefreshForTests(path, now);
                var initial = HeraSettings.SnapshotForTests();
                var initialPassed = initial.GameFeelUiMode
                    && initial.GameFeelMode
                    && initial.UiSlopMode
                    && initial.DotweenPreferred
                    && initial.DefaultCscPath == "csc-one"
                    && initial.DefaultDotnetPath == "dotnet-one";

                File.WriteAllText(path, "{");
                File.SetLastWriteTimeUtc(path, failingStamp);
                HeraSettings.RefreshForTests(path, now.AddSeconds(1));
                var afterFailure = HeraSettings.SnapshotForTests();
                var preserved = afterFailure.GameFeelUiMode
                    && afterFailure.DefaultCscPath == "csc-one";

                File.WriteAllText(path,
                    "{\"game_feel_ui_mode\":false,\"game_feel_mode\":false,"
                    + "\"ui_slop_mode\":false,"
                    + "\"defaultCscPath\":\"csc-two\",\"defaultDotnetPath\":\"dotnet-two\","
                    + "\"assets\":[]}");
                File.SetLastWriteTimeUtc(path, failingStamp);
                HeraSettings.RefreshForTests(path, now.AddSeconds(1).AddMilliseconds(100));
                var beforeRetry = HeraSettings.SnapshotForTests();
                var respectedBackoff = beforeRetry.DefaultCscPath == "csc-one";

                HeraSettings.RefreshForTests(path, now.AddSeconds(1).AddMilliseconds(500));
                var recovered = HeraSettings.SnapshotForTests();
                var recoveredPassed = !recovered.GameFeelUiMode
                    && !recovered.GameFeelMode
                    && !recovered.UiSlopMode
                    && !recovered.DotweenPreferred
                    && recovered.DefaultCscPath == "csc-two"
                    && recovered.DefaultDotnetPath == "dotnet-two";

                File.Delete(path);
                HeraSettings.RefreshForTests(path, now.AddSeconds(2));
                var missing = HeraSettings.SnapshotForTests();
                var missingDefaults = !missing.GameFeelUiMode
                    && missing.DefaultCscPath == null
                    && missing.DefaultDotnetPath == null;

                return ExpectTrue(
                    "HeraSettings preserves last-good values and retries failed timestamps",
                    initialPassed && preserved && respectedBackoff && recoveredPassed && missingDefaults);
            }
            finally
            {
                HeraSettings.ResetForTests();
            }
        }

        private static bool TestAssetConfigLockRecovery(string root)
        {
            var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
            var lockPath = Path.Combine(root, "recovery.lock");
            var old = now.UtcDateTime.Subtract(TimeSpan.FromMinutes(3));

            File.WriteAllText(lockPath,
                "{\"version\":1,\"pid\":999999,\"acquired_at_ms\":0,\"nonce\":\"old-owner\"}");
            File.SetLastWriteTimeUtc(lockPath, old);
            var recoveredDead = AssetConfigFile.TryRecoverStaleLockForTests(
                lockPath,
                now,
                _ => true)
                && !File.Exists(lockPath);

            File.WriteAllText(lockPath,
                "{\"version\":1,\"pid\":42,\"acquired_at_ms\":0,\"nonce\":\"live-owner\"}");
            File.SetLastWriteTimeUtc(lockPath, old);
            var preservedLive = !AssetConfigFile.TryRecoverStaleLockForTests(
                lockPath,
                now,
                _ => false)
                && File.Exists(lockPath);

            File.WriteAllText(lockPath, "{");
            File.SetLastWriteTimeUtc(lockPath, old);
            var recoveredMalformed = AssetConfigFile.TryRecoverStaleLockForTests(
                lockPath,
                now,
                _ => false)
                && !File.Exists(lockPath);

            File.WriteAllText(lockPath,
                "{\"version\":1,\"pid\":42,\"acquired_at_ms\":0,\"nonce\":\"owner-a\"}");
            AssetConfigFile.ReleaseOwnedLockForTests(lockPath, "owner-b");
            var nonceProtected = File.Exists(lockPath);
            AssetConfigFile.ReleaseOwnedLockForTests(lockPath, "owner-a");
            var ownerReleased = !File.Exists(lockPath);

            return ExpectTrue(
                "asset-config lock recovery preserves live owners and recovers abandoned locks",
                recoveredDead && preservedLive && recoveredMalformed && nonceProtected && ownerReleased);
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
