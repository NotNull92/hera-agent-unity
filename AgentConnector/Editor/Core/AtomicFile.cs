using System;
using System.IO;

namespace HeraAgent
{
    public static class AtomicFile
    {
        public static void WriteAllText(string path, string contents)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, contents);
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
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
