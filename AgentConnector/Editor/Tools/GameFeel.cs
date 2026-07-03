using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HeraAgent.Tools
{
    [HeraTool(
        Name = "game_feel",
        Description = "Look up a Game Feel / Juice design topic from the connector-bundled knowledge base (Game Feel & Juice Bible + Ethical Engagement Game Feel Framework + UI Feedback Design Guide + UI/UX Visual Theory & Trends). Returns { key, category, title, body } with concrete, implementation-ready parameters (px, seconds, %, Hz) and the ethical/accessibility constraints built in — presentation intensity must honestly match real achievement (Honest Juice). Includes a `ui` category (per-element feedback specs, ECN-DMN framework, cognitive load, choice symmetry, 2026 trends) — the deep layer behind Game Feel UI Mode's inline hints. No topic (or 'list') returns the bucketed topic index, ethics first. Always available; Game Feel Mode (Beta) additionally makes other tools point at these topics via agent_hint.",
        Examples = new[]
        {
            "game_feel",
            "game_feel screen_shake",
            "game_feel control_feel",
            "game_feel ui_bar",
            "game_feel ethics_checklist",
        },
        ExampleDescriptions = new[]
        {
            "Topic index grouped by category (ethics, theory, technique, ui, ...)",
            "Screen shake parameters (intensity/duration/decay + accessibility option)",
            "Input latency budget, ADSR, coyote time, jump buffering",
            "Dual-response health bar, segmented ticks, charge/cooldown bar specs",
            "Full UX-ethics validation checklist",
        })]
    public static class GameFeel
    {
        public class Parameters
        {
            [ToolParameter("Topic key (screen_shake, hit_stop, tweening_easing, honest_juice, ethics_checklist, ...). Omit or pass 'list' for the topic index.")]
            public string Topic { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            if (parameters == null) return new ErrorResponse("MISSING_PARAM", "Parameters cannot be null.");
            var p = new ToolParams(parameters);
            var argsToken = p.GetRaw("args") as JArray;

            string topic = p.Get("topic")
                ?? (argsToken != null && argsToken.Count >= 1 ? argsToken[0].ToString() : null);

            // Surface a load-time failure (bundled file missing / unreadable)
            // with a structured code so the caller can tell it apart from a
            // genuine topic miss.
            if (GameFeelStore.Count == 0)
            {
                var err = GameFeelStore.LoadError;
                return new ErrorResponse(
                    "GAME_FEEL_BUNDLE_UNAVAILABLE",
                    err ?? "Bundled game-feel data is unavailable on this connector install.",
                    suggestions: new List<string>
                    {
                        "Reinstall the AgentConnector UPM package — the game-feel file ships inside it.",
                        "If you're working from a local checkout, run `go run ./tools/build-game-feel-docs`.",
                    });
            }

            if (string.IsNullOrEmpty(topic) || topic == "list")
            {
                return new SuccessResponse(
                    $"game_feel: {GameFeelStore.Count} topics. Query one with `game_feel <key>`.",
                    new { topics = GameFeelStore.BuildIndex() });
            }

            var entry = GameFeelStore.Lookup(topic);
            if (entry != null)
            {
                return new SuccessResponse(
                    $"game_feel: {entry.title ?? entry.key}",
                    new
                    {
                        key = entry.key,
                        category = entry.category,
                        title = entry.title,
                        body = entry.body,
                    });
            }

            var suggests = GameFeelStore.SuggestSimilar(topic);
            var data = suggests.Count > 0 ? (object)new { did_you_mean = suggests } : null;
            var hints = new List<string>();
            foreach (var s in suggests) hints.Add($"game_feel {s}");
            if (hints.Count == 0)
                hints.Add("Run `game_feel list` for the full topic index.");

            return new ErrorResponse(
                "TOPIC_NOT_FOUND",
                $"No game-feel topic matches '{topic}'.",
                data: data,
                suggestions: hints);
        }
    }
}
