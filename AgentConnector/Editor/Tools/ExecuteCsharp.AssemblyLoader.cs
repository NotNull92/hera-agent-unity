using System;
using System.IO;
using System.Reflection;

namespace HeraAgent.Tools
{
    public static partial class ExecuteCsharp
    {
        private struct LoadedAssembly
        {
            public Assembly Assembly;
            public object LoadContext; // collectible ALC; null when fallback to Assembly.Load
        }

        private static LoadedAssembly LoadAssembly(byte[] bytes, string id)
        {
            try
            {
                var alcType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader");
                if (alcType != null)
                {
                    var ctor = alcType.GetConstructor(new[] { typeof(string), typeof(bool) });
                    var alc = ctor?.Invoke(new object[] { "hera-agent-unity-exec-" + id, true });
                    var loadMethod = alcType.GetMethod("LoadFromStream", new[] { typeof(Stream) });
                    if (alc != null && loadMethod != null)
                    {
                        using (var ms = new MemoryStream(bytes))
                        {
                            var asm = (Assembly)loadMethod.Invoke(alc, new object[] { ms });
                            return new LoadedAssembly { Assembly = asm, LoadContext = alc };
                        }
                    }
                }
            }
            catch { }
            return new LoadedAssembly { Assembly = Assembly.Load(bytes), LoadContext = null };
        }
    }
}
