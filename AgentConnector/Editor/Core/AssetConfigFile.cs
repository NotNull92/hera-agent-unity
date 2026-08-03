using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    public static class AssetConfigFile
    {
        private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(25);
        private static readonly TimeSpan LockStaleAfter = TimeSpan.FromMilliseconds(ProtocolContracts.AssetConfigLockStaleAfterMilliseconds);

        public static void Update(string path, Func<JObject, JObject> mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));

            using (AcquireLock(path))
            {
                var current = Read(path);
                var updated = mutation(current);
                if (updated == null) throw new InvalidDataException("Asset-config update produced no document.");
                AtomicFile.WriteAllText(path, updated.ToString(Formatting.Indented));
            }
        }

        private static JObject Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return JObject.Parse(File.ReadAllText(path));
            }
            catch (JsonReaderException ex)
            {
                throw new InvalidDataException("asset-config.json is malformed.", ex);
            }
        }

        private static IDisposable AcquireLock(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var lockPath = path + ".lock";
            var deadline = DateTimeOffset.UtcNow + LockTimeout;
            while (true)
            {
                try
                {
                    var stream = new FileStream(
                        lockPath,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.Read);
                    var record = new ConfigLockRecord
                    {
                        Version = ProtocolContracts.AssetConfigLockVersion,
                        Pid = ProjectIdentity.CurrentProcessId,
                        AcquiredAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Nonce = Guid.NewGuid().ToString("N"),
                    };
                    try
                    {
                        var data = Encoding.UTF8.GetBytes(
                            JsonConvert.SerializeObject(record, Formatting.None));
                        stream.Write(data, 0, data.Length);
                        stream.Flush(true);
                        return new ConfigLock(lockPath, stream, record.Nonce);
                    }
                    catch
                    {
                        stream.Dispose();
                        try { File.Delete(lockPath); } catch { }
                        throw;
                    }
                }
                catch (IOException) when (TryRecoverStaleLock(
                    lockPath,
                    DateTimeOffset.UtcNow,
                    ProjectIdentity.IsProcessConfirmedDead))
                {
                    continue;
                }
                catch (IOException) when (DateTimeOffset.UtcNow < deadline)
                {
                    Thread.Sleep(LockRetryDelay);
                }
                catch (IOException ex)
                {
                    throw new IOException("asset-config is busy.", ex);
                }
            }
        }

        private static bool TryRecoverStaleLock(
            string lockPath,
            DateTimeOffset now,
            Func<int, bool> processConfirmedDead)
        {
            FileInfo info;
            try
            {
                info = new FileInfo(lockPath);
                if (!info.Exists) return true;
                if (now - new DateTimeOffset(info.LastWriteTimeUtc) < LockStaleAfter)
                    return false;
            }
            catch
            {
                return false;
            }

            byte[] first;
            try { first = File.ReadAllBytes(lockPath); }
            catch (FileNotFoundException) { return true; }
            catch { return false; }

            ConfigLockRecord record = null;
            try { record = JsonConvert.DeserializeObject<ConfigLockRecord>(Encoding.UTF8.GetString(first)); }
            catch (JsonException) { }
            var valid = record != null
                && record.Version == ProtocolContracts.AssetConfigLockVersion
                && record.Pid > 0
                && !string.IsNullOrEmpty(record.Nonce);
            if (valid && !processConfirmedDead(record.Pid))
                return false;

            byte[] second;
            try { second = File.ReadAllBytes(lockPath); }
            catch (FileNotFoundException) { return true; }
            catch { return false; }
            if (!first.SequenceEqual(second))
                return false;

            try
            {
                File.Delete(lockPath);
                return true;
            }
            catch (FileNotFoundException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ReleaseOwnedLock(string lockPath, string nonce)
        {
            try
            {
                var record = JsonConvert.DeserializeObject<ConfigLockRecord>(
                    File.ReadAllText(lockPath));
                if (record?.Version != ProtocolContracts.AssetConfigLockVersion
                    || !string.Equals(record.Nonce, nonce, StringComparison.Ordinal))
                {
                    return;
                }
                File.Delete(lockPath);
            }
            catch
            {
                // A replacement lock must never be deleted. A failed cleanup is
                // recoverable through the stale-owner policy on the next write.
            }
        }

        internal static bool TryRecoverStaleLockForTests(
            string lockPath,
            DateTimeOffset now,
            Func<int, bool> processConfirmedDead) =>
            TryRecoverStaleLock(lockPath, now, processConfirmedDead);

        internal static void ReleaseOwnedLockForTests(string lockPath, string nonce) =>
            ReleaseOwnedLock(lockPath, nonce);

        private sealed class ConfigLockRecord
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("pid")] public int Pid;
            [JsonProperty("acquired_at_ms")] public long AcquiredAtMs;
            [JsonProperty("nonce")] public string Nonce;
        }

        private sealed class ConfigLock : IDisposable
        {
            private readonly string _path;
            private readonly FileStream _stream;
            private readonly string _nonce;

            public ConfigLock(string path, FileStream stream, string nonce)
            {
                _path = path;
                _stream = stream;
                _nonce = nonce;
            }

            public void Dispose()
            {
                _stream.Dispose();
                ReleaseOwnedLock(_path, _nonce);
            }
        }
    }
}
