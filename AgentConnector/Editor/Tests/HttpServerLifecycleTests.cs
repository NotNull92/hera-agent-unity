using System;
using System.Net;
using System.Threading;
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

            Debug.Log("[HttpServerLifecycleTests] ALL PASSED");
        }
    }
}
