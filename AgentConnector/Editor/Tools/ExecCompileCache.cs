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
        private const int MaxInMemoryAssemblies = 32;

        private static readonly object Gate = new object();
        private static List<string> s_RefLocations;
        private static string s_RefHash;
        private static string s_RefRspPath;
        private static string s_CscPath;
        private static string s_DotnetPath;

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

                var locations = new List<string>();
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
                var rspPath = Path.Combine(CacheDir, $"refs-{s_RefHash}.rsp");
                if (!File.Exists(rspPath))
                {
                    var sb = new StringBuilder(locations.Count * 128);
                    foreach (var loc in locations)
                        sb.Append("-r:\"").Append(loc).Append("\"\n");
                    File.WriteAllText(rspPath, sb.ToString(), new UTF8Encoding(false));
                }
                s_RefRspPath = rspPath;
            }
        }

        public static string ResolveCsc(string overridePath)
        {
            if (!string.IsNullOrEmpty(overridePath)) return overridePath;
            lock (Gate)
            {
                if (s_CscPath != null && File.Exists(s_CscPath)) return s_CscPath;
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
                s_DotnetPath = FindDotnet();
                return s_DotnetPath;
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
            return ToHex(SHA256.Create().ComputeHash(bytes));
        }

        private static string HashStrings(IEnumerable<string> values)
        {
            var sb = new StringBuilder();
            foreach (var v in values)
                sb.Append(v).Append('\n');
            return ToHex(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string FindCsc()
        {
            var content = EditorApplication.applicationContentsPath;
            var cscDll = SearchFile(content, "csc.dll");
            if (cscDll != null) return cscDll;
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
            var found = SearchFile(EditorApplication.applicationContentsPath, name);
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
