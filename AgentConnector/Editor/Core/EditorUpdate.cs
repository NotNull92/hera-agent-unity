using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace HeraAgent
{
    internal static class EditorUpdate
    {
        /// <summary>
        /// Runs the action once on the next editor update, after the current command has
        /// answered. Use this rather than EditorApplication.delayCall for work a command
        /// starts and leaves behind: delayCall does not run in an unfocused Editor, so the
        /// work waits indefinitely while the caller has already been told it started.
        /// </summary>
        internal static void Once(Action action)
        {
            void Tick()
            {
                EditorApplication.update -= Tick;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Hera] I failed running deferred editor work: {ex}");
                }
            }

            EditorApplication.update += Tick;
        }

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
