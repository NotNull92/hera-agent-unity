using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HeraAgent.Tests
{
    internal static class ToolCatalogTestSupport
    {
        internal static SuccessResponse DispatchList(JObject parameters)
        {
            return CommandRouter.Dispatch("list", parameters)
                .GetAwaiter()
                .GetResult() as SuccessResponse;
        }

        internal static string SerializeData(SuccessResponse response)
        {
            return response == null
                ? ""
                : JToken.FromObject(response.data).ToString(Formatting.None);
        }

        internal static bool IsSha256(string value)
        {
            const string prefix = "sha256:";
            return value != null
                && value.Length == prefix.Length + 64
                && value.StartsWith(prefix, StringComparison.Ordinal)
                && value.Substring(prefix.Length).All(character =>
                    character >= '0' && character <= '9'
                    || character >= 'a' && character <= 'f');
        }
    }

    [HeraTool(
        Name = "m4_legacy_custom_fixture",
        Enabled = false,
        ContractMode = ToolContractMode.Legacy)]
    internal sealed class ToolCatalogLegacyCustomFixture
    {
        public static object Ping(JObject _)
        {
            return null;
        }
    }
}
