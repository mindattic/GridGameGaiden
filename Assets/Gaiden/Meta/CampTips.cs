using System.Collections.Generic;
using System.Linq;

namespace GridGame.Meta
{
    /// <summary>One guided first-use prompt for a Camp station (GG-T173).</summary>
    public sealed class CampTip
    {
        /// <summary>The station's screen id (HubPage.Id): "shop", "blacksmith", …</summary>
        public string Station;
        public string Title;
        /// <summary>One sentence: the station's single most important action.</summary>
        public string Text;
        /// <summary>Name (or name prefix ending in '-') of the control the pointing hand aims at; null = none.</summary>
        public string Target;
    }

    /// <summary>
    /// The camp tutorial (GG-T173, bible §25.14): the first time the player opens a Camp station, a one-time prompt
    /// points at the thing to do there. Seen tips are kept in <see cref="SaveData.SeenCampTips"/> so they never
    /// repeat; Settings › How to play can replay them. The onboarding chain (GG-T174…T180) refines the text and the
    /// order stations open in; this table is the single place that wording lives.
    /// </summary>
    public static class CampTips
    {
        public static readonly IReadOnlyList<CampTip> All = new[]
        {
            new CampTip { Station = "shop", Title = "The Merchant",
                Text = "Spend your first battle gold on Healing Potions: tap + to add some, then Buy. The Sell tab turns spare drops back into gold.",
                Target = "cart-plus-healing_potion_basic" },
            new CampTip { Station = "blacksmith", Title = "The Blacksmith",
                Text = "Gear wears down in battle. Open Repair and mend anything running low before it shatters.",
                Target = "tab-repair" },
            new CampTip { Station = "alchemist", Title = "The Alchemist",
                Text = "Brew potions from what enemies drop — far cheaper than buying them. Pick a recipe, then Brew.",
                Target = "recipe-" },
            new CampTip { Station = "enchantress", Title = "The Enchantress",
                Text = "Bind an essence into a piece of gear to give it a lasting effect. Pick a recipe, then Enchant.",
                Target = "recipe-" },
            new CampTip { Station = "abilities", Title = "Training",
                Text = "Learn new skills and spells here, then set them on a hero's bar so they can use them in battle.",
                Target = null },
            new CampTip { Station = "bounties", Title = "Bounties",
                Text = "Take one contract at a time. Defeat the target enemy as you fight, then come back to Claim the reward.",
                Target = "bounty-" },
        };

        public static CampTip For(string station) => All.FirstOrDefault(t => t.Station == station);

        /// <summary>The tip to show on opening <paramref name="station"/>, or null when it has none, it's been seen,
        /// or this is a bot-played save.</summary>
        public static CampTip Pending(SaveData s, string station)
        {
            if (s == null || s.TestMode || station == null) return null;
            var tip = For(station);
            return tip != null && !s.SeenCampTips.Contains(station) ? tip : null;
        }

        public static void MarkSeen(SaveData s, string station) { if (s != null && station != null) s.SeenCampTips.Add(station); }

        /// <summary>Settings › How to play › Replay camp tips.</summary>
        public static void ResetAll(SaveData s) => s?.SeenCampTips.Clear();
    }
}
