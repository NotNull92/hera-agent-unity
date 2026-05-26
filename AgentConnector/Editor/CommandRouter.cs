using System;
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

        public static async Task<object> Dispatch(string command, JObject parameters)
        {
            await s_Lock.WaitAsync();
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

        static async Task<object> DispatchInternal(string command, JObject parameters)
        {
            if (command == "list")
                return HandleList(parameters);

            var handler = ToolDiscovery.FindHandler(command);
            if (handler == null)
                return new ErrorResponse("UNKNOWN_COMMAND",
                    $"Unknown command: {command}",
                    suggestions: new System.Collections.Generic.List<string>
                    {
                        "Run 'hera-agent-unity list --names' to see all tools"
                    });

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

                    // Create instance (must have parameterless constructor)
                    var instance = Activator.CreateInstance(toolType);
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
