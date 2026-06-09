using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HeraAgent
{
    /// <summary>
    /// Routes incoming command requests to the appropriate tool handler.
    /// All requests are serialized through a single queue to prevent
    /// race conditions when multiple CLI agents access the same Unity instance.
    /// </summary>
    public static class CommandRouter
    {
        static readonly SemaphoreSlim s_Lock = new(1, 1);

        // 120s is the lock-acquisition timeout, not the per-command execution
        // budget. Long-running operations (compile, profiler capture) are
        // handled separately via heartbeat polling on the CLI side. If a
        // command holds the lock longer than this, something is wedged and
        // the caller deserves a clear error instead of an indefinite hang.
        static readonly TimeSpan s_LockTimeout = TimeSpan.FromSeconds(120);

        public class BatchCommandItem
        {
            public string Command { get; set; }
            public JObject Params { get; set; }
        }

        public class BatchOptions
        {
            public bool FailFast { get; set; } = true;
        }

        public class BatchCommandResponse
        {
            public List<object> Results { get; set; }
            public int Completed { get; set; }
            public int Failed { get; set; }
        }

        public static async Task<object> Dispatch(string command, JObject parameters)
        {
            if (!await s_Lock.WaitAsync(s_LockTimeout))
            {
                return new ErrorResponse("[Hera] I waited 120s for the command lock but another command is still running.");
            }
            var sw = Stopwatch.StartNew();
            try
            {
                var result = await DispatchInternal(command, parameters);
                sw.Stop();
                ResponseTimings.Set(result, "total_ms", sw.ElapsedMilliseconds);
                return result;
            }
            finally
            {
                s_Lock.Release();
            }
        }

        /// <summary>
        /// Run multiple commands sequentially while holding the same lock so
        /// the batch is observed atomically from the CLI's perspective. The
        /// CLI side serialises requests anyway, but batching saves one HTTP
        /// round-trip per command and lets the editor avoid releasing /
        /// re-acquiring the work queue between steps.
        /// </summary>
        public static async Task<object> DispatchBatch(List<BatchCommandItem> commands, BatchOptions options)
        {
            if (!await s_Lock.WaitAsync(s_LockTimeout))
            {
                return new ErrorResponse("[Hera] I waited 120s for the command lock but another command is still running.");
            }

            var results = new List<object>();
            int failed = 0;

            try
            {
                foreach (var item in commands)
                {
                    var sw = Stopwatch.StartNew();
                    var result = await DispatchInternal(item.Command, item.Params);
                    sw.Stop();
                    ResponseTimings.Set(result, "total_ms", sw.ElapsedMilliseconds);
                    results.Add(result);

                    bool isError = result is ErrorResponse;
                    if (isError) failed++;

                    if (options.FailFast && isError)
                    {
                        break;
                    }
                }
            }
            finally
            {
                s_Lock.Release();
            }

            return new BatchCommandResponse
            {
                Results = results,
                Completed = results.Count,
                Failed = failed
            };
        }

        static string ExtractAction(JObject parameters)
        {
            if (parameters == null) return null;

            var action = parameters["action"]?.ToString();
            if (!string.IsNullOrEmpty(action))
                return action.ToLowerInvariant();

            var args = parameters["args"] as JArray;
            if (args != null && args.Count >= 1)
                return args[0].ToString().ToLowerInvariant();

            return null;
        }

        static async Task<object> DispatchInternal(string command, JObject parameters)
        {
            if (command == "list")
                return HandleList(parameters);

            // Try action-level dispatch first
            string action = ExtractAction(parameters);
            if (!string.IsNullOrEmpty(action))
            {
                var actionHandler = ToolDiscovery.FindActionHandler(command, action);
                if (actionHandler != null)
                {
                    try
                    {
                        object result;
                        if (actionHandler.IsStatic)
                        {
                            result = actionHandler.Invoke(null, new object[] { parameters ?? new JObject() });
                        }
                        else
                        {
                            var toolType = actionHandler.DeclaringType;
                            if (toolType == null)
                                return new ErrorResponse($"Tool type not found: {command}");
                            object instance;
                            try
                            {
                                instance = Activator.CreateInstance(toolType);
                            }
                            catch (MissingMethodException)
                            {
                                return new ErrorResponse($"Tool '{command}' requires a parameterless constructor");
                            }
                            catch (MemberAccessException)
                            {
                                return new ErrorResponse($"Tool '{command}' constructor is not accessible (must be public)");
                            }
                            if (instance == null)
                                return new ErrorResponse($"Failed to create tool instance: {command}");
                            result = actionHandler.Invoke(instance, new object[] { parameters ?? new JObject() });
                        }

                        if (result is Task<object> asyncTask)
                            return await asyncTask;
                        if (result is Task task)
                        {
                            await task;
                            return new SuccessResponse($"{command}:{action} completed");
                        }
                        return result ?? new SuccessResponse($"{command}:{action} completed");
                    }
                    catch (Exception ex)
                    {
                        var inner = ex.InnerException ?? ex;
                        UnityEngine.Debug.LogException(inner);
                        return new ErrorResponse($"{command}:{action} failed: {inner.Message}");
                    }
                }
            }

            // Fallback to legacy Handle/HandleCommand
            var handler = ToolDiscovery.FindHandler(command);
            if (handler == null)
            {
                var similar = ToolDiscovery.SuggestSimilarCommands(command);
                var suggestionList = new List<string>();
                foreach (var s in similar) suggestionList.Add($"Did you mean '{s}'?");
                suggestionList.Add("Run 'hera-agent-unity list --names' to see all tools");

                return new ErrorResponse(
                    code: "UNKNOWN_COMMAND",
                    message: $"Unknown command: {command}",
                    data: similar.Count > 0 ? new { did_you_mean = similar } : null,
                    suggestions: suggestionList);
            }

            try
            {
                object result;

                // Check if it's a static method (traditional tools)
                if (handler.IsStatic)
                {
                    result = handler.Invoke(null, new object[] { parameters ?? new JObject() });
                }
                else
                {
                    // It's an instance method (class-based tools) - create instance
                    var toolType = handler.DeclaringType;
                    if (toolType == null)
                    {
                        return new ErrorResponse($"Tool type not found: {command}");
                    }

                    // Create instance (must have parameterless constructor).
                    // The two reflection exceptions we care about have distinct
                    // operator-facing fixes — surface them rather than collapsing
                    // both into "Failed to create instance".
                    object instance;
                    try
                    {
                        instance = Activator.CreateInstance(toolType);
                    }
                    catch (MissingMethodException)
                    {
                        return new ErrorResponse($"Tool '{command}' requires a parameterless constructor");
                    }
                    catch (MemberAccessException)
                    {
                        return new ErrorResponse($"Tool '{command}' constructor is not accessible (must be public)");
                    }
                    if (instance == null)
                    {
                        return new ErrorResponse($"Failed to create tool instance: {command}");
                    }

                    result = handler.Invoke(instance, new object[] { parameters ?? new JObject() });
                }

                if (result is Task<object> asyncTask)
                    return await asyncTask;

                if (result is Task task)
                {
                    await task;
                    return new SuccessResponse($"{command} completed");
                }

                return result ?? new SuccessResponse($"{command} completed");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                UnityEngine.Debug.LogException(inner);
                return new ErrorResponse($"{command} failed: {inner.Message}");
            }
        }

        static object HandleList(JObject parameters)
        {
            var tool = parameters?["tool"]?.ToString();
            if (!string.IsNullOrEmpty(tool))
            {
                var schema = ToolDiscovery.GetToolSchema(tool);
                if (schema == null)
                    return new ErrorResponse("UNKNOWN_TOOL",
                        $"Tool not found: {tool}",
                        suggestions: new System.Collections.Generic.List<string>
                        {
                            "Run 'hera-agent-unity list --names' to see all tools"
                        });
                return new SuccessResponse($"Tool: {tool}", schema);
            }

            var namesOnly = parameters?["names"]?.Type == JTokenType.Boolean
                && parameters["names"].Value<bool>();
            if (namesOnly)
                return new SuccessResponse("Available tools", ToolDiscovery.GetToolNames());

            return new SuccessResponse("Available tools", ToolDiscovery.GetToolSchemas());
        }
    }
}
