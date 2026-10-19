using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace HeraAgent.Tests
{
    public static class HttpServerLifecycleTests
    {
        [MenuItem("HeraAgent/Tests/HttpServerLifecycle")]
        public static void RunTests()
        {
            var state = new HttpServer.ListenerState();
            using var first = new HttpListener();
            using var second = new HttpListener();
            using var firstCancellation = new CancellationTokenSource();
            using var secondCancellation = new CancellationTokenSource();

            if (!state.TryAttach(first, firstCancellation, 8090)
                || state.Port != 8090
                || !state.TryDetach(first, out var detached)
                || !ReferenceEquals(detached, firstCancellation)
                || state.Port != 0
                || state.Listener != null
                || !state.TryAttach(second, secondCancellation, 8091)
                || state.TryDetach(first, out _)
                || state.Port != 8091
                || !ReferenceEquals(state.Listener, second))
            {
                throw new InvalidOperationException("[HttpServerLifecycleTests] SOME TESTS FAILED");
            }

            TestBodyReadContextAndLimits();
            Debug.Log("[HttpServerLifecycleTests] ALL PASSED");
        }

        static void TestBodyReadContextAndLimits()
        {
            var bytes = Encoding.UTF8.GetBytes("{\"name\":\"한글\"}");
            using var input = new DeferredBodyStream(bytes);
            var context = new RecordingContext();
            var previous = SynchronizationContext.Current;
            Task<(string body, ErrorResponse error)> pending;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                pending = HttpServer.ReadBody(input, bytes.Length, bytes.Length);
                if (pending.IsCompleted)
                    throw new InvalidOperationException("Body context test must exercise an incomplete read.");
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }

            input.Release();
            if (!pending.Wait(TimeSpan.FromSeconds(5)))
                throw new InvalidOperationException("Body I/O waited for the Editor context.");
            var result = pending.GetAwaiter().GetResult();
            if (context.Posts != 0 || result.error != null || result.body != Encoding.UTF8.GetString(bytes))
                throw new InvalidOperationException("Body I/O captured the Editor context or changed UTF-8 bytes.");

            using var oversized = new MemoryStream(bytes);
            var declared = HttpServer.ReadBody(oversized, bytes.Length, bytes.Length - 1).GetAwaiter().GetResult();
            if (declared.error?.code != "HTTP_REQUEST_BODY_TOO_LARGE" || oversized.Position != 0)
                throw new InvalidOperationException("Declared oversized body was read.");
            var streamed = HttpServer.ReadBody(oversized, -1, bytes.Length - 1).GetAwaiter().GetResult();
            if (streamed.error?.code != "HTTP_REQUEST_BODY_TOO_LARGE")
                throw new InvalidOperationException("Streaming body limit was bypassed.");
        }

        sealed class RecordingContext : SynchronizationContext
        {
            internal int Posts;
            public override void Post(SendOrPostCallback callback, object state)
            {
                Interlocked.Increment(ref Posts);
                ThreadPool.QueueUserWorkItem(_ => callback(state));
            }
        }

        sealed class DeferredBodyStream : MemoryStream
        {
            readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal DeferredBodyStream(byte[] bytes) : base(bytes) { }
            internal void Release() => _gate.TrySetResult(true);
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await _gate.Task.ConfigureAwait(false);
                return Read(buffer, offset, Math.Min(count, 3));
            }
        }
    }
}
