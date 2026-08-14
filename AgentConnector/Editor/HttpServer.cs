using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using HeraAgent.Tools;

namespace HeraAgent
{
    /// <summary>
    /// Debug logging configuration and utilities
    /// </summary>
    public static class DebugLogging
    {
        private static bool _enabled = false;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Debug.Log($"[Hera] Debug logging {(value ? "enabled" : "disabled")}");
            }
        }

        public static void LogRequest(string command, JObject parameters)
        {
            if (!Enabled) return;

            Debug.Log($"[Hera] Request: {command} | Params: {parameters?.ToString(Formatting.Indented) ?? "null"}");
        }

        public static void LogResponse(string command, object response)
        {
            if (!Enabled) return;

            Debug.Log($"[Hera] Response for {command}: {JsonConvert.SerializeObject(response, Formatting.Indented)}");
        }

        public static void LogError(string command, Exception ex)
        {
            if (!Enabled) return;

            Debug.LogError($"[Hera] Error for {command}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Lightweight HTTP server on localhost. Receives CLI commands as POST /command,
    /// dispatches via CommandRouter, returns JSON responses.
    /// Uses ConcurrentQueue + EditorApplication.update for main-thread marshaling
    /// so commands execute even when Unity is unfocused.
    /// Survives domain reloads via InitializeOnLoad.
    /// </summary>
    [InitializeOnLoad]
    public static class HttpServer
    {
        const int DEFAULT_PORT = 8090;
        const int FALLBACK_PORT = 8091;
        const int MAX_PORT_ATTEMPTS = 10;
        const int MAX_COMMAND_BODY_BYTES = 1024 * 1024;
        const int MAX_BATCH_BODY_BYTES = 4 * 1024 * 1024;
        const int MAX_BATCH_COMMANDS = 50;
        const int MAX_PENDING_REQUESTS = 64;

        static readonly ListenerState s_ListenerState = new();

        static readonly ConcurrentQueue<WorkItem> s_Queue = new();
        static int s_PendingRequests;
        static int s_RestartRequested;
        static long s_LastLedgerCleanupMs;

        struct WorkItem
        {
            public string Command;
            public JObject Parameters;
            public CommandRequestContext Context;
            public TaskCompletionSource<object> Tcs;
            // Batch-specific fields (set when POST /commands is received).
            public bool IsBatch;
            public List<CommandRouter.BatchCommandItem> BatchItems;
            public CommandRouter.BatchOptions BatchOptions;
            public JObject ApprovalRequest;
        }

        internal sealed class ListenerState
        {
            readonly object _gate = new();
            HttpListener _listener;
            CancellationTokenSource _cancellation;
            int _port;

            internal HttpListener Listener
            {
                get { lock (_gate) return _listener; }
            }

            internal int Port
            {
                get { lock (_gate) return _port; }
            }

            internal bool TryAttach(
                HttpListener listener,
                CancellationTokenSource cancellation,
                int port)
            {
                lock (_gate)
                {
                    if (_listener != null)
                        return false;
                    _listener = listener;
                    _cancellation = cancellation;
                    _port = port;
                    return true;
                }
            }

            internal bool TryDetach(
                HttpListener listener,
                out CancellationTokenSource cancellation)
            {
                lock (_gate)
                {
                    if (!ReferenceEquals(_listener, listener))
                    {
                        cancellation = null;
                        return false;
                    }
                    cancellation = _cancellation;
                    _listener = null;
                    _cancellation = null;
                    _port = 0;
                    return true;
                }
            }
        }

        static HttpServer()
        {
            Start();
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += StopListener;
            AssemblyReloadEvents.afterAssemblyReload += Start;
            EditorApplication.update += ProcessQueue;
        }

        public static int Port => s_ListenerState.Port;

        static void Start()
        {
            if (s_ListenerState.Listener != null) return;

            for (var attempt = 0; attempt < MAX_PORT_ATTEMPTS; attempt++)
            {
                var port = attempt == 0 ? DEFAULT_PORT : FALLBACK_PORT + attempt - 1;
                HttpListener listener = null;
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    listener.Start();

                    var cancellation = new CancellationTokenSource();
                    if (!s_ListenerState.TryAttach(listener, cancellation, port))
                    {
                        cancellation.Dispose();
                        CloseListener(listener);
                        return;
                    }
                    var token = cancellation.Token;

                    _ = ListenLoop(listener, token).ContinueWith(
                        task => OnListenLoopEnded(listener, token, task));

                    Debug.Log($"[Hera] HTTP server started on port {port}");
                    // Defer compiler pre-warm so editor startup is not blocked by a
                    // potentially slow csc invocation. Not delayCall: it does not run in an
                    // unfocused Editor, which is exactly where Hera usually starts.
                    EditorUpdate.Once(() => ExecuteCsharp.PreWarmCompiler());
                    return;
                }
                catch (HttpListenerException)
                {
                    CloseListener(listener);
                    // Port in use, try next
                }
                catch (System.Net.Sockets.SocketException)
                {
                    CloseListener(listener);
                    // Windows/Mono throws SocketException instead of HttpListenerException
                }
            }

            Debug.LogError("[Hera] Failed to start HTTP server — no available port");
        }

        static void StopListener()
        {
            var listener = s_ListenerState.Listener;
            if (listener != null)
                ReleaseListener(listener);
        }

        static void Stop()
        {
            var port = Port;
            StopListener();
            Debug.Log($"[Hera] HTTP server stopped (was port {port})");
        }

        static void OnListenLoopEnded(
            HttpListener listener,
            CancellationToken token,
            Task task)
        {
            var restart = !token.IsCancellationRequested;
            if (task.IsFaulted)
                Debug.LogError($"[Hera] ListenLoop faulted: {task.Exception?.InnerException ?? task.Exception}");
            if (ReleaseListener(listener) && restart)
                Interlocked.Exchange(ref s_RestartRequested, 1);
        }

        static bool ReleaseListener(HttpListener listener)
        {
            if (!s_ListenerState.TryDetach(listener, out var cancellation))
                return false;
            try { cancellation?.Cancel(); }
            catch (ObjectDisposedException) { }
            cancellation?.Dispose();
            CloseListener(listener);
            return true;
        }

        static void CloseListener(HttpListener listener)
        {
            if (listener == null) return;
            try
            {
                listener.Stop();
                listener.Close();
            }
            catch
            {
            }
        }

        static void ForceEditorUpdate()
        {
            // Wake the queue pump with a repaint only when the editor is in the
            // background — Unity throttles EditorApplication.update hard when
            // unfocused, so a queued command could otherwise wait seconds. When
            // the editor is the active app it already pumps frequently, so a full
            // RepaintAllViews on every command is wasted churn.
            try
            {
                if (UnityEditorInternal.InternalEditorUtility.isApplicationActive) return;
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            catch { }
        }

        static void ProcessQueue()
        {
            if (Interlocked.Exchange(ref s_RestartRequested, 0) != 0)
                Start();
            while (s_Queue.TryDequeue(out var item))
            {
                _ = ProcessItem(item).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Debug.LogError($"[Hera] ProcessItem faulted: {t.Exception?.InnerException ?? t.Exception}");
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        static async Task ProcessItem(WorkItem item)
        {
            try
            {
                object r;
                if (item.ApprovalRequest != null)
                {
                    r = ApprovalPolicy.Preflight(item.ApprovalRequest);
                }
                else if (item.IsBatch)
                {
                    r = await CommandRouter.DispatchBatch(item.BatchItems, item.BatchOptions);
                }
                else
                {
                    r = await CommandRouter.Dispatch(item.Command, item.Parameters, item.Context);
                }
                item.Tcs.TrySetResult(r);
            }
            catch (Exception ex)
            {
                item.Tcs.TrySetResult(new ErrorResponse("INTERNAL_ERROR", $"Request handling error: {ex.Message}"));
            }
            finally
            {
                Interlocked.Decrement(ref s_PendingRequests);
            }
        }

        static async Task ListenLoop(HttpListener listener, CancellationToken ct)
        {
            while (ct.IsCancellationRequested == false && listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    _ = HandleRequest(context).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            Debug.LogError($"[Hera] HandleRequest faulted: {t.Exception?.InnerException ?? t.Exception}");
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
            }
        }

        static async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            response.ContentType = "application/json; charset=utf-8";

            // Block browser cross-origin requests — CLI uses Go HTTP client (not subject to CORS)
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            var origin = request.Headers["Origin"];
            if (origin != null)
            {
                response.StatusCode = 403;
                var buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                    new ErrorResponse("HTTP_BROWSER_REQUEST_FORBIDDEN", "Browser requests are not allowed.")));
                response.ContentLength64 = buf.Length;
                await response.OutputStream.WriteAsync(buf, 0, buf.Length);
                response.Close();
                return;
            }

            object result;

            try
            {
                if (request.HttpMethod != "POST")
                {
                    result = new ErrorResponse("HTTP_METHOD_NOT_ALLOWED", $"Expected POST, got {request.HttpMethod} {request.Url.AbsolutePath}");
                }
                else
                {
                    switch (request.Url.AbsolutePath)
                    {
                        case "/command":
                            result = await HandleSingleCommand(request);
                            break;
                        case "/commands":
                            result = await HandleBatchCommand(request);
                            break;
                        case "/approval/preflight":
                            result = await HandleApprovalPreflight(request);
                            break;
                        default:
                            result = new ErrorResponse("HTTP_NOT_FOUND", $"Expected POST /command, POST /commands, or POST /approval/preflight, got {request.HttpMethod} {request.Url.AbsolutePath}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                result = new ErrorResponse("HTTP_INTERNAL_ERROR", $"Request error: {ex.Message}");
                response.StatusCode = 500;
                DebugLogging.LogError("unknown", ex);
            }

            string operationId = null;
            if (result is CommandHttpResult commandResult)
            {
                operationId = commandResult.OperationId;
                result = commandResult.Payload;
            }

            if (result is ErrorResponse error)
                response.StatusCode = StatusCodeFor(error.code);

            try
            {
                var responseJson = JsonConvert.SerializeObject(result);
                var buffer = Encoding.UTF8.GetBytes(responseJson);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                if (!string.IsNullOrEmpty(operationId))
                    OperationLedger.Default.MarkResponded(operationId);
            }
            catch (Exception ex)
            {
                // A non-serializable result graph, or a client that disconnected
                // mid-write, would otherwise fault this task and skip Close(),
                // leaving the CLI blocked until its own timeout instead of getting
                // an error. Best-effort emit a 500, then always close the response.
                DebugLogging.LogError("response-write", ex);
                TryWriteError(response);
            }
            finally
            {
                try { response.Close(); } catch { }
                MaybeCleanupLedger();
            }
        }

        sealed class CommandHttpResult
        {
            internal string OperationId;
            internal object Payload;
        }

        // Best-effort 500 when the normal response could not be serialized or
        // sent. Silently gives up if headers were already flushed or the client
        // is gone — the finally-block Close() is what actually unblocks the CLI.
        static void TryWriteError(HttpListenerResponse response)
        {
            try
            {
                if (!response.OutputStream.CanWrite) return;
                response.StatusCode = 500;
                var buf = Encoding.UTF8.GetBytes(
                    "{\"success\":false,\"code\":\"RESPONSE_WRITE_FAILED\",\"message\":\"[Hera] I built a response I couldn't serialize or send.\"}");
                response.ContentLength64 = buf.Length;
                response.OutputStream.Write(buf, 0, buf.Length);
            }
            catch { }
        }

        static async Task<object> HandleSingleCommand(HttpListenerRequest request)
        {
            var (body, bodyError) = await ReadBody(request, MAX_COMMAND_BODY_BYTES);
            if (bodyError != null) return bodyError;
            var (json, jsonError) = ParseRequestObject(body);
            if (jsonError != null) return jsonError;

            var command = json["command"]?.ToString();
            var parameters = json["params"] as JObject;

            DebugLogging.LogRequest(command, parameters);

            if (string.IsNullOrEmpty(command))
            {
                DebugLogging.LogError("unknown", new Exception("Missing 'command' field"));
                return new ErrorResponse("HTTP_MISSING_COMMAND", "Missing 'command' field");
            }
            if (!CommandRequestContext.TryCreate(
                json["meta"] as JObject,
                parameters,
                out var requestContext,
                out var contextError))
            {
                return contextError;
            }

            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var queueError = Enqueue(new WorkItem
            {
                Command = command,
                Parameters = parameters,
                Context = requestContext,
                Tcs = tcs,
            });
            if (queueError != null) return queueError;
            var result = await tcs.Task;
            DebugLogging.LogResponse(command, result);
            return new CommandHttpResult
            {
                OperationId = requestContext.OperationId,
                Payload = result,
            };
        }

        static async Task<object> HandleApprovalPreflight(HttpListenerRequest request)
        {
            var (body, bodyError) = await ReadBody(request, MAX_COMMAND_BODY_BYTES);
            if (bodyError != null) return bodyError;
            var (json, jsonError) = ParseRequestObject(body);
            if (jsonError != null) return jsonError;
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var queueError = Enqueue(new WorkItem
            {
                ApprovalRequest = json,
                Tcs = tcs,
            });
            if (queueError != null) return queueError;
            return await tcs.Task;
        }

        static void MaybeCleanupLedger()
        {
            var now = DateTimeOffset.UtcNow;
            var nowMs = now.ToUnixTimeMilliseconds();
            var previous = Interlocked.Read(ref s_LastLedgerCleanupMs);
            if (nowMs - previous < TimeSpan.FromMinutes(5).TotalMilliseconds)
                return;
            if (Interlocked.CompareExchange(ref s_LastLedgerCleanupMs, nowMs, previous) != previous)
                return;
            try { OperationLedger.Default.Cleanup(now); }
            catch (Exception ex) { DebugLogging.LogError("operation-ledger-cleanup", ex); }
        }

        static async Task<object> HandleBatchCommand(HttpListenerRequest request)
        {
            var (body, bodyError) = await ReadBody(request, MAX_BATCH_BODY_BYTES);
            if (bodyError != null) return bodyError;
            var (json, jsonError) = ParseRequestObject(body);
            if (jsonError != null) return jsonError;

            var commandsArray = json["commands"] as JArray;
            if (commandsArray == null)
            {
                return new ErrorResponse("HTTP_MISSING_COMMANDS", "Missing 'commands' field");
            }
            if (commandsArray.Count > MAX_BATCH_COMMANDS)
                return new ErrorResponse("HTTP_BATCH_TOO_LARGE", $"Batch contains {commandsArray.Count} commands; maximum is {MAX_BATCH_COMMANDS}.");

            var items = new List<CommandRouter.BatchCommandItem>();
            foreach (var cmd in commandsArray)
            {
                if (!(cmd is JObject commandObject))
                    return new ErrorResponse("HTTP_INVALID_JSON", "Each batch command must be a JSON object.");
                items.Add(new CommandRouter.BatchCommandItem
                {
                    Command = commandObject["command"]?.ToString(),
                    Params = commandObject["params"] as JObject,
                });
            }

            var optionsObj = json["options"] as JObject;
            var options = new CommandRouter.BatchOptions
            {
                FailFast = optionsObj?["fail_fast"]?.Value<bool>() ?? true,
                Atomic = optionsObj?["atomic"]?.Value<bool>() ?? false,
            };

            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var queueError = Enqueue(new WorkItem
            {
                IsBatch = true,
                BatchItems = items,
                BatchOptions = options,
                Tcs = tcs,
            });
            if (queueError != null) return queueError;
            return await tcs.Task;
        }

        static ErrorResponse Enqueue(WorkItem item)
        {
            if (Interlocked.Increment(ref s_PendingRequests) > MAX_PENDING_REQUESTS)
            {
                Interlocked.Decrement(ref s_PendingRequests);
                return new ErrorResponse("HTTP_QUEUE_FULL", $"Too many pending requests; maximum is {MAX_PENDING_REQUESTS}.");
            }

            s_Queue.Enqueue(item);
            ForceEditorUpdate();
            return null;
        }

        static async Task<(string body, ErrorResponse error)> ReadBody(HttpListenerRequest request, int maximumBytes)
        {
            if (request.ContentLength64 > maximumBytes)
                return (null, new ErrorResponse("HTTP_REQUEST_BODY_TOO_LARGE", $"Request body exceeds {maximumBytes} bytes."));

            var buffer = new byte[8192];
            var total = 0;
            using var output = new MemoryStream();
            while (true)
            {
                var read = await request.InputStream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) break;
                total += read;
                if (total > maximumBytes)
                    return (null, new ErrorResponse("HTTP_REQUEST_BODY_TOO_LARGE", $"Request body exceeds {maximumBytes} bytes."));
                output.Write(buffer, 0, read);
            }
            return (Encoding.UTF8.GetString(output.ToArray()), null);
        }

        static (JObject json, ErrorResponse error) ParseRequestObject(string body)
        {
            try
            {
                return (JObject.Parse(body), null);
            }
            catch (JsonException)
            {
                return (null, new ErrorResponse("HTTP_INVALID_JSON", "Request body must be a JSON object."));
            }
        }

        static int StatusCodeFor(string code)
        {
            switch (code)
            {
                case "HTTP_BROWSER_REQUEST_FORBIDDEN": return 403;
                case "HTTP_NOT_FOUND": return 404;
                case "HTTP_METHOD_NOT_ALLOWED": return 405;
                case "HTTP_REQUEST_BODY_TOO_LARGE": return 413;
                case "HTTP_QUEUE_FULL": return 429;
                case "HTTP_INTERNAL_ERROR": return 500;
                case "HTTP_INVALID_JSON":
                case "HTTP_MISSING_COMMAND":
                case "HTTP_MISSING_COMMANDS":
                case "HTTP_BATCH_TOO_LARGE": return 400;
                default: return 200;
            }
        }
    }
}
