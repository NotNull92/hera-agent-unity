using System.Collections.Generic;

namespace HeraAgent
{
    /// <summary>
    /// The "juice" playbook surfaced through manage_ui's agent_hint when UI Juicy
    /// Mode is on. Concrete numbers are lifted from the Game Feel & Juice Bible + UI
    /// Feedback Design Guide so the calling agent applies feedback with real parameters
    /// instead of guessing. Pure strings — no Unity dependency, no allocation
    /// beyond the composed hint. Element property edits still go through
    /// manage_components; this only advises what to add.
    /// </summary>
    public static class UIJuiceGuide
    {
        // Per-element recipes — kept tight but specific (px / seconds / %).
        private static readonly Dictionary<string, string> Recipes = new Dictionary<string, string>
        {
            ["button"] =
                "Button feel (state machine Normal-Hover-Press-Release):\n" +
                "  - Hover: scale 100%->110%, EaseOut 0.15s, +5% brightness.\n" +
                "  - Press: ->95%, EaseOut 0.05s (immediate), -10% color.\n" +
                "  - Release: 95%->110%->100% with Back overshoot, 0.2s; play a click SFX (randomize pitch +-5-8% so repeats don't fatigue); 10ms haptic on mobile.\n" +
                "  - Optional idle: breathe 100%<->101% over a 2s loop.\n" +
                "  - Disabled: desaturate ~50%, opacity 60-70%, block interaction.\n" +
                "  - Mobile: keep the touch target >=44x44pt; no hover state, so react on Press.",

            ["panel"] =
                "Panel/popup entrance (entrance slow & exaggerated, exit fast & quiet):\n" +
                "  - In: Scale 0%->100% + Opacity 0->100%, Back easing (overshoot ~0.3), 0.3s — it should 'pop'.\n" +
                "  - Dim: semi-transparent black behind (50% opacity), fade in 0.2s, to focus and block clicks.\n" +
                "  - Out: Scale 100%->80% + fade out, EaseIn, 0.2s (faster than the entrance).",

            ["image"] =
                "Image/graphic feel:\n" +
                "  - Appear: Scale 0%->100%, EaseOut (or Back for a playful pop), 0.2-0.3s.\n" +
                "  - Reward/acquisition: pulse 100%->120%->100% + glow, scaled to rarity (small sparkle -> golden burst + screen shake).\n" +
                "  - On taking a hit/damage: flash to solid white for 1-2 frames + interrupt/restart the current tween + knock ~5-10px + bassy impact SFX — make the hit undeniable.\n" +
                "  - If interactive: hover 100%->110%, EaseOut 0.15s.",

            ["text"] =
                "Text feel:\n" +
                "  - Entrance: fade/slide in, EaseOut 0.3s; sequence multiple lines with a 0.03-0.05s stagger.\n" +
                "  - Changing numbers (score/HP/gold): count up rather than snap — step every 0.05s, total ~0.25s, EaseOut; optional 120%->100% pop; on a streak/combo raise the tick SFX pitch each step (Peggle / Mario-coin) to sell the build-up.\n" +
                "  - Damage popup: 0%->120%->100% over 0.15s, rise 50px across 0.5s, fade out the last 0.2s; crit = bigger + color + screen shake.",

            ["empty"] =
                "Container/layout feel:\n" +
                "  - Stagger child entrances with a 0.03-0.05s delay each (a slight offset reads far more polished than all-at-once).\n" +
                "  - Animate layout changes (EaseInOut 0.2-0.3s) instead of snapping children into place.",

            ["canvas"] =
                "Canvas-level setup:\n" +
                "  - Stand up the juice infrastructure first: a tween path (DOTween if available) + an audio manager for SFX.\n" +
                "  - Hit-pause: on high-impact moments (big reward, confirm, a hit landing) freeze the reaction ~30-80ms before it resolves — impact feels weighty (fighting games, God of War). Use sparingly, and tween UI on unscaled time so it survives the pause.\n" +
                "  - Keep the UI footprint <=30% of the screen; animate every screen transition (no abrupt cuts).\n" +
                "  - Expose a reduce-motion / intensity option so strong motion can be toned down.",

            ["bar"] =
                "Progress / health bar feel (the signature bar juice):\n" +
                "  - On decrease: drop the main fill fast (instant or EaseOut <=0.1s) and trail a second 'ghost'/'chip' bar behind it (white or darker) that lags and catches up over ~0.4s EaseOut — the gap reads as 'damage just taken'.\n" +
                "  - On increase (heal/XP): ease the fill up 0.2-0.3s and flash the bar brighter at the moment of change.\n" +
                "  - Low threshold: pulse/desaturate at low value (e.g. <25% = red breathing) to signal danger.\n" +
                "  - Segmented bars: tick each unit with a small pop + click so the count is felt, not just seen.",
        };

        /// <summary>
        /// Builds the agent_hint string for a created element, or null when there
        /// is no recipe for it (caller leaves agent_hint unset → omitted from JSON).
        /// </summary>
        public static string ForElement(string element, bool dotweenPreferred)
        {
            if (string.IsNullOrEmpty(element)) return null;
            if (!Recipes.TryGetValue(element.ToLowerInvariant(), out var recipe)) return null;

            return Header + recipe + "\n" + TweenLine(dotweenPreferred) + "\n" + Footer;
        }

        /// <summary>
        /// Composes one hint for several element types — dedupes by type and emits
        /// a single header / tween line / footer so a bulk ui_doc apply stays
        /// token-lean (the per-type recipes carry the strong signature, not
        /// repeated boilerplate). Returns null when no type has a recipe.
        /// </summary>
        public static string ForElements(IEnumerable<string> elements, bool dotweenPreferred)
        {
            if (elements == null) return null;
            var bodies = new List<string>();
            var seen = new HashSet<string>();
            foreach (var e in elements)
            {
                if (string.IsNullOrEmpty(e)) continue;
                var key = e.ToLowerInvariant();
                if (!seen.Add(key)) continue;
                if (Recipes.TryGetValue(key, out var recipe))
                    bodies.Add("--- " + key + " ---\n" + recipe);
            }
            if (bodies.Count == 0) return null;
            return Header + string.Join("\n\n", bodies) + "\n" + TweenLine(dotweenPreferred) + "\n" + Footer;
        }

        const string Header = "[Hera] UI Juicy Mode is on — make this feel alive (Game Feel & Juice Bible). Maximum output for minimum input.\n";
        const string Footer = "Golden rule — double down on the screen's purpose: reward / celebration UI earns big, exaggerated juice (bigger = more important); precision or input-heavy UI (forms, drag, text entry, competitive HUD) stays calm and steady so it stays readable. Always gate strong motion behind a reduce-motion / intensity option, and match feedback weight to action weight.";

        static string TweenLine(bool dotweenPreferred) => dotweenPreferred
            ? "Tweening: DOTween is enabled in Hera Settings — implement these with DOTween (project standard), e.g. " +
              "rt.DOScale(1.1f, 0.15f).SetEase(Ease.OutQuad); chain the release with .SetEase(Ease.OutBack); " +
              "use .SetUpdate(true) so UI keeps animating on unscaled time during a hit-pause."
            : "Tweening: no DOTween enabled — drive these from a coroutine/Update lerp " +
              "(x += (target - x) * 0.1f per frame gives a natural EaseOut) or Unity animation. " +
              "Enable DOTween in Hera Settings to switch to DOScale-based tweens.";
    }
}
