using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Core.Rules
{
    /// <summary>How far a hero is into one named armor set (GG-T182): pieces worn out of five, and which bonus tiers
    /// that switches on.</summary>
    public sealed class SetProgress
    {
        public ArmorSetDef Set;
        /// <summary>Distinct pieces of <see cref="Set"/> being worn (0-5).</summary>
        public int Worn;
        /// <summary>Bonus tiers whose piece count <see cref="Worn"/> reaches, in ascending piece order.</summary>
        public List<SetBonusDef> Active = new List<SetBonusDef>();
        /// <summary>The next tier still to reach, or null if every tier is active.</summary>
        public SetBonusDef Next;
    }

    /// <summary>Armor set bonuses (GG-T182). Pure: a list of worn armor item ids in, set progress and the bonuses
    /// that apply out. The battle engine applies <see cref="ActiveBonuses"/> to a hero at spawn and re-resolves
    /// when a piece breaks; the camp screens read <see cref="Progress"/>.</summary>
    public static class ArmorSets
    {
        /// <summary>Every set at least one of <paramref name="wornItemIds"/> belongs to, most pieces first.</summary>
        public static List<SetProgress> Progress(ContentDb c, IEnumerable<string> wornItemIds)
        {
            var bySet = new Dictionary<ArmorSetDef, HashSet<string>>();
            foreach (var id in wornItemIds ?? Enumerable.Empty<string>())
            {
                var set = c.TryArmorSetOf(id);
                if (set == null) continue;
                if (!bySet.TryGetValue(set, out var pieces)) bySet[set] = pieces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                pieces.Add(id);
            }
            var result = new List<SetProgress>();
            foreach (var kv in bySet)
            {
                var p = new SetProgress { Set = kv.Key, Worn = kv.Value.Count };
                foreach (var b in kv.Key.Bonuses.OrderBy(b => b.Pieces))
                {
                    if (p.Worn >= b.Pieces) p.Active.Add(b);
                    else if (p.Next == null) p.Next = b;
                }
                result.Add(p);
            }
            int Order(ArmorSetDef s) => c.ArmorSetOrder.IndexOf(s);
            return result.OrderByDescending(p => p.Worn).ThenBy(p => Order(p.Set)).ToList();
        }

        /// <summary>Every bonus tier switched on by <paramref name="wornItemIds"/>, across all sets.</summary>
        public static List<SetBonusDef> ActiveBonuses(ContentDb c, IEnumerable<string> wornItemIds) =>
            Progress(c, wornItemIds).SelectMany(p => p.Active).ToList();

        /// <summary>Summed stat bonus of every active tier.</summary>
        public static Stats BonusStats(ContentDb c, IEnumerable<string> wornItemIds)
        {
            var total = new Stats();
            foreach (var b in ActiveBonuses(c, wornItemIds)) total = total + b.Stats;
            return total;
        }

        private static readonly (string Name, Func<Stats, float> Get)[] StatNames =
        {
            ("STR", s => s.Strength), ("VIT", s => s.Vitality), ("AGI", s => s.Agility), ("SPD", s => s.Speed),
            ("INT", s => s.Intelligence), ("WIS", s => s.Wisdom), ("LCK", s => s.Luck), ("STA", s => s.Stamina),
        };

        /// <summary>One line for a bonus tier, e.g. "STR +3, Fire ×0.6, Starts battles Protected".</summary>
        public static string Describe(SetBonusDef b)
        {
            var parts = new List<string>();
            foreach (var (name, get) in StatNames)
            {
                float v = get(b.Stats);
                if (Math.Abs(v) > 0.01f) parts.Add($"{name} {(v > 0 ? "+" : "")}{v:0}");
            }
            foreach (var kv in b.Resistances) parts.Add($"{kv.Key} ×{kv.Value:0.##}");
            if (b.PincerDamageBonus > 0) parts.Add($"Pincer +{Math.Round(b.PincerDamageBonus * 100)}%");
            if (b.RegenPerTurn > 0) parts.Add($"Regen {b.RegenPerTurn}/turn");
            if (b.BattleStartManaOrbs > 0) parts.Add($"+{b.BattleStartManaOrbs} orbs at battle start");
            if (b.StartsProtected) parts.Add("Starts battles Protected");
            return string.Join(", ", parts);
        }
    }
}
