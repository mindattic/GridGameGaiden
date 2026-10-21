using System;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Core.Rules
{
    public sealed class Pincer
    {
        public Unit A;
        public Unit B;
        /// <summary>Distinct opponents in the line, ordered from A toward B.</summary>
        public List<Unit> Opponents = new List<Unit>();
        public List<Unit> SupportersA = new List<Unit>();
        public List<Unit> SupportersB = new List<Unit>();
    }

    /// <summary>
    /// Pincer geometry (bible §1.2, GG-LAW-2): two Humanoid allies in the same row or column with
    /// every tile strictly between them covered by opponents (no gaps, no allies). A multi-tile
    /// enemy covering several line tiles counts as one opponent. Board-wide scan.
    /// </summary>
    public static class PincerDetector
    {
        public static List<Pincer> Detect(BattleState s, Team team, Unit dropped)
        {
            var members = new List<Unit>();
            foreach (var u in s.Units)
                if (u.Alive && u.Team == team && !u.IsMultiTile && u.IsHumanoid) members.Add(u);

            var pairs = new List<Pincer>();
            for (int i = 0; i < members.Count; i++)
                for (int j = i + 1; j < members.Count; j++)
                {
                    var p = TryPair(s, members[i], members[j], team);
                    if (p != null) pairs.Add(p);
                }
            return Order(pairs, dropped);
        }

        public static Pincer TryPair(BattleState s, Unit a, Unit b, Team team)
        {
            var pa = a.Pos; var pb = b.Pos;
            if (pa.X != pb.X && pa.Y != pb.Y) return null;
            if (pa.Manhattan(pb) < 2) return null;
            // Orient A→B
            var step = new Coord(Math.Sign(pb.X - pa.X), Math.Sign(pb.Y - pa.Y));
            var opponents = new List<Unit>();
            for (var c = pa + step; c != pb; c = c + step)
            {
                var occ = s.UnitAt(c);
                if (occ == null || occ.Team == team) return null;
                if (!opponents.Contains(occ)) opponents.Add(occ);
            }
            if (opponents.Count == 0) return null;
            return new Pincer
            {
                A = a, B = b, Opponents = opponents,
                SupportersA = FindSupporters(s, a, b),
                SupportersB = FindSupporters(s, b, a),
            };
        }

        /// <summary>Allies cardinally adjacent to <paramref name="attacker"/> (not its partner) join as supporters (§1.2.3).</summary>
        public static List<Unit> FindSupporters(BattleState s, Unit attacker, Unit partner)
        {
            var list = new List<Unit>();
            foreach (var u in s.Units)
            {
                if (!u.Alive || u.Team != attacker.Team || u == attacker || u == partner) continue;
                if (u.IsAdjacentTo(attacker)) list.Add(u);
            }
            list.Sort((x, y) => x.Id.CompareTo(y.Id));
            return list;
        }

        /// <summary>Chains first (B of one pair is A/B of the next), otherwise nearest to the dropped hero (§1.2.2).</summary>
        private static List<Pincer> Order(List<Pincer> pairs, Unit dropped)
        {
            var ordered = new List<Pincer>();
            var remaining = new List<Pincer>(pairs);
            Coord origin = dropped != null ? dropped.Pos : new Coord(0, 0);
            Unit chainEnd = null;
            while (remaining.Count > 0)
            {
                Pincer next = null;
                if (chainEnd != null)
                {
                    foreach (var p in remaining)
                        if (p.A == chainEnd || p.B == chainEnd) { next = p; break; }
                }
                if (next == null)
                {
                    if (ordered.Count == 0 && dropped != null)
                        foreach (var p in remaining)
                            if (p.A == dropped || p.B == dropped) { next = p; break; }
                    if (next == null)
                    {
                        int best = int.MaxValue;
                        foreach (var p in remaining)
                        {
                            int d = Math.Min(p.A.Pos.Manhattan(origin), p.B.Pos.Manhattan(origin));
                            if (d < best) { best = d; next = p; }
                        }
                    }
                }
                // Orient so the chain flows: the shared hero is A of the next link.
                if (chainEnd != null && next.B == chainEnd) { var t = next.A; next.A = next.B; next.B = t; var ts = next.SupportersA; next.SupportersA = next.SupportersB; next.SupportersB = ts; next.Opponents.Reverse(); }
                else if (ordered.Count == 0 && dropped != null && next.B == dropped) { var t = next.A; next.A = next.B; next.B = t; var ts = next.SupportersA; next.SupportersA = next.SupportersB; next.SupportersB = ts; next.Opponents.Reverse(); }
                ordered.Add(next);
                remaining.Remove(next);
                chainEnd = next.B;
                origin = next.B.Pos;
            }
            return ordered;
        }
    }
}
