using System;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal static class UiBackendSelection
    {
        internal static ErrorResponse ValidateDocument(string uiSystem, JObject document)
        {
            var backend = document?["backend"]?.ToString();
            if (string.Equals(uiSystem, HeraSettings.UiSystemUITK, StringComparison.Ordinal))
            {
                return string.Equals(backend, HeraSettings.UiSystemUITK, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : new ErrorResponse(
                        "UI_SYSTEM_MISMATCH",
                        "ui_system is uitk, so ui_doc apply requires a document with backend: 'uitk'.");
            }

            return string.IsNullOrWhiteSpace(backend)
                || string.Equals(backend, HeraSettings.UiSystemUGUI, StringComparison.OrdinalIgnoreCase)
                ? null
                : new ErrorResponse(
                    "UI_SYSTEM_MISMATCH",
                    $"ui_system is ugui, so ui_doc apply cannot use backend: '{backend}'.");
        }
    }
}
