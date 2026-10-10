using System;
using System.Collections.Generic;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;

namespace Gaiden.Core.Bot
{
    /// <summary>
    /// A player stand-in that drives the real command API. Greedy one-ply: it previews every
    /// L-shaped drag for every hero and every castable slot, scores the outcome, and picks the best.
    /// Used by headless soak tests (balance + soft-lock detection) and the in-game autoplay harness.
    /// </summary>
    public sealed class HeroBot
    {
        private readonly Rng rng;
        /// <summary>0 = always best move, 1 = always random legal move.</summary>
        public float Randomness;

        public HeroBot(int seed, float randomness = 0f)
        {
            rng = new Rng(seed);
            Randomness = randomness;
        }

        public BattleCommand Decide(BattleState s)
        {
            var heroes = s.LivingList(Team.Hero);
            var candidates = new List<(BattleCommand cmd, float score)>();

            foreach (var h in heroes)
            {
                if (!BattleEngine.CanMove(s, h.Id)) continue;
                for (int y = 0; y < BattleConfig.Rows; y++)
                    for (int x = 0; x < BattleConfig.Cols; x++)
                    {
                        var dest = new Coord(x, y);
                        if (dest == h.Pos) continue;
                        foreach (var path in LPaths(h.Pos, dest))
                        {
                            var pv = BattleEngine.PreviewMove(s, h.Id, path);
                            if (pv.StepsTaken.Count == 0) continue;
                            float score = ScoreMove(s, pv, h);
                            candidates.Add((new MoveCommand { HeroId = h.Id, Path = new List<Coord>(pv.StepsTaken) }, score));
                        }
                    }

                for (int slot = 0; slot < h.Bar.Count; slot++)
                {
                    if (BattleEngine.GetSlotStatus(s, h.Id, slot) != SlotStatus.Ready) continue;
                    var a = BattleEngine.SlotAbility(s, h, slot);
                    if (a == null) continue;
                    if (a.Mode == TargetMode.Auto)
                    {
                        float sc = ScoreAbility(s, h, a, h.Pos);
                        if (sc > 0) candidates.Add((new UseSlotCommand { HeroId = h.Id, Slot = slot, Target = h.Pos }, sc));
                        continue;
                    }
                    foreach (var t in BattleEngine.ValidTargets(s, h.Id, slot))
                    {
                        float sc = ScoreAbility(s, h, a, t);
                        if (sc > 0) candidates.Add((new UseSlotCommand { HeroId = h.Id, Slot = slot, Target = t }, sc));
                    }
                }
            }

            if (candidates.Count == 0) return new ShieldCommand();
            if (Randomness > 0f && rng.Chance(Randomness)) return candidates[rng.Range(0, candidates.Count)].cmd;

            float best = float.NegativeInfinity;
            var bestList = new List<BattleCommand>();
            foreach (var (cmd, score) in candidates)
            {
                if (score > best + 0.001f) { best = score; bestList.Clear(); bestList.Add(cmd); }
                else if (Math.Abs(score - best) <= 0.001f) bestList.Add(cmd);
            }
            if (best < 0.5f && rng.Chance(0.2f)) return new ShieldCommand();
            return bestList[rng.Range(0, bestList.Count)];
        }

        public static IEnumerable<List<Coord>> LPaths(Coord from, Coord to)
        {
            yield return Path(from, to, true);
            if (from.X != to.X && from.Y != to.Y) yield return Path(from, to, false);
        }

        private static List<Coord> Path(Coord from, Coord to, bool xFirst)
        {
            var list = new List<Coord>();
            var c = from;
            void StepX() { while (c.X != to.X) { c = new Coord(c.X + Math.Sign(to.X - c.X), c.Y); list.Add(c); } }
            void StepY() { while (c.Y != to.Y) { c = new Coord(c.X, c.Y + Math.Sign(to.Y - c.Y)); list.Add(c); } }
            if (xFirst) { StepX(); StepY(); } else { StepY(); StepX(); }
            return list;
        }

        private float ScoreMove(BattleState s, MovePreview pv, Unit hero)
        {
            float score = 0f;
            foreach (var p in pv.Pincers)
            {
                foreach (var o in p.Opponents)
                {
                    if (BattleEngine.IsArmored(s, o)) { score -= 1f; continue; }
                    score += 10f;
                    var e = s.TurnEntryOf(o.Id);
                    if (e != null && e.InZone) score += 4f;
                    if (o.HpFraction < 0.4f) score += 3f;
                }
                score += 2f * (p.SupportersA.Count + p.SupportersB.Count);
            }
            // Mild preference for staying out of melee range of soon-acting enemies when nothing else is on offer.
            if (pv.Positions.TryGetValue(hero.Id, out var at))
            {
                foreach (var e in s.LivingList(Team.Enemy))
                {
                    if (!pv.Positions.TryGetValue(e.Id, out var ep)) continue;
                    int d = Math.Abs(ep.X - at.X) + Math.Abs(ep.Y - at.Y);
                    if (d == 1 && hero.HpFraction < 0.35f) score -= 3f;
                    score += 0.05f * (8 - Math.Min(8, d));
                }
            }
            return score + rng.NextFloat() * 0.01f;
        }

        private float ScoreAbility(BattleState s, Unit caster, AbilityDef a, Coord anchor)
        {
            if (a.IsTeleport)
            {
                var copy = s.Clone();
                var cu = copy.UnitById(caster.Id);
                cu.Pos = anchor;
                float sc = 0f;
                foreach (var p in PincerDetector.Detect(copy, Team.Hero, cu)) sc += 10f * p.Opponents.Count;
                return sc - 2f;
            }
            var targets = TargetResolver.Collect(s, a, caster, anchor);
            float score = 0f;
            foreach (var t in targets)
            {
                if (t.Team != caster.Team)
                {
                    if (BattleEngine.IsArmored(s, t)) continue;
                    if (a.BaseDamage > 0) score += Math.Min(t.Hp, Formulas.SpellDamage(a, t, false)) * 0.6f + (t.Hp <= a.BaseDamage ? 8f : 0f);
                    if (a.ApplyBuff != null && !t.HasBuff(a.ApplyBuff)) score += 3f;
                    if (a.StealsMana) score += s.Bank.IsFull ? 0f : 3f;
                    if (a.HastenU > 0) score -= 5f;
                }
                else
                {
                    if (a.BaseHeal > 0) score += Math.Min(t.MaxHp - t.Hp, a.BaseHeal) * (t.HpFraction < 0.5f ? 0.9f : 0.2f);
                    if (a.RemovesDebuffs) foreach (var b in t.Buffs) if (b.Def.Kind == BuffKind.Debuff) score += 4f;
                    if (a.BaseDamage > 0 || (a.ApplyBuff != null && s.Content.TryBuff(a.ApplyBuff)?.Kind == BuffKind.Debuff)) score -= 20f;
                }
            }
            if (a.Kind == AbilityKind.Spell) score -= ManaBank.ParseCost(a.Cost).Count * 1.5f;
            if (a.Kind == AbilityKind.Item) score -= 4f;
            return score;
        }
    }

    /// <summary>Plays a battle to completion with a bot; returns the final state (or throws on a stuck battle).</summary>
    public static class BotRunner
    {
        public static BattleState Play(ContentDb content, BattleSetup setup, HeroBot bot, int maxActions, Action<BattleCommand, List<BattleEvent>, BattleState> observe = null)
        {
            var events = new List<BattleEvent>();
            var s = BattleEngine.Create(content, setup, events);
            observe?.Invoke(null, events, s);
            int refusedInARow = 0;
            for (int i = 0; i < maxActions && !s.IsOver; i++)
            {
                var cmd = bot.Decide(s);
                var ev = BattleEngine.Apply(s, cmd);
                observe?.Invoke(cmd, ev, s);
                bool acted = ev.Exists(e => e is PlayerControlEvent || e is BattleEndedEvent);
                refusedInARow = acted ? 0 : refusedInARow + 1;
                if (refusedInARow > 5) throw new InvalidOperationException($"Bot stuck: {cmd} refused repeatedly ({string.Join("; ", ev)})");
            }
            return s;
        }
    }
}
