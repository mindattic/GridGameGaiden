using System.Collections.Generic;
using System.Text;
using Gaiden.Core.Content;

namespace Gaiden.Core.Battle
{
    /// <summary>
    /// The party's single shared, capped, ordered line of mana orbs (bible §3.1, GG-LAW-6).
    /// Orbs render left-packed in insertion order; spending removes the leftmost orb of each
    /// demanded color, falling back to Colorless wilds for any colored shortfall.
    /// </summary>
    public sealed class ManaBank
    {
        public const int DefaultCapacity = 12;

        private readonly List<ManaColor> orbs = new List<ManaColor>();

        public int Capacity { get; }
        public int Count => orbs.Count;
        public bool IsFull => orbs.Count >= Capacity;
        public IReadOnlyList<ManaColor> Orbs => orbs;

        public ManaBank(int capacity = DefaultCapacity) { Capacity = capacity; }

        public ManaBank Clone()
        {
            var b = new ManaBank(Capacity);
            b.orbs.AddRange(orbs);
            return b;
        }

        /// <summary>Adds up to <paramref name="n"/> orbs; returns how many landed (the rest overflowed).</summary>
        public int Add(ManaColor color, int n = 1)
        {
            int landed = 0;
            for (int i = 0; i < n && orbs.Count < Capacity; i++) { orbs.Add(color); landed++; }
            return landed;
        }

        public int CountOf(ManaColor c)
        {
            int n = 0;
            foreach (var o in orbs) if (o == c) n++;
            return n;
        }

        public static List<ManaColor> ParseCost(string cost)
        {
            var list = new List<ManaColor>();
            if (string.IsNullOrEmpty(cost)) return list;
            foreach (char ch in cost)
            {
                switch (char.ToUpperInvariant(ch))
                {
                    case 'W': list.Add(ManaColor.White); break;
                    case 'U': list.Add(ManaColor.Blue); break;
                    case 'B': list.Add(ManaColor.Black); break;
                    case 'R': list.Add(ManaColor.Red); break;
                    case 'G': list.Add(ManaColor.Green); break;
                    case 'C': list.Add(ManaColor.Colorless); break;
                }
            }
            return list;
        }

        public static char Letter(ManaColor c)
        {
            switch (c)
            {
                case ManaColor.White: return 'W';
                case ManaColor.Blue: return 'U';
                case ManaColor.Black: return 'B';
                case ManaColor.Red: return 'R';
                case ManaColor.Green: return 'G';
                default: return 'C';
            }
        }

        public bool CanAfford(string cost) => CanAfford(ParseCost(cost));

        public bool CanAfford(List<ManaColor> cost) => PlanSpend(cost) != null;

        /// <summary>Removes the orbs for <paramref name="cost"/>; returns the spent indices' colors, or null if unaffordable.</summary>
        public List<ManaColor> Spend(string cost)
        {
            var plan = PlanSpend(ParseCost(cost));
            if (plan == null) return null;
            plan.Sort();
            var spent = new List<ManaColor>();
            for (int i = plan.Count - 1; i >= 0; i--)
            {
                spent.Add(orbs[plan[i]]);
                orbs.RemoveAt(plan[i]);
            }
            return spent;
        }

        // Colored requirements take their own color first (leftmost); any shortfall falls back
        // to Colorless wilds. Explicit Colorless requirements are paid only by Colorless.
        private List<int> PlanSpend(List<ManaColor> cost)
        {
            var used = new bool[orbs.Count];
            var picks = new List<int>();
            var shortfall = 0;
            int colorlessNeeded = 0;
            foreach (var c in cost)
            {
                if (c == ManaColor.Colorless) { colorlessNeeded++; continue; }
                int idx = FindLeftmost(c, used);
                if (idx < 0) { shortfall++; continue; }
                used[idx] = true; picks.Add(idx);
            }
            for (int i = 0; i < colorlessNeeded + shortfall; i++)
            {
                int idx = FindLeftmost(ManaColor.Colorless, used);
                if (idx < 0) return null;
                used[idx] = true; picks.Add(idx);
            }
            return picks;
        }

        private int FindLeftmost(ManaColor c, bool[] used)
        {
            for (int i = 0; i < orbs.Count; i++) if (!used[i] && orbs[i] == c) return i;
            return -1;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var o in orbs) sb.Append(Letter(o));
            return sb.ToString();
        }
    }
}
