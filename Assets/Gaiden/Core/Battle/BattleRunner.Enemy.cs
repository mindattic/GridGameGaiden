using System;
using System.Collections.Generic;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;

namespace Gaiden.Core.Battle
{
    internal sealed partial class BattleRunner
    {
        /// <summary>An enemy icon reached the trigger (§14): boss phases → skip/charge/trap/move → pincer or melee → end-of-turn ticks.</summary>
        private void RunEnemyTurn(Unit enemy)
        {
            MintTimeBank();
            Emit(new TurnStartedEvent { UnitId = enemy.Id });

            var phase = AdvanceBossPhases(enemy);
            if (S.IsOver || !enemy.Alive) return;

            if (enemy.IsImmobile)
            {
                Emit(new TurnSkippedEvent { UnitId = enemy.Id, Reason = enemy.HasBuff("sleep") ? "asleep" : "immobile" });
                EndEnemyTurn(enemy);
                return;
            }

            bool alreadyCasting = S.CastEntryOf(enemy.Id) != null;
            var charge = alreadyCasting ? null : EnemyPlanner.PlanCast(S, enemy, phase?.PrefersCharge ?? false);
            if (charge != null)
            {
                QueueCharge(enemy, charge);
                EndEnemyTurn(enemy);
                return;
            }

            if (TryLayTrap(enemy)) { EndEnemyTurn(enemy); return; }

            int steps = Math.Max(0, enemy.Def.MoveRange);
            for (int i = 0; i < steps && enemy.Alive && !S.IsOver; i++)
            {
                var dest = EnemyPlanner.PlanStep(S, enemy);
                if (dest == enemy.Pos) break;
                if (!StepEnemy(enemy, dest)) break;
            }
            if (!enemy.Alive || S.IsOver) return;

            EnemyAttack(enemy);
            if (CheckEnd()) return;
            EndEnemyTurn(enemy);
        }

        private void EndEnemyTurn(Unit enemy)
        {
            if (enemy.Alive) TickTurnBuffs(enemy, false);
            CheckEnd();
        }

        private BossPhaseDef AdvanceBossPhases(Unit enemy)
        {
            var phases = enemy.Def.BossPhases;
            if (phases == null || phases.Count == 0) return null;
            while (enemy.BossPhase + 1 < phases.Count && enemy.HpFraction <= phases[enemy.BossPhase + 1].HpThreshold)
            {
                enemy.BossPhase++;
                var ph = phases[enemy.BossPhase];
                Emit(new BossPhaseEvent { UnitId = enemy.Id, Phase = enemy.BossPhase, Text = ph.Announce });
                if (!string.IsNullOrEmpty(ph.Announce)) Announce(ph.Announce, enemy.Id);
                var a = C.TryAbility(ph.OnEnterAbility);
                if (a != null) ResolveAbility(enemy, a, enemy.Pos, enemy);
                if (S.IsOver || !enemy.Alive) return null;
            }
            return enemy.BossPhase >= 0 ? phases[enemy.BossPhase] : null;
        }

        private void QueueCharge(Unit enemy, ChargePlan plan)
        {
            int ticks = Formulas.SecondsToTicks(plan.Ability.CastTime > 0f ? plan.Ability.CastTime : BattleConfig.DefaultChargeCastSeconds);
            var entry = new TimelineEntry
            {
                Id = S.NextEntryId++,
                Kind = TimelineKind.Cast,
                OwnerId = enemy.Id,
                AbilityId = plan.Ability.Id,
                CasterTeam = Team.Enemy,
                TargetUnitId = plan.Target.Id,
                TargetTile = plan.Target.Pos,
                TotalCastTicks = ticks,
                RemainingTicks = ticks,
                IsCharge = true,
                ChargeShape = plan.Shape,
                LineTiles = plan.Shape == ChargeShape.Line ? plan.Line : null,
            };
            S.Timeline.Add(entry);
            Emit(new CastQueuedEvent { EntryId = entry.Id, CasterId = enemy.Id, AbilityId = plan.Ability.Id, Ticks = ticks, IsCharge = true, LineTiles = entry.LineTiles });
            Announce($"{enemy.Name} is charging {plan.Ability.Name}!", enemy.Id);
        }

        private bool TryLayTrap(Unit enemy)
        {
            if (enemy.Def.Trap == null || !C.Traps.TryGetValue(enemy.Def.Trap, out var trap)) return false;
            foreach (var h in S.Living(Team.Hero)) if (enemy.IsAdjacentTo(h)) return false;
            if (!S.Rng.Chance(trap.LayChance)) return false;
            var spots = new List<Coord>();
            foreach (var d in Coord.Cardinals)
            {
                var c = enemy.Pos + d;
                if (BattleState.InBounds(c) && S.UnitAt(c) == null && S.TrapAt(c) == null) spots.Add(c);
            }
            if (spots.Count == 0) return false;
            var at = spots[S.Rng.Range(0, spots.Count)];
            S.Traps.Add(new Trap { At = at, TrapId = trap.Id, OwnerId = enemy.Id });
            Emit(new TrapPlacedEvent { ById = enemy.Id, At = at, TrapId = trap.Id });
            return true;
        }

        /// <summary>One cardinal step. 1×1 enemies step into free tiles; multi-tile bosses shove heroes behind them (§1.5 rule 3); snake bodies follow the head.</summary>
        private bool StepEnemy(Unit enemy, Coord dest)
        {
            var delta = dest - enemy.Pos;
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) != 1) return false;
            var from = enemy.Pos;

            if (enemy.IsMultiTile)
            {
                if (!EnemyPlanner.FootprintStepLegal(S, enemy, dest)) return false;
                // Heroes in the entered tiles are swapped to the tile the body vacates on that lane.
                var shoves = new List<(Unit hero, Coord to)>();
                for (int dy = 0; dy < enemy.H; dy++)
                    for (int dx = 0; dx < enemy.W; dx++)
                    {
                        var entered = new Coord(dest.X + dx, dest.Y + dy);
                        if (enemy.Occupies(entered)) continue;
                        var occ = S.UnitAt(entered);
                        if (occ == null) continue;
                        if (!occ.IsHero) return false;
                        shoves.Add((occ, VacatedTile(enemy, from, delta, entered)));
                    }
                enemy.Pos = dest;
                Emit(new SteppedEvent { UnitId = enemy.Id, From = from, To = dest });
                if (S.Amplifier.HasValue && enemy.Occupies(S.Amplifier.Value) && !OccupiedBefore(enemy, from, S.Amplifier.Value))
                    ShoveAmplifier(enemy, VacatedTile(enemy, from, delta, S.Amplifier.Value));
                foreach (var (hero, to) in shoves)
                {
                    var hf = hero.Pos;
                    hero.Pos = to;
                    Emit(new DisplacedEvent { UnitId = hero.Id, From = hf, To = to, ByUnitId = enemy.Id });
                    if (BreaksOnMove(hero)) RemoveBuffsWhere(hero, b => b.Def.BreaksOnMove, "moved");
                    TriggerTrap(hero);
                }
                return true;
            }

            if (S.UnitAt(dest) != null) return false;
            enemy.Pos = dest;
            Emit(new SteppedEvent { UnitId = enemy.Id, From = from, To = dest });
            if (S.Amplifier == dest) ShoveAmplifier(enemy, from); // enemies can't use it, but walking through drags it along

            if (enemy.SnakeHeadId == enemy.Id) FollowSnake(enemy, from);
            return true;
        }

        /// <summary>Enemies can't use the amplifier; stepping onto it pushes it to the tile they came from (§1.2.6).</summary>
        private void ShoveAmplifier(Unit enemy, Coord to)
        {
            var from = S.Amplifier.Value;
            if (!BattleState.InBounds(to) || from == to) return;
            S.Amplifier = to;
            Emit(new AmplifierMovedEvent { From = from, To = to, ByUnitId = enemy.Id });
        }

        private static bool OccupiedBefore(Unit u, Coord anchor, Coord c) =>
            c.X >= anchor.X && c.X < anchor.X + u.W && c.Y >= anchor.Y && c.Y < anchor.Y + u.H;

        /// <summary>For a footprint stepping by <paramref name="delta"/>, the tile it vacates on the same lane as <paramref name="entered"/>.</summary>
        private static Coord VacatedTile(Unit u, Coord from, Coord delta, Coord entered)
        {
            if (delta.X > 0) return new Coord(from.X, entered.Y);
            if (delta.X < 0) return new Coord(from.X + u.W - 1, entered.Y);
            if (delta.Y > 0) return new Coord(entered.X, from.Y);
            return new Coord(entered.X, from.Y + u.H - 1);
        }

        private void FollowSnake(Unit head, Coord headFrom)
        {
            var members = new List<Unit>();
            foreach (var u in S.Units)
                if (u.Alive && u.SnakeHeadId == head.Id && u.SnakeIndex > 0) members.Add(u);
            members.Sort((a, b) => a.SnakeIndex.CompareTo(b.SnakeIndex));
            var trail = headFrom;
            foreach (var m in members)
            {
                var prev = m.Pos;
                if (S.UnitAt(trail) != null && S.UnitAt(trail) != m) break;
                m.Pos = trail;
                Emit(new SteppedEvent { UnitId = m.Id, From = prev, To = trail });
                trail = prev;
            }
        }

        /// <summary>Humanoid enemies resolve any pincer they're part of; otherwise melee the adjacent hero their personality picks.</summary>
        private void EnemyAttack(Unit enemy)
        {
            if (enemy.IsHumanoid && !enemy.IsMultiTile)
            {
                var pincers = PincerDetector.Detect(S, Team.Enemy, enemy);
                bool any = false;
                foreach (var p in pincers) if (p.A == enemy || p.B == enemy) any = true;
                if (any)
                {
                    Announce($"{enemy.Name} pincers!", enemy.Id);
                    ResolvePincers(Team.Enemy, enemy, enemy);
                    return;
                }
            }
            var adjacent = new List<Unit>();
            foreach (var h in S.Units)
                if (h.Alive && h.IsHero && enemy.IsAdjacentTo(h)) adjacent.Add(h);
            var target = EnemyPlanner.PickFrom(S, enemy, adjacent);
            if (target == null && enemy.IsRanged)
            {
                var shot = EnemyPlanner.RangedTarget(S, enemy);
                if (shot != null)
                {
                    Emit(new AttackEvent { AttackerId = enemy.Id, TargetId = shot.Id, IsRanged = true });
                    var r = Formulas.Physical(enemy, shot, S.Rng);
                    Damage(shot, r.Damage, enemy, r.Hit, DamageType.Physical);
                }
                return;
            }
            if (target == null) return;
            Emit(new AttackEvent { AttackerId = enemy.Id, TargetId = target.Id, IsMelee = true });
            var roll = Formulas.Physical(enemy, target, S.Rng);
            Damage(target, roll.Damage, enemy, roll.Hit, DamageType.Physical);
            if (roll.Hit != HitKind.Miss) MaybeCounter(target, enemy);
        }
    }
}
