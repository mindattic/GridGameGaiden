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
        /// <summary>The enemy's personality; Auto → Weakest for clever foes (INT ≥ 14), Threat for other humanoids, else Closest.</summary>
        public static Targeting TargetingOf(Unit enemy)
        {
            var t = enemy.Def.Targeting;
            if (t != Targeting.Auto) return t;
            if (enemy.Def.BaseStats.Intelligence >= 14) return Targeting.Weakest;
            return enemy.IsHumanoid ? Targeting.Threat : Targeting.Closest;
        }

        public static string Describe(Targeting t)
        {
            switch (t)
            {
                case Targeting.Weakest: return "Hunts the weakest hero";
                case Targeting.Threat: return "Goes after your hardest hitter";
                case Targeting.Squishy: return "Targets the most fragile hero";
                case Targeting.Erratic: return "Attacks at random";
                default: return "Attacks whoever is closest";
            }
        }

        /// <summary>The hero this enemy wants, among all living heroes.</summary>
        public static Unit PickTarget(BattleState s, Unit enemy) => PickFrom(s, enemy, s.LivingList(Team.Hero));

        /// <summary>
        /// The hero this enemy wants among <paramref name="heroes"/>, by its personality. Ties fall back to
        /// distance, then lower HP, then id, so every choice is deterministic. Erratic picks a hero that
        /// stays fixed for the whole turn (hash of clock and id) without consuming the battle RNG.
        /// </summary>
        public static Unit PickFrom(BattleState s, Unit enemy, List<Unit> heroes)
        {
            if (heroes == null || heroes.Count == 0) return null;
            var mode = TargetingOf(enemy);
            if (mode == Targeting.Erratic)
            {
                var sorted = new List<Unit>(heroes);
                sorted.Sort((x, y) => x.Id.CompareTo(y.Id));
                int h = (int)((s.Clock * 31 + enemy.Id * 17) & 0x7fffffff);
                return sorted[h % sorted.Count];
            }
            if (mode == Targeting.Threat)
            {
                bool anyDamage = false;
                foreach (var hero in heroes) if (hero.DamageDealt > 0) anyDamage = true;
                if (!anyDamage) mode = Targeting.Closest; // nobody has hurt it yet
            }
            Unit best = null;
            foreach (var hero in heroes)
                if (best == null || Better(enemy, mode, hero, best)) best = hero;
            return best;
        }

        private static bool Better(Unit enemy, Targeting mode, Unit a, Unit b)
        {
            float ka = Key(enemy, mode, a), kb = Key(enemy, mode, b);
            if (ka != kb) return ka < kb;
            int da = enemy.DistanceTo(a), db = enemy.DistanceTo(b);
            if (da != db) return da < db;
            if (a.Hp != b.Hp) return a.Hp < b.Hp;
            return a.Id < b.Id;
        }

        private static float Key(Unit enemy, Targeting mode, Unit h)
        {
            switch (mode)
            {
                case Targeting.Weakest: return h.Hp;
                case Targeting.Threat: return -h.DamageDealt;
                case Targeting.Squishy: return h.Effective.Vitality * 1000f + h.MaxHp;
                default: return enemy.DistanceTo(h);
            }
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
                if (enemy.IsRanged && !wounded)
                {
                    // Ranged: hold at its reach, prefer a clear firing lane, avoid melee contact.
                    score = -Math.Abs(dist - enemy.Def.AttackRange) * 1.5f;
                    if (RangedTarget(s, enemy) != null) score += 3f;
                    if (enemy.IsAdjacentTo(target)) score -= 2f;
                }
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

            var target = PickFrom(s, enemy, heroes);
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

        /// <summary>The hero it wants (by personality) among those in a clear straight row/column line within reach, or null.</summary>
        public static Unit RangedTarget(BattleState s, Unit enemy)
        {
            var inSight = new List<Unit>();
            foreach (var d in Coord.Cardinals)
            {
                var c = enemy.Pos + d;
                for (int i = 1; i <= enemy.Def.AttackRange && BattleState.InBounds(c); i++, c = c + d)
                {
                    var occ = s.UnitAt(c);
                    if (occ == null) continue;
                    if (occ.IsHero) inSight.Add(occ);
                    break; // line of sight stops at the first unit
                }
            }
            return PickFrom(s, enemy, inSight);
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
