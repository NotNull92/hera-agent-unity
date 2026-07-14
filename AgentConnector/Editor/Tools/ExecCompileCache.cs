using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        // Bump this whenever compiler inputs or the response-file recipe changes.
        // Cache entries are executable assemblies, so incompatible compile inputs
        // must never reuse an existing disk or in-memory entry.
        private const string CompilationCacheFormat = "exec-cache-v2";
        private const string CompilationRecipe = "target=library;nowarn=0105,0162,1701,1702;preferreduilang=en-US;utf8output;shared";

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

                locations = CollectReferenceLocations();
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
                if (IsUsableConfiguredCsc(configPath))
                    s_CscPath = configPath;
                else
                    s_CscPath = FindCsc();
                return s_CscPath;
            }
        }

        public static string ResolveCscUncached(string overridePath)
        {
            if (!string.IsNullOrEmpty(overridePath)) return overridePath;
            var configPath = HeraSettings.DefaultCscPath;
            return IsUsableConfiguredCsc(configPath) ? configPath : FindCsc();
        }

        public static string ResolveDotnet(string overridePath)
        {
            if (!string.IsNullOrEmpty(overridePath)) return overridePath;
            lock (Gate)
            {
                if (s_DotnetPath != null && File.Exists(s_DotnetPath)) return s_DotnetPath;
                var configPath = HeraSettings.DefaultDotnetPath;
                if (IsUsableConfiguredDotnet(configPath))
                    s_DotnetPath = configPath;
                else
                    s_DotnetPath = FindDotnet();
                return s_DotnetPath;
            }
        }

        public static string ResolveDotnetUncached(string overridePath)
        {
            if (!string.IsNullOrEmpty(overridePath)) return overridePath;
            var configPath = HeraSettings.DefaultDotnetPath;
            return IsUsableConfiguredDotnet(configPath) ? configPath : FindDotnet();
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

        public static string ResolveMonoUncached()
        {
            return FindMono();
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

        public static void AppendUncachedReferenceArguments(StringBuilder responseFile)
        {
            foreach (var location in CollectReferenceLocations())
                responseFile.Append("-r:\"").Append(location).Append("\"\n");
        }

        internal static string BuildCompilationIdentity(string cscPath, string hostPath, string langVersion)
        {
            return string.Join("\n", new[]
            {
                "format=" + CompilationCacheFormat,
                "lang=" + (langVersion ?? string.Empty),
                "recipe=" + CompilationRecipe,
                "compiler=" + CompilerFileFingerprint(cscPath),
                "host=" + CompilerFileFingerprint(hostPath),
            });
        }

        public static string GetCompilationIdentity(string cscOverride, string dotnetOverride, string langVersion)
        {
            var csc = ResolveCsc(cscOverride);
            string host = null;
            if (!string.IsNullOrEmpty(csc) && csc.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                host = ResolveDotnet(dotnetOverride);
            else if (!string.IsNullOrEmpty(csc) && Application.platform != RuntimePlatform.WindowsEditor)
                host = ResolveMono();
            return BuildCompilationIdentity(csc, host, langVersion);
        }

        public static string ComputeKey(string source, string langVersion, string compilationIdentity)
        {
            var refHash = GetRefHash();
            var bytes = Encoding.UTF8.GetBytes(source + "\0" + refHash + "\0" + langVersion + "\0" + compilationIdentity);
            using var sha = SHA256.Create();
            return ToHex(sha.ComputeHash(bytes));
        }

        private static List<string> CollectReferenceLocations()
        {
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
            return locations;
        }

        private static string CompilerFileFingerprint(string path)
        {
            if (string.IsNullOrEmpty(path)) return "missing";
            try
            {
                var fullPath = Path.GetFullPath(path);
                var info = new FileInfo(fullPath);
                if (!info.Exists) return "missing:" + fullPath;

                string version = null;
                try { version = FileVersionInfo.GetVersionInfo(fullPath).FileVersion; }
                catch { }
                return string.Join("|", fullPath, info.Length, info.LastWriteTimeUtc.Ticks, version ?? "unknown");
            }
            catch
            {
                return "invalid:" + path;
            }
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

        internal static bool IsUsableConfiguredCsc(string path)
        {
            return !string.IsNullOrEmpty(path)
                && File.Exists(path)
                && !IsMonoCsc(path)
                && !IsBundledToolPathForDifferentEditor(path, EditorApplication.applicationContentsPath);
        }

        internal static bool IsUsableConfiguredDotnet(string path)
        {
            return !string.IsNullOrEmpty(path)
                && File.Exists(path)
                && !IsBundledToolPathForDifferentEditor(path, EditorApplication.applicationContentsPath);
        }

        internal static bool IsBundledToolPathForDifferentEditor(string path, string applicationContentsPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(applicationContentsPath))
                return false;

            var normalizedPath = NormalizePath(path);
            var normalizedContent = NormalizePath(applicationContentsPath);
            if (normalizedPath == null || normalizedContent == null)
                return false;

            if (IsPathUnder(normalizedPath, normalizedContent))
                return false;

            return normalizedPath.IndexOf("/DotNetSdkRoslyn/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedPath.IndexOf("/DotNetSdk/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedPath.IndexOf("/NetCoreRuntime/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedPath.IndexOf("/MonoBleedingEdge/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedPath.IndexOf("/Resources/Scripting/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPathUnder(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
                return false;

            return path.Equals(root, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
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
            var bundled = FindBundledCsc(content);
            if (bundled != null) return bundled;

            return null;
        }

        internal static string FindBundledCsc(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;

            var dllCandidates = new[]
            {
                Path.Combine(content, "DotNetSdkRoslyn", "csc.dll"),
                Path.Combine(content, "Resources", "Scripting", "DotNetSdkRoslyn", "csc.dll"),
            };
            foreach (var c in dllCandidates)
                if (File.Exists(c)) return c;

            var sdkCsc = FindVersionedSdkCsc(content);
            if (sdkCsc != null) return sdkCsc;

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

        private static string FindVersionedSdkCsc(string content)
        {
            var sdkRoots = new[]
            {
                Path.Combine(content, "DotNetSdk", "sdk"),
                Path.Combine(content, "Resources", "Scripting", "DotNetSdk", "sdk"),
            };

            var candidates = new List<(string path, Version version)>();
            foreach (var root in sdkRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var path = Path.Combine(dir, "Roslyn", "bincore", "csc.dll");
                    if (File.Exists(path))
                        candidates.Add((path, ExtractSdkVersionFromPath(dir)));

                    path = Path.Combine(dir, "Roslyn", "csc.dll");
                    if (File.Exists(path))
                        candidates.Add((path, ExtractSdkVersionFromPath(dir)));
                }
            }

            return candidates
                .OrderByDescending(c => c.version)
                .ThenBy(c => c.path, StringComparer.Ordinal)
                .Select(c => c.path)
                .FirstOrDefault();
        }

        private static Version ExtractSdkVersionFromPath(string sdkDirectoryPath)
        {
            var dirName = Path.GetFileName(sdkDirectoryPath);
            return Version.TryParse(dirName, out var version) ? version : new Version(0, 0);
        }

        private static string FindDotnet()
        {
            var name = "dotnet" + (Application.platform == RuntimePlatform.WindowsEditor ? ".exe" : "");
            var content = EditorApplication.applicationContentsPath;
            var bundled = FindBundledDotnet(content, name);
            if (bundled != null) return bundled;

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

        internal static string FindBundledDotnet(string content, string name)
        {
            if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(name))
                return null;

            var candidates = new[]
            {
                Path.Combine(content, "DotNetSdk", name),
                Path.Combine(content, "NetCoreRuntime", name),
                Path.Combine(content, "dotnet", name),
                Path.Combine(content, name),
                Path.Combine(content, "Resources", "Scripting", "DotNetSdk", name),
                Path.Combine(content, "Resources", "Scripting", "NetCoreRuntime", name),
                Path.Combine(content, "Resources", "Scripting", "dotnet", name),
                Path.Combine(content, "Resources", "Scripting", name),
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;

            return SearchFile(content, name);
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
