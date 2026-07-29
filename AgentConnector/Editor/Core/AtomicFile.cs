using System;
using System.IO;

namespace HeraAgent
{
    public static class AtomicFile
    {
        public static void WriteAllText(string path, string contents)
        {
            WriteAllTextCore(path, contents, (source, destination) => File.Replace(source, destination, null));
        }

        internal static void WriteAllTextCore(string path, string contents, Action<string, string> replace)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(contents);
                    writer.Flush();
                    stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    try
                    {
                        replace(tmp, path);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        File.Copy(tmp, path, true);
                    }
                }
                else
                    File.Move(tmp, path);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }
}
