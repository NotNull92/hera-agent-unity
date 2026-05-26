# Custom Tool Development Guide

This guide explains how to add new C# tools to Unity that can be called from the CLI.

---

## Minimal Example

Create a C# class in `AgentConnector/Editor/Tools/` (or anywhere in an Editor assembly):

```csharp
using Newtonsoft.Json.Linq;
using HeraAgent;

[HeraTool(Name = "spawn_cube", Description = "Spawns a cube at the specified position")]
public static class SpawnCubeTool
{
    public class Parameters
    {
        [ToolParameter("X position", Required = true)]
        public float X { get; set; }

        [ToolParameter("Y position", Required = true)]
        public float Y { get; set; }

        [ToolParameter("Z position", Required = true)]
        public float Z { get; set; }

        [ToolParameter("Object name", Default = "Cube")]
        public string Name { get; set; }
    }

    public static object HandleCommand(JObject @params)
    {
        var p = new ToolParams(@params);

        var x = p.GetFloat("x");
        var y = p.GetFloat("y");
        var z = p.GetFloat("z");
        var name = p.GetString("name", "Cube");

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = new UnityEngine.Vector3(x, y, z);

        return new SuccessResponse($"Spawned {name} at ({x}, {y}, {z})");
    }
}
```

That's it. The tool is automatically discovered and callable via:

```bash
hera-agent-unity-unity spawn_cube --params '{"x":0,"y":1,"z":0,"name":"MyCube"}'
```

---

## Tool Patterns

### Pattern 1: Static Class (Recommended for stateless tools)

```csharp
[HeraTool(Name = "my_tool")]
public static class MyTool
{
    public static object HandleCommand(JObject @params) { ... }
}
```

### Pattern 2: Instance Class (For stateful tools)

```csharp
[HeraTool(Name = "stateful_tool")]
public class StatefulTool : IHeraTool
{
    private int _counter;

    public object HandleCommand(JObject @params)
    {
        _counter++;
        return new SuccessResponse($"Called {_counter} times");
    }
}
```

The `CommandRouter` creates an instance via `Activator.CreateInstance()` for each call.

### Pattern 3: Async Tool

```csharp
[HeraTool(Name = "async_tool")]
public static class AsyncTool
{
    public static async Task<object> HandleCommand(JObject @params)
    {
        await Task.Delay(1000);
        return new SuccessResponse("Done after 1 second");
    }
}
```

`CommandRouter` awaits `Task<object>` and `Task` results automatically.

---

## Attributes

### HeraToolAttribute (`[HeraTool]`)

```csharp
[HeraTool(
    Name = "tool_name",           // Command name (snake_case recommended)
    Description = "What it does",  // Shown in list output
    Group = "Editor",              // Optional grouping
    Enabled = true                 // Can be disabled to hide from discovery
)]
```

### ToolParameterAttribute

```csharp
public class Parameters
{
    [ToolParameter("Description", Required = true, Default = "default_value")]
    public string MyParam { get; set; }
}
```

| Property | Description |
|:---|:---|
| `Description` | Human-readable parameter description |
| `Required` | Whether the parameter must be provided |
| `Default` | Default value if not provided |
| `EnumType` | Name of enum type for enum parameters |
| `OutputSchema` | JSON schema for the tool's output |

---

## Parameter Access (ToolParams)

`ToolParams` provides typed access to the incoming `JObject`:

```csharp
var p = new ToolParams(@params);

p.GetRequired("key")           // Returns Result<string>, fails if missing
p.GetString("key", "default")  // Returns string, uses default if missing
p.GetInt("key", 0)             // Returns int
p.GetFloat("key", 0f)          // Returns float
p.GetBool("key", false)        // Returns bool
p.GetStringArray("key")        // Returns string[]
```

Always use `GetRequired()` for mandatory parameters and return `ErrorResponse` on failure:

```csharp
var result = p.GetRequired("action");
if (!result.IsSuccess)
    return new ErrorResponse(result.ErrorMessage);
```

---

## Response Patterns

### Success with message

```csharp
return new SuccessResponse("Operation completed");
```

### Success with data

```csharp
return new SuccessResponse("Found objects", new { count = 5, names = new[] { "A", "B" } });
```

### Error

```csharp
return new ErrorResponse("Something went wrong");
```

### Raw object (auto-serialized)

```csharp
return new { success = true, value = 42 };
```

---

## Complete Example: Find Objects by Tag

```csharp
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using HeraAgent;

[HeraTool(
    Name = "find_by_tag",
    Description = "Finds all GameObjects with the specified tag"
)]
public static class FindByTagTool
{
    public class Parameters
    {
        [ToolParameter("Tag to search for", Required = true)]
        public string Tag { get; set; }
    }

    public static object HandleCommand(JObject @params)
    {
        var p = new ToolParams(@params);

        var tagResult = p.GetRequired("tag");
        if (!tagResult.IsSuccess)
            return new ErrorResponse(tagResult.ErrorMessage);

        var objects = GameObject.FindGameObjectsWithTag(tagResult.Value);
        var names = objects.Select(o => o.name).ToArray();

        return new SuccessResponse(
            $"Found {names.Length} objects with tag '{tagResult.Value}'",
            new { count = names.Length, names }
        );
    }
}
```

Call it:

```bash
hera-agent-unity-unity find_by_tag --params '{"tag":"Enemy"}'
```

---

## Testing Custom Tools

1. Save the C# file in Unity
2. Wait for compilation (domain reload)
3. Verify discovery: `hera-agent-unity-unity list | grep my_tool`
4. Test execution: `hera-agent-unity-unity my_tool --params '{"key":"value"}'`
5. Check Unity Console for any errors

---

## Tool Discovery Rules

1. Class must have `[HeraTool]` attribute
2. Must have a `HandleCommand` method with signature:
   - `static object HandleCommand(JObject params)` (static)
   - `object HandleCommand(JObject params)` (instance)
   - `static Task<object> HandleCommand(JObject params)` (async static)
   - `Task<object> HandleCommand(JObject params)` (async instance)
3. Tool name = `Name` attribute property, or `StringCaseUtility.ToSnakeCase(className)`
4. Duplicate names are logged as errors; first found wins

---

## Related Documentation

- [`CSHARP_CONNECTOR.md`](CSHARP_CONNECTOR.md) — C# connector internals
- [`COMMANDS.md`](COMMANDS.md) — Command reference
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — System architecture
