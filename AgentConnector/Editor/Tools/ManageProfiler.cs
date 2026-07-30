using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "profiler",
        Description = "Control Unity Profiler. Actions: hierarchy, enable, disable, status, clear.",
        Profiles = new[] { "diagnostics", "testing" },
        RiskClass = HeraRiskClass.Destructive,
        ContractMode = ToolContractMode.Strict)]
    [HeraActionContract(
        "hierarchy",
        typeof(ManageProfiler.HierarchyParameters),
        ResultType = typeof(ManageProfiler.Result),
        RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("enable", typeof(ManageProfiler.EmptyParameters), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract("disable", typeof(ManageProfiler.EmptyParameters), RiskClass = HeraRiskClass.Write)]
    [HeraActionContract(
        "status",
        typeof(ManageProfiler.EmptyParameters),
        ResultType = typeof(ManageProfiler.StatusResult),
        RiskClass = HeraRiskClass.ReadOnly)]
    [HeraActionContract("clear", typeof(ManageProfiler.EmptyParameters), RiskClass = HeraRiskClass.Destructive)]
    [HeraArgumentGroup(
        ToolArgumentGroupMode.AtMostOne,
        "frame",
        "frames",
        Action = "hierarchy")]
    [HeraArgumentGroup(
        ToolArgumentGroupMode.AtMostOne,
        "frame",
        "from",
        Action = "hierarchy")]
    [HeraArgumentGroup(
        ToolArgumentGroupMode.AtMostOne,
        "frame",
        "to",
        Action = "hierarchy")]
    [HeraArgumentGroup(
        ToolArgumentGroupMode.AtMostOne,
        "frames",
        "from",
        Action = "hierarchy")]
    [HeraArgumentGroup(
        ToolArgumentGroupMode.AtMostOne,
        "frames",
        "to",
        Action = "hierarchy")]
    [HeraArgumentGroup(
        ToolArgumentGroupMode.AtMostOne,
        "root",
        "parent",
        Action = "hierarchy")]
    [HeraArgumentGroup(ToolArgumentGroupMode.AtMostOne, "frame", "frames")]
    [HeraArgumentGroup(ToolArgumentGroupMode.AtMostOne, "frame", "from")]
    [HeraArgumentGroup(ToolArgumentGroupMode.AtMostOne, "frame", "to")]
    [HeraArgumentGroup(ToolArgumentGroupMode.AtMostOne, "frames", "from")]
    [HeraArgumentGroup(ToolArgumentGroupMode.AtMostOne, "frames", "to")]
    [HeraArgumentGroup(ToolArgumentGroupMode.AtMostOne, "root", "parent")]
    public static class ManageProfiler
    {
        public sealed class EmptyParameters
        {
        }

        public class HierarchyParameters
        {
            [ToolParameter(
                "Frame index. -1 or omit = last captured frame.",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":-1}")]
            public int Frame { get; set; }

            [ToolParameter(
                "Start frame index for range average.",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":-1}")]
            public int From { get; set; }

            [ToolParameter(
                "End frame index for range average.",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":-1}")]
            public int To { get; set; }

            [ToolParameter(
                "Number of recent frames to average (shortcut for range).",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":0}")]
            public int Frames { get; set; }

            [ToolParameter(
                "Thread index. 0 = main thread.",
                SchemaJson = "{\"type\":\"integer\",\"minimum\":0}")]
            public int Thread { get; set; }

            [ToolParameter("Parent item ID to drill into. Omit for root level.")]
            public int Parent { get; set; }

            [ToolParameter("Find item by name and use as root. Substring match.")]
            public string Root { get; set; }

            [ToolParameter(
                "Minimum total time (ms) filter.",
                SchemaJson = "{\"type\":\"number\",\"minimum\":0}")]
            public float Min { get; set; }

            [ToolParameter(
                "Sort column: 'total', 'self', or 'calls'. Default 'total'.",
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"total\",\"self\",\"calls\"]}")]
            public string Sort { get; set; }

            [ToolParameter("Max children per level. Default 30.")]
            public int Max { get; set; }

            [ToolParameter("Recursive depth. 1 = one level (default), 0 = unlimited.")]
            public int Depth { get; set; }
        }

        public sealed class Parameters : HierarchyParameters
        {
            [ToolParameter(
                "Action: hierarchy, enable, disable, status, or clear.",
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"hierarchy\",\"enable\",\"disable\",\"status\",\"clear\"]}")]
            public string Action { get; set; }
        }

        public sealed class StatusResult
        {
            public bool Enabled { get; set; }

            [JsonProperty("firstFrame")]
            public int FirstFrame { get; set; }

            [JsonProperty("lastFrame")]
            public int LastFrame { get; set; }

            [JsonProperty("frameCount")]
            public int FrameCount { get; set; }

            [JsonProperty("isPlaying")]
            public bool IsPlaying { get; set; }
        }

        public sealed class Result
        {
            public int Frame { get; set; }

            [JsonProperty("threadIndex")]
            public int ThreadIndex { get; set; }

            public int Parent { get; set; }

            [JsonProperty("parentName")]
            public string ParentName { get; set; }

            public int Depth { get; set; }
            public object[] Children { get; set; }

            [JsonProperty("frameCount")]
            public int FrameCount { get; set; }

            public string Root { get; set; }
            public object[] Items { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            var p = new ToolParams(parameters);
            var action = p.Get("action")?.ToLowerInvariant()
                ?? (p.GetRaw("args") as JArray)?[0]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                action = "hierarchy";

            switch (action)
            {
                case "hierarchy": return Hierarchy(p);
                case "enable":
                    UnityEngine.Profiling.Profiler.enabled = true;
                    ProfilerDriver.enabled = true;
                    return new SuccessResponse("Profiler enabled.");
                case "disable":
                    ProfilerDriver.enabled = false;
                    UnityEngine.Profiling.Profiler.enabled = false;
                    return new SuccessResponse("Profiler disabled.");
                case "status":
                    int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
                    return new SuccessResponse("Profiler status", new
                    {
                        enabled = ProfilerDriver.enabled,
                        firstFrame = first, lastFrame = last,
                        frameCount = last >= first ? last - first + 1 : 0,
                        isPlaying = Application.isPlaying
                    });
                case "clear":
                    ProfilerDriver.ClearAllFrames();
                    return new SuccessResponse("All profiler frames cleared.");
                default:
                    return new ErrorResponse("UNKNOWN_ACTION", $"Unknown action: '{action}'. Valid: hierarchy, enable, disable, status, clear.");
            }
        }

        private static object Hierarchy(ToolParams p)
        {
            if (ProfilerDriver.enabled == false && ProfilerDriver.lastFrameIndex < 0)
                return new ErrorResponse("PROFILER_NO_DATA", "Profiler has no captured data. Enable profiler first.");

            var fromFrame = p.GetInt("from", -1).Value;
            var toFrame = p.GetInt("to", -1).Value;
            var framesCount = p.GetInt("frames", 0).Value;

            if (fromFrame >= 0 || toFrame >= 0)
            {
                if (fromFrame < 0) fromFrame = ProfilerDriver.firstFrameIndex;
                if (toFrame < 0) toFrame = ProfilerDriver.lastFrameIndex;
                return AveragedHierarchy(p, fromFrame, toFrame);
            }
            if (framesCount > 1)
                return AveragedHierarchy(p, ProfilerDriver.lastFrameIndex - framesCount + 1, ProfilerDriver.lastFrameIndex);

            var frameIndex = p.GetInt("frame", -1).Value;
            if (frameIndex < 0) frameIndex = ProfilerDriver.lastFrameIndex;
            if (frameIndex < ProfilerDriver.firstFrameIndex || frameIndex > ProfilerDriver.lastFrameIndex)
                return new ErrorResponse(
                    $"Frame {frameIndex} out of range [{ProfilerDriver.firstFrameIndex}..{ProfilerDriver.lastFrameIndex}]");

            var threadIndex = p.GetInt("thread", 0).Value;
            var parentIdToken = p.GetRaw("parent");
            var rootName = p.Get("root");
            var minTime = p.GetFloat("min", 0f).Value;
            var sortBy = (p.Get("sort", "total")).ToLowerInvariant();
            var maxItems = p.GetInt("max", 30).Value;
            if (maxItems <= 0) maxItems = 30;
            var depth = p.GetInt("depth", 1).Value;
            if (depth <= 0) depth = 999;

            int sortColumn = GetSortColumn(sortBy);

            using var frameData = ProfilerDriver.GetHierarchyFrameDataView(
                frameIndex, threadIndex,
                HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                sortColumn, false);

            if (frameData == null || frameData.valid == false)
                return new ErrorResponse("PROFILER_NO_FRAME_DATA", $"No profiler data for frame {frameIndex}, thread {threadIndex}.");

            // Must traverse from root first — Unity lazy-initializes the hierarchy tree.
            int rootId = frameData.GetRootItemID();
            var rootChildIds = new List<int>();
            frameData.GetItemChildren(rootId, rootChildIds);

            int parentId = rootId;
            string parentName = "(root)";

            // --root: find by name
            if (!string.IsNullOrEmpty(rootName))
            {
                int found = FindItemByName(frameData, rootId, rootName);
                if (found < 0)
                    return new ErrorResponse("PROFILER_ITEM_NOT_FOUND", $"No profiler item matching '{rootName}' found.");
                parentId = found;
                parentName = frameData.GetItemName(found);
            }
            // --parent: by ID
            else if (parentIdToken != null && parentIdToken.Type != JTokenType.Null)
            {
                parentId = parentIdToken.Value<int>();
                parentName = frameData.GetItemName(parentId);
            }

            var items = BuildChildren(frameData, parentId, minTime, maxItems, depth);

            var result = new JObject
            {
                ["frame"] = frameIndex,
                ["threadIndex"] = threadIndex,
                ["parent"] = parentId,
                ["parentName"] = parentName,
                ["depth"] = depth >= 999 ? 0 : depth,
                ["children"] = items,
            };

            return new SuccessResponse($"Hierarchy of '{parentName}' (frame {frameIndex})", result);
        }

        private static object AveragedHierarchy(ToolParams p, int fromFrame, int toFrame)
        {
            int firstAvail = ProfilerDriver.firstFrameIndex;
            int lastAvail = ProfilerDriver.lastFrameIndex;
            fromFrame = Math.Max(fromFrame, firstAvail);
            toFrame = Math.Min(toFrame, lastAvail);
            int frameCount = toFrame - fromFrame + 1;
            if (frameCount <= 0)
                return new ErrorResponse("PROFILER_NO_FRAMES_IN_RANGE", $"No frames in range [{fromFrame}..{toFrame}]. Available: [{firstAvail}..{lastAvail}].");

            var threadIndex = p.GetInt("thread", 0).Value;
            var rootName = p.Get("root");
            var minTime = p.GetFloat("min", 0f).Value;
            var sortBy = (p.Get("sort", "total")).ToLowerInvariant();
            var maxItems = p.GetInt("max", 30).Value;
            if (maxItems <= 0) maxItems = 30;
            var depth = p.GetInt("depth", 1).Value;
            if (depth <= 0) depth = 999;

            int sortColumn = GetSortColumn(sortBy);

            // Collect data across frames
            var accumulated = new Dictionary<string, (double totalMs, double selfMs, long calls, int count)>();

            for (int frameIndex = fromFrame; frameIndex <= toFrame; frameIndex++)
            {
                using var frameData = ProfilerDriver.GetHierarchyFrameDataView(
                    frameIndex, threadIndex,
                    HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                    sortColumn, false);

                if (frameData == null || frameData.valid == false) continue;

                int rootId = frameData.GetRootItemID();
                var rootChildIds = new List<int>();
                frameData.GetItemChildren(rootId, rootChildIds);

                int parentId = rootId;
                if (!string.IsNullOrEmpty(rootName))
                {
                    int found = FindItemByName(frameData, rootId, rootName);
                    if (found >= 0) parentId = found;
                }

                CollectFlat(frameData, parentId, depth, accumulated);
            }

            // Build averaged result
            var sorted = accumulated
                .Select(kv => new
                {
                    name = kv.Key,
                    avgTotalMs = Math.Round(kv.Value.totalMs / kv.Value.count, 3),
                    avgSelfMs = Math.Round(kv.Value.selfMs / kv.Value.count, 3),
                    avgCalls = Math.Round((double)kv.Value.calls / kv.Value.count, 1),
                    appearedIn = kv.Value.count,
                })
                .Where(x => x.avgTotalMs >= minTime)
                .OrderByDescending(x => sortBy == "self" ? x.avgSelfMs : x.avgTotalMs)
                .Take(maxItems)
                .ToList();

            string rootLabel = string.IsNullOrEmpty(rootName) ? "(root)" : rootName;

            return new SuccessResponse($"Averaged over {frameCount} frames", new
            {
                frameCount,
                threadIndex,
                root = rootLabel,
                items = sorted,
            });
        }

        private static void CollectFlat(HierarchyFrameDataView frameData, int parentId, int remainingDepth,
            Dictionary<string, (double totalMs, double selfMs, long calls, int count)> acc)
        {
            var childIds = new List<int>();
            frameData.GetItemChildren(parentId, childIds);

            foreach (var childId in childIds)
            {
                var name = frameData.GetItemName(childId);
                var totalMs = frameData.GetItemColumnDataAsFloat(childId, HierarchyFrameDataView.columnTotalTime);
                var selfMs = frameData.GetItemColumnDataAsFloat(childId, HierarchyFrameDataView.columnSelfTime);
                var calls = (long)frameData.GetItemColumnDataAsFloat(childId, HierarchyFrameDataView.columnCalls);

                if (acc.TryGetValue(name, out var existing))
                    acc[name] = (existing.totalMs + totalMs, existing.selfMs + selfMs, existing.calls + calls, existing.count + 1);
                else
                    acc[name] = (totalMs, selfMs, calls, 1);

                if (remainingDepth > 1)
                    CollectFlat(frameData, childId, remainingDepth - 1, acc);
            }
        }

        private static int FindItemByName(HierarchyFrameDataView frameData, int parentId, string name)
        {
            var childIds = new List<int>();
            frameData.GetItemChildren(parentId, childIds);

            foreach (var childId in childIds)
            {
                var itemName = frameData.GetItemName(childId);
                if (itemName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return childId;

                // Recurse to find nested items
                int found = FindItemByName(frameData, childId, name);
                if (found >= 0) return found;
            }

            return -1;
        }

        static int GetSortColumn(string sortBy)
        {
            switch (sortBy)
            {
                case "self": return HierarchyFrameDataView.columnSelfTime;
                case "calls": return HierarchyFrameDataView.columnCalls;
                default: return HierarchyFrameDataView.columnTotalTime;
            }
        }

        static JArray BuildChildren(HierarchyFrameDataView frameData, int parentId, float minTime, int maxItems, int remainingDepth)
        {
            var childIds = new List<int>();
            frameData.GetItemChildren(parentId, childIds);

            var items = new JArray();
            int shown = 0;
            foreach (var childId in childIds)
            {
                var totalTime = frameData.GetItemColumnDataAsFloat(childId, HierarchyFrameDataView.columnTotalTime);
                if (totalTime < minTime) continue;
                if (shown >= maxItems) break;
                shown++;

                var selfTime = frameData.GetItemColumnDataAsFloat(childId, HierarchyFrameDataView.columnSelfTime);
                var calls = (int)frameData.GetItemColumnDataAsFloat(childId, HierarchyFrameDataView.columnCalls);

                var item = new JObject
                {
                    ["itemId"] = childId,
                    ["name"] = frameData.GetItemName(childId),
                    ["totalMs"] = Math.Round(totalTime, 3),
                    ["selfMs"] = Math.Round(selfTime, 3),
                    ["calls"] = calls,
                };

                if (remainingDepth > 1)
                {
                    var subChildren = BuildChildren(frameData, childId, minTime, maxItems, remainingDepth - 1);
                    if (subChildren.Count > 0)
                        item["children"] = subChildren;
                }

                items.Add(item);
            }

            return items;
        }
    }
}
