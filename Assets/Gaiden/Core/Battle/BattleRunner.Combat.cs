using System;
using System.Collections.Generic;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;

namespace Gaiden.Core.Battle
{
    internal sealed partial class BattleRunner
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // Movement (§1.1) — one cardinal tile at a time; displaced occupant takes the tile just left.
        // ─────────────────────────────────────────────────────────────────────────────

        private bool DoMove(MoveCommand m)
        {
            var hero = S.UnitById(m.HeroId);
            if (hero == null || !hero.Alive || !hero.IsHero) { Emit(new AbilityRefusedEvent { CasterId = m.HeroId, Reason = "No such hero" }); return false; }
            if (hero.IsImmobile) { Emit(new AbilityRefusedEvent { CasterId = hero.Id, Reason = "Can't move" }); return false; }

            var before = SnapshotPositions();
            int trapsBefore = S.Traps.Count, pickupsBefore = S.Pickups.Count;
            int hpBefore = hero.Hp;
            Emit(new ActionBeganEvent { ActorId = hero.Id, Label = "Move" });
            var taken = new List<Coord>();
            WalkPath(hero, m.Path, taken);
            bool nothingHappened = taken.Count == 0
                || (hero.Alive && hero.Hp == hpBefore && S.Traps.Count == trapsBefore && S.Pickups.Count == pickupsBefore && SamePositions(before));
            if (nothingHappened)
            {
                // A drag that changes nothing is a cancel, not a turn (no clock cost).
                Emit(new AbilityRefusedEvent { CasterId = hero.Id, Reason = "No move" });
                return false;
            }
            if (BreaksOnMove(hero)) RemoveBuffsWhere(hero, b => b.Def.BreaksOnMove, "moved");

            S.PendingTimeBankSeconds = Math.Max(0f, Math.Min(BattleConfig.DragTimerSeconds, m.TimeLeftSeconds));
            if (CheckEnd()) return true; // a trap can kill the last hero
            ResolvePincers(Team.Hero, hero);
            CheckEnd();
            return true;
        }

        private static bool BreaksOnMove(Unit u)
        {
            foreach (var b in u.Buffs) if (b.Def.BreaksOnMove) return true;
            return false;
        }

        private Dictionary<int, Coord> SnapshotPositions()
        {
            var d = new Dictionary<int, Coord>();
            foreach (var u in S.Units) if (u.Alive) d[u.Id] = u.Pos;
            return d;
        }

        private bool SamePositions(Dictionary<int, Coord> before)
        {
            foreach (var u in S.Units)
                if (u.Alive && (!before.TryGetValue(u.Id, out var p) || p != u.Pos)) return false;
            return true;
        }

        /// <summary>Walks the hero step by step; returns false if a step was refused (the rest of the path is dropped).</summary>
        public bool WalkPath(Unit hero, List<Coord> path, List<Coord> taken)
        {
            foreach (var next in path)
            {
                if (!hero.Alive) return false;
                if (!hero.Pos.IsCardinalNeighbor(next))
                {
                    Emit(new StepBlockedEvent { UnitId = hero.Id, At = next, Reason = "not adjacent" });
                    return false;
                }
                if (!BattleState.InBounds(next))
                {
                    Emit(new StepBlockedEvent { UnitId = hero.Id, At = next, Reason = "edge" });
                    return false;
                }
                if (S.IsBlock(next))
                {
                    Emit(new StepBlockedEvent { UnitId = hero.Id, At = next, Reason = "block" });
                    return false;
                }
                var occ = S.UnitAt(next);
                if (occ != null && (occ.IsMultiTile || occ.IsSnakeMember || occ.IsRemains))
                {
                    Emit(new StepBlockedEvent { UnitId = hero.Id, At = next, Reason = "immovable" });
                    return false;
                }
                var from = hero.Pos;
                var pushed = S.MovableAt(next);
                hero.Pos = next;
                Emit(new SteppedEvent { UnitId = hero.Id, From = from, To = next });
                taken.Add(next);
                if (pushed != null)
                {
                    pushed.Pos = from; // a movable tile swaps into the tile just left, like any displaced actor
                    Emit(new AmplifierMovedEvent { From = next, To = from, ByUnitId = hero.Id });
                }
                if (occ != null)
                {
                    occ.Pos = from;
                    Emit(new DisplacedEvent { UnitId = occ.Id, From = next, To = from, ByUnitId = hero.Id });
                    if (!PreviewOnly && BreaksOnMove(occ)) RemoveBuffsWhere(occ, b => b.Def.BreaksOnMove, "moved");
                    if (!PreviewOnly) TriggerTrap(occ);
                }
                if (!PreviewOnly) TriggerTrap(hero);
                if (!PreviewOnly && !hero.Alive) return false;
            }
            return true;
        }

        private void TriggerTrap(Unit u)
        {
            if (!u.Alive || u.Team != Team.Hero) return;
            CollectPickup(u);
            if (!u.Alive) return;
            var trap = S.TrapAt(u.Pos);
            if (trap == null) return;
            S.Traps.Remove(trap);
            var def = C.Traps.TryGetValue(trap.TrapId, out var t) ? t : null;
            Emit(new TrapTriggeredEvent { UnitId = u.Id, At = u.Pos, TrapId = trap.TrapId });
            if (def == null) return;
            if (def.Damage > 0) Damage(u, def.Damage, null, HitKind.Normal, def.DamageType, def.Name);
            var buff = C.TryBuff(def.ApplyBuff);
            if (buff != null && u.Alive) ApplyBuff(u, buff, buff.Duration);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Pincers (§1.2, §13.1.1)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Board-wide pincer scan for <paramref name="team"/>; resolves every pair in chain order.</summary>
        private void ResolvePincers(Team team, Unit dropped, Unit mustInclude = null)
        {
            var pincers = PincerDetector.Detect(S, team, dropped);
            int chain = 0;
            foreach (var p in pincers)
            {
                if (S.IsOver) return;
                if (mustInclude != null && p.A != mustInclude && p.B != mustInclude) continue;
                if (!p.A.Alive || !p.B.Alive) continue;
                bool anyAlive = false;
                foreach (var o in p.Opponents) if (o.Alive) anyAlive = true;
                if (!anyAlive)
                {
                    if (chain > 0) Emit(new RespiteEvent { Attacker1 = p.A.Id, Attacker2 = p.B.Id });
                    continue;
                }
                ResolvePincer(p, team, chain++);
            }
        }

        private void ResolvePincer(Pincer p, Team team, int chainIndex)
        {
            var supporters = new List<Unit>();
            foreach (var x in p.SupportersA) if (!supporters.Contains(x)) supporters.Add(x);
            foreach (var x in p.SupportersB) if (!supporters.Contains(x)) supporters.Add(x);
            var formed = new PincerFormedEvent { Team = team, Attacker1 = p.A.Id, Attacker2 = p.B.Id, ChainIndex = chainIndex };
            foreach (var o in p.Opponents) formed.Opponents.Add(o.Id);
            foreach (var su in supporters) formed.Supporters.Add(su.Id);
            Emit(formed);

            // Power Amplifier: a hero attacker standing on it strikes amplified and uses it up (§1.2.6).
            Unit amplified = null;
            if (team == Team.Hero && S.Amplifier.HasValue)
                foreach (var attacker in new[] { p.A, p.B })
                    if (amplified == null && attacker.Alive && TouchesTile(attacker, S.Amplifier.Value)) amplified = attacker;
            if (amplified != null)
            {
                Emit(new AmplifierUsedEvent { At = S.Amplifier.Value, HeroId = amplified.Id });
                Announce("Amplified!", amplified.Id);
                S.Amplifier = null;
            }

            var hpBefore = new Dictionary<Unit, int>();
            foreach (var o in p.Opponents) hpBefore[o] = o.Hp;

            foreach (var attacker in new[] { p.A, p.B })
            {
                if (!attacker.Alive) continue;
                float mult = 1f + attacker.PincerDamageBonus;
                if (attacker == amplified) mult *= BattleConfig.AmplifierDamageMultiplier;
                bool shatters = attacker.IsHero && attacker.WeaponId != null && attacker.WeaponDurability == 1;
                if (shatters) mult *= BattleConfig.ShatterTargetMultiplier;
                bool twice = HasPassive(attacker, "double_attack");
                foreach (var target in p.Opponents)
                {
                    if (!target.Alive || !attacker.Alive) continue;
                    Strike(attacker, target, mult, false);
                    if (twice && target.Alive && attacker.Alive) Strike(attacker, target, mult * 0.5f, false);
                }
                if (attacker.IsHero && attacker.WeaponId != null && attacker.WeaponDurability > 0) WearWeapon(attacker);
            }

            foreach (var sup in supporters)
                foreach (var target in p.Opponents)
                    if (sup.Alive && target.Alive) Strike(sup, target, BattleConfig.SupporterDamageFraction, true);

            if (team == Team.Hero)
            {
                // Mana charge (§3.1.9): the share of enemy HP this pincer took, divided by how many heroes it took to do
                // it, multiplied up the deeper it sits in a chain. Lean, chained pincers farm mana; dog-piles don't.
                float share = 0f;
                foreach (var o in p.Opponents)
                    if (o.MaxHp > 0) share += Math.Min(1f, Math.Max(0, hpBefore[o] - o.Hp) / (float)o.MaxHp);
                int participants = 2 + supporters.Count;
                float divisor = 1f + BattleConfig.ChargeParticipantPenalty * Math.Max(0, participants - 2);
                float chainMult = 1f + BattleConfig.ChargeChainBonus * chainIndex;
                if (share > 0f)
                    AddCharge(Math.Max(BattleConfig.MinPincerCharge, (int)Math.Round(BattleConfig.PincerChargePerHp * share * chainMult / divisor)), p.A.Id, "pincer", participants, chainIndex + 1);
                if (amplified != null && amplified.Alive) AddCharge(BattleConfig.AmplifierCharge, amplified.Id, "amplifier");
            }
        }

        private bool HasPassive(Unit u, string id)
        {
            if (u.Def.Abilities == null || !u.Def.Abilities.Contains(id)) return false;
            var a = C.TryAbility(id);
            return a != null && a.Kind == AbilityKind.Passive;
        }

        private bool countering;

        /// <summary>Counter Attack passive: a surviving, adjacent defender strikes back at half strength (no chains).</summary>
        private void MaybeCounter(Unit defender, Unit attacker)
        {
            if (countering || attacker == null || !defender.Alive || !attacker.Alive) return;
            if (defender.Team == attacker.Team || !defender.IsAdjacentTo(attacker) || !HasPassive(defender, "counter_attack")) return;
            countering = true;
            Announce("Counter!", defender.Id);
            Strike(defender, attacker, 0.5f, false);
            countering = false;
        }

        private void Strike(Unit attacker, Unit target, float mult, bool support)
        {
            Emit(new AttackEvent { AttackerId = attacker.Id, TargetId = target.Id, IsSupport = support });
            var roll = Formulas.Physical(attacker, target, S.Rng, mult * KitDamageMultiplier(attacker) * attacker.OutgoingDamageMultiplier);
            int before = target.Hp;
            Damage(target, roll.Damage, attacker, roll.Hit, DamageType.Physical);
            if (roll.Hit == HitKind.Critical && attacker.IsHero) AddCharge(BattleConfig.CritCharge, attacker.Id, "critical");
            if (roll.Hit != HitKind.Miss) { AfterLandedHit(attacker, target, before - target.Hp); MaybeCounter(target, attacker); }
        }

        /// <summary>Is <paramref name="c"/> cardinally next to any tile of <paramref name="u"/>?</summary>
        private static bool TouchesTile(Unit u, Coord c)
        {
            foreach (var d in Coord.Cardinals) if (u.Occupies(c + d) && !u.Occupies(c)) return true;
            return false;
        }

        // ── Knockback (§6.3): shield bash and friends ──

        /// <summary>
        /// Shove <paramref name="t"/> up to <paramref name="tiles"/> straight away from the caster. Hitting another unit is
        /// a crash (both take damage); hitting stone or the board edge is a slam (heavy damage). Bosses and snakes don't budge.
        /// </summary>
        private void Knockback(Unit caster, Unit t, int tiles, int baseDamage)
        {
            if (!t.Alive || t.IsMultiTile || t.IsSnakeMember || t.IsRemains) return;
            var d = new Coord(t.Pos.X - caster.Pos.X, t.Pos.Y - caster.Pos.Y);
            var dir = Math.Abs(d.X) >= Math.Abs(d.Y) ? new Coord(Math.Sign(d.X), 0) : new Coord(0, Math.Sign(d.Y));
            if (dir.X == 0 && dir.Y == 0) return;
            for (int i = 0; i < tiles && t.Alive && !S.IsOver; i++)
            {
                var next = t.Pos + dir;
                if (!S.Walkable(next))
                {
                    Announce("Slam!", t.Id);
                    Damage(t, BattleConfig.KnockbackSlamDamage + baseDamage, caster, HitKind.Normal, DamageType.Physical, "Slam!", canPush: false);
                    return;
                }
                var other = S.UnitAt(next);
                if (other != null)
                {
                    Announce("Crash!", t.Id);
                    int crash = BattleConfig.KnockbackCrashDamage + baseDamage / 2;
                    Damage(t, crash, caster, HitKind.Normal, DamageType.Physical, "Crash!", canPush: false);
                    if (other.Alive && other.Team != caster.Team) Damage(other, crash, caster, HitKind.Normal, DamageType.Physical, "Crash!", canPush: false);
                    return;
                }
                if (S.MovableAt(next) is MovableTile mv)
                {
                    var beyond = next + dir;
                    if (!S.FreeForSpawn(beyond)) { Damage(t, BattleConfig.KnockbackCrashDamage, caster, HitKind.Normal, DamageType.Physical, "Crash!", canPush: false); return; }
                    mv.Pos = beyond;
                    Emit(new AmplifierMovedEvent { From = next, To = beyond, ByUnitId = t.Id });
                }
                var from = t.Pos;
                t.Pos = next;
                Emit(new DisplacedEvent { UnitId = t.Id, From = from, To = next, ByUnitId = caster.Id });
                if (t.IsHero) TriggerTrap(t);
            }
        }

        // ── Enemy kits (§14.4): passives and on-hit effects shared by every enemy attack ──

        /// <summary>Berserk: below half HP an enemy hits 35% harder.</summary>
        private float KitDamageMultiplier(Unit attacker) =>
            attacker.IsHero ? 1f : BattleConfig.EnemyStrikeMultiplier * (skirmishing ? 0.8f : 1f) * (attacker.HpFraction < 0.5f && HasPassive(attacker, "berserk") ? 1.35f : 1f);

        /// <summary>After an enemy's attack lands: lifesteal heals half the damage; an on-hit buff may stick.</summary>
        private void AfterLandedHit(Unit attacker, Unit target, int dealt)
        {
            if (attacker.IsHero || !attacker.Alive) return;
            if (dealt > 0 && HasPassive(attacker, "lifesteal")) Heal(attacker, Math.Max(1, dealt / 2), attacker);
            var buff = C.TryBuff(attacker.Def.OnHitBuff);
            if (buff != null && target.Alive && S.Rng.Chance(attacker.Def.OnHitChance)) ApplyBuff(target, buff, buff.Duration);
        }

        /// <summary>Armor loses one durability per landing enemy hit; at 0 it breaks and its bonuses are gone (US-243).</summary>
        private void WearArmor(Unit hero)
        {
            if (hero.ArmorId == null || hero.ArmorDurability <= 0) return;
            hero.ArmorDurability--;
            if (hero.ArmorDurability > 0) return;
            var armor = C.TryItem(hero.ArmorId);
            if (armor != null)
            {
                hero.GearStats = hero.GearStats + armor.Stats * -1f;
                foreach (var kv in armor.Resistances)
                    if (hero.GearResist.TryGetValue(kv.Key, out var cur) && kv.Value != 0f) hero.GearResist[kv.Key] = cur / kv.Value;
            }
            Emit(new ArmorBrokenEvent { UnitId = hero.Id, ArmorId = hero.ArmorId });
            Announce("Armor broke!", hero.Id);
            hero.ArmorId = null;
        }

        /// <summary>One durability per pincer; at 0 the weapon shatters: the strike already dealt ×1.5, the wielder takes 15% MaxHP (§24.5).</summary>
        private void WearWeapon(Unit hero)
        {
            hero.WeaponDurability--;
            if (hero.WeaponDurability > 0)
            {
                Emit(new WeaponWornEvent { UnitId = hero.Id, WeaponId = hero.WeaponId, Durability = hero.WeaponDurability });
                return;
            }
            int self = Math.Max(1, (int)Math.Round(hero.MaxHp * BattleConfig.ShatterSelfDamageFraction));
            var weapon = C.TryItem(hero.WeaponId);
            Emit(new WeaponShatteredEvent { UnitId = hero.Id, WeaponId = hero.WeaponId, SelfDamage = self });
            if (weapon != null) hero.GearStats = hero.GearStats + weapon.Stats * -1f;
            hero.WeaponId = null;
            hero.WeaponPower = 0f;
            Damage(hero, self, null, HitKind.Normal, DamageType.Physical, "Shatter!");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Timeline reactions to hits (§2.3, §13.4)
        // ─────────────────────────────────────────────────────────────────────────────

        private void Pushback(Unit enemy, Unit attacker)
        {
            var entry = S.TurnEntryOf(enemy.Id);
            if (entry == null || !entry.InZone) return;
            float zoneWidth = BattleConfig.BarLength - BattleConfig.ZoneStart;
            float frac = (entry.Ip - BattleConfig.ZoneStart) / zoneWidth;
            float strMult = 1f + (attacker.Effective.Strength - 10f) / 40f;
            if (strMult < 0.5f) strMult = 0.5f;
            float push = BattleConfig.PushbackBaseU * (0.5f + frac) * strMult;
            if (push > BattleConfig.PushbackMaxU) push = BattleConfig.PushbackMaxU;
            float from = entry.U;
            entry.Ip = Math.Max(0, entry.Ip - (int)Math.Round(push * BattleConfig.BarLength));
            float agiMult = 1f + enemy.Effective.Agility / 50f;
            entry.StunTicks = Math.Max(1, (int)Math.Round(BattleConfig.BaseStunTicks / agiMult));
            Emit(new PushedBackEvent { UnitId = enemy.Id, FromU = from, ToU = entry.U, StunTicks = entry.StunTicks });
        }

        /// <summary>Cast-stagger interrupt: Clutch (hero only) → WIS shrug → delay; cancel once delay ≥ cast time.</summary>
        private void StaggerCasts(Unit caster, Unit attacker)
        {
            var cast = S.CastEntryOf(caster.Id);
            if (cast == null) return;
            var cs = caster.Effective;
            bool allowClutch = caster.IsHero;
            if (allowClutch)
            {
                float clutch = Math.Min(BattleConfig.ClutchMaxChance, cs.Luck * BattleConfig.ClutchChancePerLuck);
                if (S.Rng.Chance(clutch))
                {
                    Emit(new ClutchEvent { EntryId = cast.Id, CasterId = caster.Id });
                    Announce("Clutch!", caster.Id);
                    ResolveCast(cast);
                    return;
                }
            }
            float resist = Math.Min(BattleConfig.MaxResistChance, cs.Wisdom * BattleConfig.WisdomResistPerPoint);
            if (S.Rng.Chance(resist))
            {
                Emit(new CastStaggeredEvent { EntryId = cast.Id, CasterId = caster.Id, DelayTicks = 0, Resisted = true });
                return;
            }
            float delaySec = BattleConfig.BaseInterruptDelaySeconds
                * (1f + attacker.Effective.Strength / BattleConfig.InterruptStrengthScale)
                / (1f + cs.Wisdom / BattleConfig.InterruptWisdomDelayScale);
            int delay = Formulas.SecondsToTicks(delaySec);
            cast.AccumulatedDelayTicks += delay;
            cast.RemainingTicks += delay;
            Emit(new CastStaggeredEvent { EntryId = cast.Id, CasterId = caster.Id, DelayTicks = delay });
            if (cast.AccumulatedDelayTicks >= cast.TotalCastTicks)
            {
                S.Timeline.Remove(cast);
                Emit(new CastCancelledEvent { EntryId = cast.Id, CasterId = caster.Id, AbilityId = cast.AbilityId, Reason = "interrupted" });
                Announce("Interrupted!", caster.Id);
                if (cast.CasterTeam == Team.Enemy)
                {
                    AddCharge(BattleConfig.InterruptCharge, caster.Id, "interrupt");
                }
            }
        }
    }
}
