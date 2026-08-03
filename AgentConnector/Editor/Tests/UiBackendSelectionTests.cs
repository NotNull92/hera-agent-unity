using HeraAgent;
using HeraAgent.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HeraAgent.Tests
{
    public sealed class UiBackendSelectionTests
    {
        [TestCase("ugui", "uitk", true)]
        [TestCase("uitk", "ugui", true)]
        [TestCase("ugui", "ugui", false)]
        [TestCase("uitk", "uitk", false)]
        [TestCase("ugui", null, false)]
        public void ValidateDocument_reports_mismatch_when_document_backend_differs_from_setting(
            string uiSystem,
            string documentBackend,
            bool expectsMismatch)
        {
            var document = new JObject();
            if (documentBackend != null) document["backend"] = documentBackend;

            var error = UiBackendSelection.ValidateDocument(uiSystem, document);

            Assert.That(error?.code == "UI_SYSTEM_MISMATCH", Is.EqualTo(expectsMismatch));
        }

        [TestCase("ugui", "uitk")]
        [TestCase("uitk", "ugui")]
        public void Apply_rejects_document_when_backend_differs_from_setting(
            string uiSystem,
            string documentBackend)
        {
            var response = UiDoc.ApplyForUiSystem(
                new JObject
                {
                    ["doc"] = new JObject
                    {
                        ["schema"] = UiDocSchema.SchemaId,
                        ["backend"] = documentBackend,
                    },
                },
                uiSystem);

            Assert.That(response, Is.TypeOf<ErrorResponse>());
            Assert.That(((ErrorResponse)response).code, Is.EqualTo("UI_SYSTEM_MISMATCH"));
        }
    }
}
