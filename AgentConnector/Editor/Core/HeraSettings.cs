using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    /// <summary>
    /// Reads the shared ~/.hera-agent-unity/asset-config.json — the same file the
    /// Hera Settings window and the CLI persist — so the connector can honour
    /// user-facing toggles at dispatch time. Cached by the file's last-write time
    /// so a burst of tool calls re-parses only when the user actually changed a
    /// setting. Best-effort: a missing/locked/malformed file reads as "off".
    /// </summary>
    public static class HeraSettings
    {
        private static readonly object s_lock = new object();
        private static long s_stampTicks = long.MinValue;
        private static bool s_gameFeelMode;
        private static bool s_dotweenPreferred;
        private static string s_defaultCscPath;
        private static string s_defaultDotnetPath;

        /// <summary>Game Feel UI Mode (Beta) toggle. False when unset or unreadable.</summary>
        public static bool GameFeelMode
        {
            get { Refresh(); return s_gameFeelMode; }
        }

        /// <summary>
        /// True when DOTween (or DOTween Pro) is enabled in Hera Settings. Mirrors
        /// the existing asset-config contract where `enabled` means "prefer this".
        /// </summary>
        public static bool DotweenPreferred
        {
            get { Refresh(); return s_dotweenPreferred; }
        }

        /// <summary>
        /// User-configured csc path from asset-config.json, or null when unset/unreadable.
        /// </summary>
        public static string DefaultCscPath
        {
            get { Refresh(); return s_defaultCscPath; }
        }

        /// <summary>
        /// User-configured dotnet path from asset-config.json, or null when unset/unreadable.
        /// </summary>
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
            lock (s_lock)
            {
                try
                {
                    var path = ConfigPath();
                    if (!File.Exists(path))
                    {
                        s_stampTicks = long.MinValue;
                        s_gameFeelMode = false;
                        s_dotweenPreferred = false;
                        s_defaultCscPath = null;
                        s_defaultDotnetPath = null;
                        return;
                    }

                    var stamp = File.GetLastWriteTimeUtc(path).Ticks;
                    if (stamp == s_stampTicks) return; // unchanged — keep cache
                    s_stampTicks = stamp;

                    var root = JObject.Parse(File.ReadAllText(path));
                    // Prefer the current key; fall back to the pre-rename `ui_juicy_mode`
                    // so a config not yet re-saved after the Game Feel UI Mode rename still honours the toggle.
                    s_gameFeelMode = root.Value<bool?>("game_feel_ui_mode") ?? root.Value<bool?>("ui_juicy_mode") ?? false;
                    s_defaultCscPath = root.Value<string>("defaultCscPath");
                    s_defaultDotnetPath = root.Value<string>("defaultDotnetPath");

                    bool dotween = false;
                    if (root["assets"] is JArray assets)
                    {
                        foreach (var a in assets)
                        {
                            var id = a.Value<string>("id");
                            if ((id == "dotween" || id == "dotween_pro") && (a.Value<bool?>("enabled") ?? false))
                            {
                                dotween = true;
                                break;
                            }
                        }
                    }
                    s_dotweenPreferred = dotween;
                }
                catch
                {
                    // A malformed or locked file should never break a tool call.
                    s_gameFeelMode = false;
                    s_dotweenPreferred = false;
                    s_defaultCscPath = null;
                    s_defaultDotnetPath = null;
                }
            }
        }
    }
}
