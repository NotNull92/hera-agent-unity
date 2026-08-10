namespace HeraAgent.Tools
{
    public static partial class InputTool
    {
        public sealed class RecordParameters
        {
            [ToolParameter(
                "Recording mode.",
                Required = true,
                SchemaJson = "{\"type\":\"string\",\"enum\":[\"start\",\"stop\",\"status\"]}")]
            public string Mode { get; set; }

            [ToolParameter(
                "New .json output path under the project or system temp directory. Start only.")]
            public string Path { get; set; }
        }

        public sealed class ReplayParameters
        {
            [ToolParameter(
                "Existing hera.input-recording/1 .json path under the project or system temp directory.",
                Required = true)]
            public string Path { get; set; }
        }
    }
}
