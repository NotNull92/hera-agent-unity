using Newtonsoft.Json.Linq;

namespace HeraAgent.Tools
{
    public static partial class InputTool
    {
        private const string SequenceStepsSchema =
            "{\"type\":\"array\",\"minItems\":1,\"maxItems\":32,\"items\":{\"oneOf\":[" +
            "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"action\",\"key\"],\"properties\":{" +
            "\"action\":{\"type\":\"string\",\"enum\":[\"keyboard\"]}," +
            "\"backend\":{\"type\":\"string\",\"enum\":[\"inputsystem\",\"auto\"]}," +
            "\"key\":{\"type\":\"string\",\"pattern\":\"\\\\S\"}," +
            "\"mode\":{\"type\":\"string\",\"enum\":[\"press\",\"down\",\"up\"]}," +
            "\"hold_ms\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":5000}," +
            "\"settle_frames\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":120}}}," +
            "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"action\"],\"properties\":{" +
            "\"action\":{\"type\":\"string\",\"enum\":[\"mouse\"]}," +
            "\"backend\":{\"type\":\"string\",\"enum\":[\"inputsystem\",\"auto\"]}," +
            "\"mode\":{\"type\":\"string\",\"enum\":[\"move\",\"click\",\"down\",\"up\",\"delta\",\"scroll\"]}," +
            "\"button\":{\"type\":\"string\",\"enum\":[\"left\",\"right\",\"middle\"]}," +
            "\"position\":" + Vector2Schema + "," +
            "\"delta\":" + Vector2Schema + "," +
            "\"scroll_delta\":" + Vector2Schema + "," +
            "\"hold_ms\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":5000}," +
            "\"settle_frames\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":120}}}]}}";

        public sealed class SequenceParameters
        {
            [ToolParameter(
                "Ordered Input System keyboard/mouse steps.",
                Required = true,
                SchemaJson = SequenceStepsSchema)]
            public JArray Steps { get; set; }
        }
    }
}
