using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace HeraAgent
{
    internal static class EditorUpdate
    {
        internal static Task Next(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            var source = new TaskCompletionSource<bool>();
            CancellationTokenRegistration registration = default;
            void Tick()
            {
                EditorApplication.update -= Tick;
                registration.Dispose();
                source.TrySetResult(true);
            }

            EditorApplication.update += Tick;
            registration = cancellationToken.Register(() =>
            {
                source.TrySetCanceled(cancellationToken);
            });
            EditorApplication.QueuePlayerLoopUpdate();
            return source.Task;
        }

        internal static async Task Wait(
            int count,
            int delayMs = 0,
            CancellationToken cancellationToken = default)
        {
            if (delayMs > 0)
                await Task.Delay(delayMs, cancellationToken);
            while (count-- > 0)
                await Next(cancellationToken);
        }
    }
}
