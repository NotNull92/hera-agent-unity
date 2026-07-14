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

        static HttpListener s_Listener;
        static CancellationTokenSource s_Cts;
        static int s_Port;

        static readonly ConcurrentQueue<WorkItem> s_Queue = new();
        static int s_PendingRequests;

        struct WorkItem
        {
            public string Command;
            public JObject Parameters;
            public TaskCompletionSource<object> Tcs;
            // Batch-specific fields (set when POST /commands is received).
            public bool IsBatch;
            public List<CommandRouter.BatchCommandItem> BatchItems;
            public CommandRouter.BatchOptions BatchOptions;
        }

        static HttpServer()
        {
            Start();
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += StopListener;
            AssemblyReloadEvents.afterAssemblyReload += Start;
            EditorApplication.update += ProcessQueue;
        }

        public static int Port => s_Port;

        static void Start()
        {
            if (s_Listener != null) return;

            for (var attempt = 0; attempt < MAX_PORT_ATTEMPTS; attempt++)
            {
                var port = attempt == 0 ? DEFAULT_PORT : FALLBACK_PORT + attempt - 1;
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    listener.Start();

                    s_Listener = listener;
                    s_Port = port;
                    s_Cts = new CancellationTokenSource();

                    _ = ListenLoop(s_Cts.Token).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            Debug.LogError($"[Hera] ListenLoop faulted: {t.Exception?.InnerException ?? t.Exception}");
                    }, TaskContinuationOptions.OnlyOnFaulted);

                    Debug.Log($"[Hera] HTTP server started on port {port}");
                    // Defer compiler pre-warm so editor startup is not blocked by a
                    // potentially slow csc invocation.
                    EditorApplication.delayCall += () => ExecuteCsharp.PreWarmCompiler();
                    return;
                }
                catch (HttpListenerException)
                {
                    // Port in use, try next
                }
                catch (System.Net.Sockets.SocketException)
                {
                    // Windows/Mono throws SocketException instead of HttpListenerException
                }
            }

            Debug.LogError("[Hera] Failed to start HTTP server — no available port");
        }

        static void StopListener()
        {
            if (s_Listener == null) return;

            s_Cts?.Cancel();
            s_Cts?.Dispose();
            s_Cts = null;

            try
            {
                s_Listener.Stop();
                s_Listener.Close();
            }
            catch
            {
            }

            s_Listener = null;
        }

        static void Stop()
        {
            var port = s_Port;
            StopListener();
            Debug.Log($"[Hera] HTTP server stopped (was port {port})");
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
                if (item.IsBatch)
                {
                    r = await CommandRouter.DispatchBatch(item.BatchItems, item.BatchOptions);
                }
                else
                {
                    r = await CommandRouter.Dispatch(item.Command, item.Parameters);
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

        static async Task ListenLoop(CancellationToken ct)
        {
            while (ct.IsCancellationRequested == false && s_Listener?.IsListening == true)
            {
                try
                {
                    var context = await s_Listener.GetContextAsync();
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
                        default:
                            result = new ErrorResponse("HTTP_NOT_FOUND", $"Expected POST /command or POST /commands, got {request.HttpMethod} {request.Url.AbsolutePath}");
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

            if (result is ErrorResponse error)
                response.StatusCode = StatusCodeFor(error.code);

            try
            {
                var responseJson = JsonConvert.SerializeObject(result);
                var buffer = Encoding.UTF8.GetBytes(responseJson);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
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
            }
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

            var tcs = new TaskCompletionSource<object>();
            var queueError = Enqueue(new WorkItem
            {
                Command = command,
                Parameters = parameters,
                Tcs = tcs,
            });
            if (queueError != null) return queueError;
            var result = await tcs.Task;
            DebugLogging.LogResponse(command, result);
            return result;
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

            var tcs = new TaskCompletionSource<object>();
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
