using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "ui_slop",
        Description = "Look up a Unity UI-slop tell from the connector-bundled taxonomy (49 tells across 5 areas A-E: decorative sweep, layout/RectTransform/containers, spacing, typography, color). Ported from the slopslap methodology, grounded in live hera measurement and per-version editor-binary reflection. Returns { id, area, severity, tell, check_ugui, check_uitk, check, exception, fix, borrow, deep_topic } — `check` is the predicate for the active ui_system (uGUI or UI Toolkit) that you re-measure from the live scene; `fix` is the mechanical repair; `borrow` is the quantitative snap target for replacement tells (null for deletion tells). No id (or 'list') returns the area-grouped index. Always available; Unity De-slop Mode (Beta) additionally makes other tools point at these tells via agent_hint.",
        Examples = new[]
        {
            "ui_slop",
            "ui_slop box-in-box",
            "ui_slop unscaled-spacing-ladder",
            "ui_slop tmp-italic",
            "ui_slop low-contrast-text",
        },
        ExampleDescriptions = new[]
        {
            "Taxonomy index grouped by area (A decorative, B layout, C spacing, D typography, E color)",
            "Surface-in-surface flatten rule + the game-UI exception gate (inventory slots are not flattened)",
            "Spacing ladder: base x fixed multiplier, snapped to the bundled reference",
            "Decorative italics: fontStyle & Italic == 0 (uGUI) / -unity-font-style has no italic (UITK)",
            "WCAG contrast: foreground vs background >= 4.5:1, measured live",
        })]
    public static class UiSlop
    {
        public class Parameters
        {
            [ToolParameter("Tell id (box-in-box, unscaled-spacing-ladder, tmp-italic, low-contrast-text, ...). Omit or pass 'list' for the taxonomy index.")]
            public string Id { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            if (parameters == null) return new ErrorResponse("MISSING_PARAM", "Parameters cannot be null.");
            var p = new ToolParams(parameters);
            var argsToken = p.GetRaw("args") as JArray;

            string id = p.Get("id")
                ?? (argsToken != null && argsToken.Count >= 1 ? argsToken[0].ToString() : null);

            // Surface a load-time failure (bundled file missing / unreadable) with
            // a structured code so the caller can tell it apart from a genuine miss.
            if (UiSlopStore.Count == 0)
            {
                var err = UiSlopStore.LoadError;
                return new ErrorResponse(
                    "UI_SLOP_BUNDLE_UNAVAILABLE",
                    err ?? "Bundled ui-slop data is unavailable on this connector install.",
                    suggestions: new List<string>
                    {
                        "Reinstall the AgentConnector UPM package — the ui-slop file ships inside it.",
                        "If you're working from a local checkout, run `go run ./tools/build-ui-slop-docs`.",
                    });
            }

            if (string.IsNullOrEmpty(id) || id == "list")
            {
                return new SuccessResponse(
                    $"ui_slop: {UiSlopStore.Count} tells across areas A-E. Query one with `ui_slop <id>`. Execute A -> B -> C -> D -> E.",
                    new { areas = UiSlopStore.BuildIndex() });
            }

            var entry = UiSlopStore.Lookup(id);
            if (entry != null)
            {
                // Lead with the check for the active UI system so the agent
                // measures the right predicate without re-deriving ui_system.
                var check = UiSlopStore.CheckFor(id, HeraSettings.UiSystem);
                return new SuccessResponse(
                    $"ui_slop {entry.area}: {entry.id}",
                    new
                    {
                        id = entry.id,
                        area = entry.area,
                        severity = entry.severity,
                        tell = entry.tell,
                        check,
                        check_ugui = entry.check_ugui,
                        check_uitk = entry.check_uitk,
                        exception = entry.exception,
                        fix = entry.fix,
                        borrow = entry.borrow,
                        deep_topic = entry.deep_topic,
                    });
            }

            var suggests = UiSlopStore.SuggestSimilar(id);
            var data = suggests.Count > 0 ? (object)new { did_you_mean = suggests } : null;
            var hints = new List<string>();
            foreach (var s in suggests) hints.Add($"ui_slop {s}");
            if (hints.Count == 0)
                hints.Add("Run `ui_slop list` for the full taxonomy index.");

            return new ErrorResponse(
                "TELL_NOT_FOUND",
                $"No ui-slop tell matches '{id}'.",
                data: data,
                suggestions: hints);
        }
    }
}
