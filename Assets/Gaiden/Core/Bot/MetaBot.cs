using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Content;

namespace Gaiden.Core.Bot
{
    /// <summary>
    /// A stand-in for what a player does at camp between battles, expressed against plain callbacks so
    /// Core stays free of the Meta assembly. The Meta-aware driver lives in the tests / app (CampRoutine).
    /// </summary>
    public static class MetaHeuristics
    {
        /// <summary>Simple gear score: the sum of an item's stat bonuses plus its attack.</summary>
        public static float Score(ItemDef i)
        {
            if (i == null) return 0f;
            var s = i.Stats;
            return s.Strength + s.Vitality + s.Agility + s.Speed + s.Intelligence + s.Wisdom + s.Luck + s.Stamina + i.BaseDamage * 0.5f;
        }

        public static IEnumerable<ItemDef> BestFirst(IEnumerable<ItemDef> items) => items.OrderByDescending(Score);
    }
}
