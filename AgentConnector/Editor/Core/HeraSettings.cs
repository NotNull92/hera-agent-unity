using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HeraAgent
{
    /// <summary>
    /// Reads the shared ~/.hera-agent-unity/asset-config.json — the same file the
    /// Hera Settings window and the CLI persist — so the connector can honour
    /// user-facing toggles at dispatch time. A successful snapshot is cached by
    /// last-write time. Transient locked or malformed reads preserve the last good
    /// snapshot and are retried after a short backoff; a missing file resets to the
    /// product defaults.
    /// </summary>
    public static class HeraSettings
    {
        private static readonly object s_lock = new object();
        private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromMilliseconds(250);
        private static long s_successStampTicks = long.MinValue;
        private static long s_failedStampTicks = long.MinValue;
        private static long s_retryAfterMs = long.MinValue;
        private static long s_warnedStampTicks = long.MinValue;
        private static bool s_gameFeelUiMode;
        private static bool s_gameFeelMode;
        private static bool s_uiSlopMode;
        private static bool s_dotweenPreferred;
        private static string s_defaultCscPath;
        private static string s_defaultDotnetPath;

        /// <summary>Game Feel UI Mode (Beta) toggle. False when unset.</summary>
        public static bool GameFeelUiMode
        {
            get { Refresh(); return s_gameFeelUiMode; }
        }

        /// <summary>
        /// Game Feel Mode (Beta) toggle — gameplay-wide game-feel guidance
        /// (screen shake, hit stop, control feel, honest juice, ...). False when
        /// unset.
        /// </summary>
        public static bool GameFeelMode
        {
            get { Refresh(); return s_gameFeelMode; }
        }

        /// <summary>
        /// Unity De-slop Mode (Beta) toggle — static visual slop cleanup guidance
        /// (layout, spacing, typography, color discipline; complements Game Feel
        /// Mode's motion/feel). False when unset.
        /// </summary>
        public static bool UiSlopMode
        {
            get { Refresh(); return s_uiSlopMode; }
        }

        /// <summary>
        /// True when DOTween (or DOTween Pro) is enabled in Hera Settings. Mirrors
        /// the existing asset-config contract where `enabled` means "prefer this".
        /// </summary>
        public static bool DotweenPreferred
        {
            get { Refresh(); return s_dotweenPreferred; }
        }

        /// <summary>User-configured csc path, or null when unset.</summary>
        public static string DefaultCscPath
        {
            get { Refresh(); return s_defaultCscPath; }
        }

        /// <summary>User-configured dotnet path, or null when unset.</summary>
        public static string DefaultDotnetPath
        {
            get { Refresh(); return s_defaultDotnetPath; }
        }

        private static string ConfigPath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".hera-agent-unity", "asset-config.json");
        }

        private static void Refresh()
        {
            RefreshFromPath(ConfigPath(), DateTimeOffset.UtcNow, true);
        }

        internal static void RefreshForTests(string path, DateTimeOffset now)
        {
            RefreshFromPath(path, now, false);
        }

        internal static HeraSettingsSnapshot SnapshotForTests()
        {
            lock (s_lock)
            {
                return CurrentSnapshot();
            }
        }

        internal static void ResetForTests()
        {
            lock (s_lock)
            {
                ResetDefaults();
                ResetCacheState();
            }
        }

        private static void RefreshFromPath(string path, DateTimeOffset now, bool logWarning)
        {
            lock (s_lock)
            {
                if (!File.Exists(path))
                {
                    ResetDefaults();
                    ResetCacheState();
                    return;
                }

                long stamp;
                try
                {
                    stamp = File.GetLastWriteTimeUtc(path).Ticks;
                }
                catch (Exception exception) when (exception is IOException
                    || exception is UnauthorizedAccessException)
                {
                    WarnOnce(long.MinValue, exception, logWarning);
                    return;
                }

                if (stamp == s_successStampTicks)
                    return;
                if (stamp == s_failedStampTicks
                    && now.ToUnixTimeMilliseconds() < s_retryAfterMs)
                {
                    return;
                }

                try
                {
                    var root = JObject.Parse(File.ReadAllText(path));
                    var snapshot = ParseSnapshot(root);
                    Publish(snapshot);
                    s_successStampTicks = stamp;
                    s_failedStampTicks = long.MinValue;
                    s_retryAfterMs = long.MinValue;
                    s_warnedStampTicks = long.MinValue;
                }
                catch (Exception exception) when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is Newtonsoft.Json.JsonException)
                {
                    s_failedStampTicks = stamp;
                    s_retryAfterMs = now.Add(FailureRetryDelay).ToUnixTimeMilliseconds();
                    WarnOnce(stamp, exception, logWarning);
                }
            }
        }

        private static HeraSettingsSnapshot ParseSnapshot(JObject root)
        {
            var dotween = false;
            if (root["assets"] is JArray assets)
            {
                foreach (var asset in assets)
                {
                    var id = asset.Value<string>("id");
                    if ((id == "dotween" || id == "dotween_pro")
                        && (asset.Value<bool?>("enabled") ?? false))
                    {
                        dotween = true;
                        break;
                    }
                }
            }

            return new HeraSettingsSnapshot(
                root.Value<bool?>("game_feel_ui_mode")
                    ?? root.Value<bool?>("ui_juicy_mode")
                    ?? false,
                root.Value<bool?>("game_feel_mode") ?? false,
                root.Value<bool?>("ui_slop_mode") ?? false,
                dotween,
                root.Value<string>("defaultCscPath"),
                root.Value<string>("defaultDotnetPath"));
        }

        private static void Publish(HeraSettingsSnapshot snapshot)
        {
            s_gameFeelUiMode = snapshot.GameFeelUiMode;
            s_gameFeelMode = snapshot.GameFeelMode;
            s_uiSlopMode = snapshot.UiSlopMode;
            s_dotweenPreferred = snapshot.DotweenPreferred;
            s_defaultCscPath = snapshot.DefaultCscPath;
            s_defaultDotnetPath = snapshot.DefaultDotnetPath;
        }

        private static HeraSettingsSnapshot CurrentSnapshot()
        {
            return new HeraSettingsSnapshot(
                s_gameFeelUiMode,
                s_gameFeelMode,
                s_uiSlopMode,
                s_dotweenPreferred,
                s_defaultCscPath,
                s_defaultDotnetPath);
        }

        private static void ResetDefaults()
        {
            Publish(new HeraSettingsSnapshot(
                false,
                false,
                false,
                false,
                null,
                null));
        }

        private static void ResetCacheState()
        {
            s_successStampTicks = long.MinValue;
            s_failedStampTicks = long.MinValue;
            s_retryAfterMs = long.MinValue;
            s_warnedStampTicks = long.MinValue;
        }

        private static void WarnOnce(long stamp, Exception exception, bool enabled)
        {
            if (!enabled || stamp == s_warnedStampTicks)
                return;
            s_warnedStampTicks = stamp;
            Debug.LogWarning(
                $"[Hera] I couldn't refresh asset-config.json; keeping the last good settings and retrying: {exception.Message}");
        }

    }

    internal readonly struct HeraSettingsSnapshot
    {
        internal HeraSettingsSnapshot(
            bool gameFeelUiMode,
            bool gameFeelMode,
            bool uiSlopMode,
            bool dotweenPreferred,
            string defaultCscPath,
            string defaultDotnetPath)
        {
            GameFeelUiMode = gameFeelUiMode;
            GameFeelMode = gameFeelMode;
            UiSlopMode = uiSlopMode;
            DotweenPreferred = dotweenPreferred;
            DefaultCscPath = defaultCscPath;
            DefaultDotnetPath = defaultDotnetPath;
        }

        internal bool GameFeelUiMode { get; }
        internal bool GameFeelMode { get; }
        internal bool UiSlopMode { get; }
        internal bool DotweenPreferred { get; }
        internal string DefaultCscPath { get; }
        internal string DefaultDotnetPath { get; }
    }
}
