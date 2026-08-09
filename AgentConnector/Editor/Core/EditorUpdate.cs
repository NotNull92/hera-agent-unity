using System.Threading.Tasks;
using UnityEditor;

namespace HeraAgent
{
    internal static class EditorUpdate
    {
        internal static Task Next()
        {
            var source = new TaskCompletionSource<bool>();
            void Tick()
            {
                EditorApplication.update -= Tick;
                source.TrySetResult(true);
            }

            EditorApplication.update += Tick;
            return source.Task;
        }

        internal static async Task Wait(int count, int delayMs = 0)
        {
            if (delayMs > 0)
                await Task.Delay(delayMs);
            while (count-- > 0)
                await Next();
        }
    }
}
