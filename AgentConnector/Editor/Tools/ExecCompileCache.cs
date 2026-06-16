using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tools
{
    /// <summary>
    /// Caches expensive parts of exec compilation:
    ///   - AppDomain assembly location list (invalidated on assembly reload)
    ///   - response file with -r references (written once per ref-set)
    ///   - csc / dotnet resolution
    ///   - compiled DLL bytes on disk, keyed by source+ref hash
    ///   - loaded Assembly in memory, keyed by the same hash
    /// </summary>
    [InitializeOnLoad]
    internal static class ExecCompileCache
    {
        private const int MaxInMemoryAssemblies = 128;

        private static readonly object Gate = new object();
        private static List<string> s_RefLocations;
        private static string s_RefHash;
        private static string s_RefRspPath;
        private static string s_CscPath;
        private static string s_DotnetPath;
        private static string s_MonoPath;

        private struct CachedAssembly
        {
            public Assembly Assembly;
            public object LoadContext; // collectible AssemblyLoadContext; null on Mono fallback
        }

        private static readonly Dictionary<string, CachedAssembly> s_AssemblyCache = new Dictionary<string, CachedAssembly>();
        private static readonly LinkedList<string> s_AssemblyOrder = new LinkedList<string>();

        static ExecCompileCache()
        {
            AssemblyReloadEvents.afterAssemblyReload += Invalidate;
            EditorApplication.quitting += Invalidate;
        }

        public static string CacheDir
        {
            get
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(projectRoot ?? ".", "Library", "HeraAgentCache");
            }
        }

        public static string BinCacheDir => Path.Combine(CacheDir, "bin");

        public static void Invalidate()
        {
            lock (Gate)
            {
                s_RefLocations = null;
                s_RefHash = null;
                s_RefRspPath = null;
                foreach (var entry in s_AssemblyCache.Values)
                    TryUnload(entry.LoadContext);
                s_AssemblyCache.Clear();
                s_AssemblyOrder.Clear();
            }
        }

        /// <summary>
        /// Invokes AssemblyLoadContext.Unload() via reflection when the ALC is
        /// collectible. No-op on Mono runtimes that returned a null context.
        /// </summary>
        public static void TryUnload(object loadContext)
        {
            if (loadContext == null) return;
            try
            {
                var unload = loadContext.GetType().GetMethod("Unload");
                unload?.Invoke(loadContext, null);
            }
            catch { }
        }

        public static string GetRefHash()
        {
            EnsureRefs();
            return s_RefHash;
        }

        /// <summary>Returns a cached response file containing all -r references.</summary>
        public static string GetRefRspPath()
        {
            EnsureRefs();
            return s_RefRspPath;
        }

        private static void EnsureRefs()
        {
            lock (Gate)
            {
                if (s_RefLocations != null && s_RefRspPath != null && File.Exists(s_RefRspPath))
                    return;

                if (TryLoadRefsMeta(out var locations, out var hash, out var rspPath))
                {
                    s_RefLocations = locations;
                    s_RefHash = hash;
                    s_RefRspPath = rspPath;
                    return;
                }

                locations = new List<string>();
                var seenNames = new HashSet<string>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location)) continue;
                        var name = asm.GetName().Name;
                        if (!seenNames.Add(name)) continue;
                        locations.Add(asm.Location);
                    }
                    catch { }
                }
                locations.Sort(StringComparer.Ordinal);
                s_RefLocations = locations;
                s_RefHash = HashStrings(locations);

                Directory.CreateDirectory(CacheDir);
                rspPath = Path.Combine(CacheDir, $"refs-{s_RefHash}.rsp");
                if (!File.Exists(rspPath))
                {
                    var sb = new StringBuilder(locations.Count * 128);
                    foreach (var loc in locations)
                        sb.Append("-r:\"").Append(loc).Append("\"\n");
                    File.WriteAllText(rspPath, sb.ToString(), new UTF8Encoding(false));
                }
                s_RefRspPath = rspPath;
                SaveRefsMeta(locations, s_RefHash, s_RefRspPath);
            }
        }

        private static bool TryLoadRefsMeta(out List<string> locations, out string hash, out string rspPath)
        {
            locations = null;
            hash = null;
            rspPath = null;
            try
            {
                var metaPath = Path.Combine(CacheDir, "refs-meta.json");
                if (!File.Exists(metaPath)) return false;

                var json = File.ReadAllText(metaPath);
                var root = Newtonsoft.Json.Linq.JObject.Parse(json);
                var savedHash = root.Value<string>("hash");
                var savedRsp = root.Value<string>("rspPath");
                var savedCount = root.Value<int?>("assemblyCount") ?? -1;
                var savedLocations = root["locations"] as Newtonsoft.Json.Linq.JArray;
                if (string.IsNullOrEmpty(savedHash) || string.IsNullOrEmpty(savedRsp) || savedLocations == null)
                    return false;
                if (!File.Exists(savedRsp)) return false;
                if (savedCount != savedLocations.Count) return false;

                locations = savedLocations.Select(t => t.ToString()).ToList();
                hash = savedHash;
                rspPath = savedRsp;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SaveRefsMeta(List<string> locations, string hash, string rspPath)
        {
            try
            {
                var metaPath = Path.Combine(CacheDir, "refs-meta.json");
                var root = new Newtonsoft.Json.Linq.JObject
                {
                    ["hash"] = hash,
                    ["rspPath"] = rspPath,
                    ["assemblyCount"] = locations.Count,
                    ["locations"] = new Newtonsoft.Json.Linq.JArray(locations)
                };
                File.WriteAllText(metaPath, root.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch { }
        }

        public static string ResolveCsc(string overridePath)
        {
            if (!string.IsNullOrEmpty(overridePath)) return overridePath;
            lock (Gate)
            {
                if (s_CscPath != null && File.Exists(s_CscPath)) return s_CscPath;
                var configPath = HeraSettings.DefaultCscPath;
                // Honor a configured csc path — UNLESS it points at the bundled
                // Mono csc.exe. The Hera Settings auto-detect persists that path on
                // Unity versions where it can't find the .NET SDK Roslyn (6.5 moved
                // csc.dll to DotNetSdk/sdk/<version>/Roslyn/bincore/), but Mono
                // csc.exe crashes loading System.Text.Encoding.CodePages on a
                // non-Latin (CP949 etc.) Windows console — breaking every exec. Fall
                // through to FindCsc, which resolves the working csc.dll, so a stale
                // or auto-detected Mono path can't override the fix.
                if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath) && !IsMonoCsc(configPath))
                    s_CscPath = configPath;
                else
                    s_CscPath = FindCsc();
                return s_CscPath;
            }
        }

        public static string ResolveDotnet(string overridePath)
        {
            if (!string.IsNullOrEmpty(overridePath)) return overridePath;
            lock (Gate)
            {
                if (s_DotnetPath != null && File.Exists(s_DotnetPath)) return s_DotnetPath;
                var configPath = HeraSettings.DefaultDotnetPath;
                if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
                    s_DotnetPath = configPath;
                else
                    s_DotnetPath = FindDotnet();
                return s_DotnetPath;
            }
        }

        /// <summary>
        /// Resolves the bundled Mono host. On macOS/Linux this is needed to run a
        /// Windows-PE csc.exe (a managed assembly cannot be exec'd directly).
        /// Returns null when no mono host can be found.
        /// </summary>
        public static string ResolveMono()
        {
            lock (Gate)
            {
                if (s_MonoPath != null && File.Exists(s_MonoPath)) return s_MonoPath;
                s_MonoPath = FindMono();
                return s_MonoPath;
            }
        }

        public static bool TryGetAssembly(string key, out Assembly assembly)
        {
            lock (Gate)
            {
                if (s_AssemblyCache.TryGetValue(key, out var entry))
                {
                    s_AssemblyOrder.Remove(key);
                    s_AssemblyOrder.AddLast(key);
                    assembly = entry.Assembly;
                    return true;
                }
                assembly = null;
                return false;
            }
        }

        public static void StoreAssembly(string key, Assembly assembly, object loadContext)
        {
            lock (Gate)
            {
                if (s_AssemblyCache.TryGetValue(key, out var existing))
                {
                    // Replacing: unload the previous ALC. Only safe because the
                    // caller is replacing with a freshly loaded equivalent.
                    if (!ReferenceEquals(existing.LoadContext, loadContext))
                        TryUnload(existing.LoadContext);
                    s_AssemblyCache[key] = new CachedAssembly { Assembly = assembly, LoadContext = loadContext };
                    s_AssemblyOrder.Remove(key);
                    s_AssemblyOrder.AddLast(key);
                    return;
                }
                while (s_AssemblyOrder.Count >= MaxInMemoryAssemblies)
                {
                    var oldest = s_AssemblyOrder.First;
                    if (oldest == null) break;
                    if (s_AssemblyCache.TryGetValue(oldest.Value, out var evicted))
                        TryUnload(evicted.LoadContext);
                    s_AssemblyCache.Remove(oldest.Value);
                    s_AssemblyOrder.RemoveFirst();
                }
                s_AssemblyCache[key] = new CachedAssembly { Assembly = assembly, LoadContext = loadContext };
                s_AssemblyOrder.AddLast(key);
            }
        }

        public static string ComputeKey(string source, string langVersion)
        {
            var refHash = GetRefHash();
            var bytes = Encoding.UTF8.GetBytes(source + "\0" + refHash + "\0" + langVersion);
            using var sha = SHA256.Create();
            return ToHex(sha.ComputeHash(bytes));
        }

        private static string HashStrings(IEnumerable<string> values)
        {
            var sb = new StringBuilder();
            foreach (var v in values)
                sb.Append(v).Append('\n');
            using var sha = SHA256.Create();
            return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // True for the bundled Mono Roslyn (MonoBleedingEdge/.../csc.exe), which
        // fails to load System.Text.Encoding.CodePages on a non-Latin Windows
        // console. A VS / MSBuild csc.exe (full .NET Framework) is not flagged.
        private static bool IsMonoCsc(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var p = path.Replace('\\', '/');
            return p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && p.IndexOf("MonoBleedingEdge", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FindCsc()
        {
            var content = EditorApplication.applicationContentsPath;
            // The .NET SDK Roslyn (`dotnet exec csc.dll`) is the modern, correct
            // compiler. Its path moved between Unity versions — 6.0–6.4:
            // DotNetSdkRoslyn/csc.dll; 6.5+: DotNetSdk/sdk/<version>/Roslyn/
            // bincore/csc.dll (version-numbered) — and macOS nests everything
            // under Resources/Scripting, so the recursive SearchFile below is the
            // real resolver. These are only fast-path hits for the stable layouts.
            var dllCandidates = new[]
            {
                Path.Combine(content, "DotNetSdkRoslyn", "csc.dll"),
                Path.Combine(content, "Resources", "Scripting", "DotNetSdkRoslyn", "csc.dll"),
            };
            foreach (var c in dllCandidates)
                if (File.Exists(c)) return c;

            // Always prefer ANY csc.dll over Mono csc.exe — recursive so it finds
            // the version-numbered 6.5 SDK path too. (A previous MonoBleedingEdge
            // csc.exe fast-path candidate short-circuited here on Windows 6.5 —
            // where the 6.3 csc.dll candidate path no longer exists — and picked
            // the Mono compiler, which fails to load System.Text.Encoding.CodePages
            // on a non-Latin (CP949 etc.) console, breaking every exec.)
            var cscDll = SearchFile(content, "csc.dll");
            if (cscDll != null) return cscDll;

            // Mono csc.exe is the last resort — only when no .NET Roslyn ships.
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                var cscExe = SearchFile(content, "csc.exe");
                if (cscExe != null) return cscExe;
            }
            return null;
        }

        private static string FindDotnet()
        {
            var name = "dotnet" + (Application.platform == RuntimePlatform.WindowsEditor ? ".exe" : "");
            var content = EditorApplication.applicationContentsPath;
            var candidates = new[]
            {
                Path.Combine(content, "dotnet", name),
                Path.Combine(content, name),
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;

            var found = SearchFile(content, name);
            if (found != null) return found;
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                var macPaths = new[]
                {
                    "/usr/local/share/dotnet/dotnet",
                    "/opt/homebrew/bin/dotnet",
                    "/usr/local/bin/dotnet",
                };
                foreach (var p in macPaths)
                    if (File.Exists(p)) return p;
            }
            return name;
        }

        private static string FindMono()
        {
            var name = "mono" + (Application.platform == RuntimePlatform.WindowsEditor ? ".exe" : "");
            var content = EditorApplication.applicationContentsPath;
            var candidates = new[]
            {
                // Windows layout (applicationContentsPath = .../Editor/Data).
                Path.Combine(content, "MonoBleedingEdge", "bin", name),
                // macOS layout (applicationContentsPath = .../Unity.app/Contents).
                Path.Combine(content, "Resources", "Scripting", "MonoBleedingEdge", "bin", name),
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;

            return SearchFile(content, name);
        }

        private static string SearchFile(string dir, string name)
        {
            try
            {
                var files = Directory.GetFiles(dir, name, SearchOption.AllDirectories);
                foreach (var f in files)
                    if (Path.GetFileName(f) == name)
                        return f;
            }
            catch { }
            return null;
        }
    }
}
