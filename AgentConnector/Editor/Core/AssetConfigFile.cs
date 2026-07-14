using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    public static class AssetConfigFile
    {
        private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(25);

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
            var deadline = DateTime.UtcNow + LockTimeout;
            while (true)
            {
                try
                {
                    var stream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    return new ConfigLock(lockPath, stream);
                }
                catch (IOException) when (DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(LockRetryDelay);
                }
                catch (IOException ex)
                {
                    throw new IOException("asset-config is busy.", ex);
                }
            }
        }

        private sealed class ConfigLock : IDisposable
        {
            private readonly string _path;
            private readonly FileStream _stream;

            public ConfigLock(string path, FileStream stream)
            {
                _path = path;
                _stream = stream;
            }

            public void Dispose()
            {
                _stream.Dispose();
                File.Delete(_path);
            }
        }
    }
}
