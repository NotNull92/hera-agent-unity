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

        static HttpListener s_Listener;
        static CancellationTokenSource s_Cts;
        static int s_Port;

        static readonly ConcurrentQueue<WorkItem> s_Queue = new();

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
            try { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
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
                item.Tcs.TrySetResult(new ErrorResponse(ex.Message));
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
                var buf = Encoding.UTF8.GetBytes("{\"error\":\"Browser requests are not allowed\"}");
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
                    result = new ErrorResponse($"Expected POST, got {request.HttpMethod} {request.Url.AbsolutePath}");
                    response.StatusCode = 400;
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
                            result = new ErrorResponse($"Expected POST /command or POST /commands, got {request.HttpMethod} {request.Url.AbsolutePath}");
                            response.StatusCode = 400;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                result = new ErrorResponse($"Request error: {ex.Message}");
                response.StatusCode = 500;
                DebugLogging.LogError("unknown", ex);
            }

            var responseJson = JsonConvert.SerializeObject(result);
            var buffer = Encoding.UTF8.GetBytes(responseJson);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        static async Task<object> HandleSingleCommand(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var json = JObject.Parse(body);

            var command = json["command"]?.ToString();
            var parameters = json["params"] as JObject;

            DebugLogging.LogRequest(command, parameters);

            if (string.IsNullOrEmpty(command))
            {
                DebugLogging.LogError("unknown", new Exception("Missing 'command' field"));
                return new ErrorResponse("Missing 'command' field");
            }

            var tcs = new TaskCompletionSource<object>();
            s_Queue.Enqueue(new WorkItem
            {
                Command = command,
                Parameters = parameters,
                Tcs = tcs,
            });
            ForceEditorUpdate();
            var result = await tcs.Task;
            DebugLogging.LogResponse(command, result);
            return result;
        }

        static async Task<object> HandleBatchCommand(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var json = JObject.Parse(body);

            var commandsArray = json["commands"] as JArray;
            if (commandsArray == null)
            {
                return new ErrorResponse("Missing 'commands' field");
            }

            var items = new List<CommandRouter.BatchCommandItem>();
            foreach (var cmd in commandsArray)
            {
                items.Add(new CommandRouter.BatchCommandItem
                {
                    Command = cmd["command"]?.ToString(),
                    Params = cmd["params"] as JObject,
                });
            }

            var optionsObj = json["options"] as JObject;
            var options = new CommandRouter.BatchOptions
            {
                FailFast = optionsObj?["fail_fast"]?.Value<bool>() ?? true,
            };

            var tcs = new TaskCompletionSource<object>();
            s_Queue.Enqueue(new WorkItem
            {
                IsBatch = true,
                BatchItems = items,
                BatchOptions = options,
                Tcs = tcs,
            });
            ForceEditorUpdate();
            return await tcs.Task;
        }
    }
}
