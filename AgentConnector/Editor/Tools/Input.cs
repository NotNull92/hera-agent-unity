using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "input",
        Description = "Unity input QA: inspect and synthesize uGUI EventSystem input events without Computer Use coordinates.",
        RequiresPlayMode = false,
        Examples = new[]
        {
            "input state",
            "input inspect --path /Canvas/StartButton --details true",
            "input click --path /Canvas/StartButton --settle_frames 2",
            "input drag --path /Canvas/Slider/Handle --to_normalized 0.8,0.5",
            "input submit --path /Canvas/StartButton"
        },
        ExampleDescriptions = new[]
        {
            "Report EventSystem, raycaster, InputSystem, and native backend availability",
            "Raycast a target point and report blockers/handlers",
            "Drive pointer enter/down/up/click through Unity's EventSystem",
            "Drive begin/drag/end handlers from the target point to a target-local point",
            "Select the target and execute its submit handler"
        })]
    public static class InputTool
    {
        public class Parameters
        {
            [ToolParameter("Action: state, inspect, click, pointer_down, pointer_up, drag, scroll, submit. Can also be passed as first positional arg.", Required = true)]
            public string Action { get; set; }

            [ToolParameter("Backend: eventsystem or auto. InputSystem/native backends are planned but not phase-1 defaults.")]
            public string Backend { get; set; }

            [ToolParameter("Target by InstanceID.")]
            public int? InstanceId { get; set; }

            [ToolParameter("Target by hierarchy path '/Canvas/Child'.")]
            public string Path { get; set; }

            [ToolParameter("Target convenience: hierarchy path or InstanceID.")]
            public string Target { get; set; }

            [ToolParameter("Screen position 'x,y' in Unity bottom-left screen coordinates.")]
            public string Position { get; set; }

            [ToolParameter("Point inside target RectTransform as normalized 'x,y'.")]
            public string Normalized { get; set; }

            [ToolParameter("Point offset from target rect center in local UI pixels: 'x,y'.")]
            public string Offset { get; set; }

            [ToolParameter("scroll: scroll delta 'x,y'. Default 0,-1.")]
            public string ScrollDelta { get; set; }

            [ToolParameter("drag: end screen position 'x,y'. Alias: to.")]
            public string ToPosition { get; set; }

            [ToolParameter("drag: end point inside target RectTransform as normalized 'x,y'.")]
            public string ToNormalized { get; set; }

            [ToolParameter("Mouse button: left, right, middle. Default left.")]
            public string Button { get; set; }

            [ToolParameter("Pointer click count. Default 1.")]
            public int? ClickCount { get; set; }

            [ToolParameter("Delay between pointer down and up in milliseconds. Default 50.")]
            public int? HoldMs { get; set; }

            [ToolParameter("Editor updates to wait after the final event. Default 1.")]
            public int? SettleFrames { get; set; }

            [ToolParameter("drag: number of intermediate drag steps. Default 8.")]
            public int? Steps { get; set; }

            [ToolParameter("Maximum raycast or raycaster diagnostics to return. Default 50, maximum 100.")]
            public int? MaxResults { get; set; }

            [ToolParameter("Fail when the target is blocked or the expected click handler is not reached. Default true.")]
            public bool? Strict { get; set; }

            [ToolParameter("Return detailed event system, raycast, and handler diagnostics. Default false.")]
            public bool? Details { get; set; }
        }

        public static async Task<object> HandleCommand(JObject raw)
        {
            var (options, err) = InputQaResolver.Parse(raw);
            if (err != null) return err;

            if (options.Backend != "eventsystem" && options.Backend != "auto")
                return new ErrorResponse(
                    "INPUT_BACKEND_UNIMPLEMENTED",
                    $"Input backend '{options.Backend}' is not implemented in this connector build.");

            switch (options.Action)
            {
                case "state":
                    return InputQaEventSystem.State(options);
                case "inspect":
                    return InputQaEventSystem.Inspect(options);
                case "click":
                    return await InputQaEventSystem.Click(options);
                case "pointer_down":
                    return await InputQaEventSystem.PointerDown(options);
                case "pointer_up":
                    return await InputQaEventSystem.PointerUp(options);
                case "submit":
                    return await InputQaEventSystem.Submit(options);
                case "scroll":
                    return await InputQaEventSystem.Scroll(options);
                case "drag":
                    return await InputQaEventSystem.Drag(options);
                default:
                    return new ErrorResponse("INPUT_UNKNOWN_ACTION", $"Unknown input action: '{options.Action}'. Use state, inspect, click, pointer_down, pointer_up, submit, scroll, or drag.");
            }
        }
    }
}
