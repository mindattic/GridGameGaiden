using System;
using System.Collections.Generic;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;

namespace Gaiden.Core.Rules
{
    public sealed class ChargePlan
    {
        public Unit Target;
        public AbilityDef Ability;
        public ChargeShape Shape;
        public List<Coord> Line;
    }

    /// <summary>Enemy decision-making (bible §14). Pure: reads state, never commits.</summary>
    public static class EnemyPlanner
    {
        private const float ThreatIntScale = 0.8f;

        public static Unit PickTarget(BattleState s, Unit enemy)
        {
            var heroes = s.LivingList(Team.Hero);
            if (heroes.Count == 0) return null;
            int maxThreat = 0;
            foreach (var h in heroes) maxThreat = Math.Max(maxThreat, h.DamageDealt);
            float intFactor = enemy.Effective.Intelligence * ThreatIntScale;
            Unit best = null; float bestScore = float.MaxValue;
            foreach (var h in heroes)
            {
                float threat = maxThreat > 0 ? (float)h.DamageDealt / maxThreat * intFactor : 0f;
                float score = enemy.DistanceTo(h) + h.HpFraction * 8f - threat;
                if (score < bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        /// <summary>The single best next tile for this enemy (its anchor for a multi-tile actor).</summary>
        public static Coord PlanStep(BattleState s, Unit enemy)
        {
            if (enemy.IsImmobile || enemy.IsSnakeSegment) return enemy.Pos;
            var target = PickTarget(s, enemy);
            if (target == null) return enemy.Pos;

            var candidates = new List<Coord> { enemy.Pos };
            foreach (var d in Coord.Cardinals)
            {
                var c = enemy.Pos + d;
                if (enemy.IsMultiTile) { if (!FootprintStepLegal(s, enemy, c)) continue; }
                else
                {
                    if (!BattleState.InBounds(c)) continue;
                    var occ = s.UnitAt(c);
                    if (occ != null && occ != enemy) continue;
                }
                candidates.Add(c);
            }

            bool wounded = enemy.HpFraction < BattleConfig.RetreatHpThreshold;
            bool seeksPincer = enemy.IsHumanoid && !enemy.IsMultiTile;
            var original = enemy.Pos;
            Coord best = enemy.Pos; float bestScore = float.NegativeInfinity;
            foreach (var c in candidates)
            {
                enemy.Pos = c;
                float dist = enemy.DistanceTo(target);
                float score = wounded ? dist : -dist;
                if (!wounded && enemy.IsAdjacentTo(target)) score += 2f;
                if (!enemy.IsMultiTile && WouldBeFlanked(s, c)) score -= 100f;
                if (c == original) score -= 0.5f;
                if (!wounded && seeksPincer && WouldFormPincer(s, enemy)) score += 50f;
                if (!wounded && WouldSupportAllyPincer(s, enemy)) score += 25f;
                enemy.Pos = original;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            enemy.Pos = original;
            return best;
        }

        /// <summary>Caster archetype: telegraph a charge when no hero is adjacent (or a boss phase prefers charging).</summary>
        public static ChargePlan PlanCast(BattleState s, Unit enemy, bool ignoreMeleeRange)
        {
            if (enemy.IsImmobile || enemy.IsSilenced) return null;
            var ability = s.Content.TryAbility(enemy.Def.ChargeAbility);
            if (ability == null) return null;
            var heroes = s.LivingList(Team.Hero);
            if (heroes.Count == 0) return null;
            if (!ignoreMeleeRange)
                foreach (var h in heroes) if (enemy.IsAdjacentTo(h)) return null;

            Unit target = null; int bestD = int.MaxValue;
            foreach (var h in heroes)
            {
                int d = enemy.DistanceTo(h);
                if (d < bestD) { bestD = d; target = h; }
            }
            var plan = new ChargePlan { Target = target, Ability = ability, Shape = enemy.Def.ChargeShape };
            if (plan.Shape == ChargeShape.Line)
            {
                plan.Line = LineToward(enemy, target);
                if (plan.Line.Count == 0) plan.Shape = ChargeShape.Single; // edge-pinned: fall back
            }
            return plan;
        }

        /// <summary>Cardinal line from beside the caster to the board edge along the dominant axis toward the target (X on ties).</summary>
        public static List<Coord> LineToward(Unit caster, Unit target)
        {
            int dx = target.Pos.X - caster.Pos.X, dy = target.Pos.Y - caster.Pos.Y;
            Coord dir = Math.Abs(dx) >= Math.Abs(dy)
                ? new Coord(Math.Sign(dx == 0 ? 1 : dx), 0)
                : new Coord(0, Math.Sign(dy));
            var list = new List<Coord>();
            Coord start = caster.Pos;
            if (dir.X > 0) start = new Coord(caster.Pos.X + caster.W - 1, caster.Pos.Y);
            if (dir.Y > 0) start = new Coord(caster.Pos.X, caster.Pos.Y + caster.H - 1);
            for (var c = start + dir; BattleState.InBounds(c); c = c + dir) list.Add(c);
            return list;
        }

        public static bool FootprintStepLegal(BattleState s, Unit enemy, Coord newAnchor)
        {
            for (int dy = 0; dy < enemy.H; dy++)
                for (int dx = 0; dx < enemy.W; dx++)
                {
                    var t = new Coord(newAnchor.X + dx, newAnchor.Y + dy);
                    if (!BattleState.InBounds(t)) return false;
                    var occ = s.UnitAt(t);
                    if (occ != null && occ != enemy && occ.Team == Team.Enemy) return false;
                }
            return true;
        }

        private static bool WouldBeFlanked(BattleState s, Coord loc)
        {
            bool HeroAt(Coord p) { var u = s.UnitAt(p); return u != null && u.Team == Team.Hero; }
            return (HeroAt(loc + Coord.Left) && HeroAt(loc + Coord.Right)) || (HeroAt(loc + Coord.Up) && HeroAt(loc + Coord.Down));
        }

        private static bool WouldFormPincer(BattleState s, Unit enemy)
        {
            foreach (var p in PincerDetector.Detect(s, Team.Enemy, enemy))
                if (p.A == enemy || p.B == enemy) return true;
            return false;
        }

        private static bool WouldSupportAllyPincer(BattleState s, Unit enemy)
        {
            foreach (var p in PincerDetector.Detect(s, Team.Enemy, null))
            {
                if (p.A == enemy || p.B == enemy) continue;
                if (p.SupportersA.Contains(enemy) || p.SupportersB.Contains(enemy)) return true;
            }
            return false;
        }
    }
}
