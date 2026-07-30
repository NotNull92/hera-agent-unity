using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal static class ToolContractCanonicalJson
    {
        internal static JObject Canonicalize(JObject value)
        {
            return value == null ? null : SchemaUtility.CanonicalizeSchema(value);
        }
    }
}
