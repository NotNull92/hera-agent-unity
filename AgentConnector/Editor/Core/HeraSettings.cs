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
        private static bool s_juicyMode;
        private static bool s_dotweenPreferred;

        /// <summary>UI Juicy Mode toggle. False when unset or unreadable.</summary>
        public static bool JuicyMode
        {
            get { Refresh(); return s_juicyMode; }
        }

        /// <summary>
        /// True when DOTween (or DOTween Pro) is enabled in Hera Settings. Mirrors
        /// the existing asset-config contract where `enabled` means "prefer this".
        /// </summary>
        public static bool DotweenPreferred
        {
            get { Refresh(); return s_dotweenPreferred; }
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
                        s_juicyMode = false;
                        s_dotweenPreferred = false;
                        return;
                    }

                    var stamp = File.GetLastWriteTimeUtc(path).Ticks;
                    if (stamp == s_stampTicks) return; // unchanged — keep cache
                    s_stampTicks = stamp;

                    var root = JObject.Parse(File.ReadAllText(path));
                    s_juicyMode = root.Value<bool?>("ui_juicy_mode") ?? false;

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
                    s_juicyMode = false;
                    s_dotweenPreferred = false;
                }
            }
        }
    }
}
