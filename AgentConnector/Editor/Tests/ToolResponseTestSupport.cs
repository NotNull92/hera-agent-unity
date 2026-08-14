using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HeraAgent.Tests
{
    internal static class ToolResponseTestSupport
    {
        internal static JObject RequireSuccess(object response)
        {
            var success = response as SuccessResponse;
            Assert.IsNotNull(success, "Expected success, got: " + JObject.FromObject(response));
            return success.data == null ? new JObject() : JObject.FromObject(success.data);
        }

        internal static void RequireError(object response, string code)
        {
            var error = response as ErrorResponse;
            Assert.IsNotNull(error, "Expected error " + code + ", got: " + JObject.FromObject(response));
            Assert.AreEqual(code, error.code);
        }
    }
}
