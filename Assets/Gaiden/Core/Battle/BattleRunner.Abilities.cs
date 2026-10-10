using System;
using System.Collections.Generic;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;

namespace Gaiden.Core.Battle
{
    internal sealed partial class BattleRunner
    {
        /// <summary>The ability a bar slot fires. A plain consumable becomes a synthesized single-target heal/damage.</summary>
        public static AbilityDef AbilityForSlot(ContentDb c, AbilitySlot slot)
        {
            if (slot == null || slot.IsEmpty) return null;
            if (slot.IsWeapon)
            {
                var w = c.TryItem(slot.WeaponId);
                return new AbilityDef { Id = "weapon:" + slot.WeaponId, Name = "Equip " + (w?.Name ?? slot.WeaponId), Kind = AbilityKind.Skill, Shape = TargetShape.Self, Mode = TargetMode.Auto, Filter = TargetFilter.Any, Description = "Swap this weapon with the one in hand. Costs a turn." };
            }
            if (!slot.IsItem) return c.TryAbility(slot.AbilityId);
            return ItemAbility(c, c.TryItem(slot.ItemId));
        }

        /// <summary>A consumable's battle effect as an ability: its OnUseAbility, or one synthesized from the item's knobs.</summary>
        public static AbilityDef ItemAbility(ContentDb c, ItemDef item)
        {
            if (item == null || item.OutOfBattleOnly) return null;
            var onUse = c.TryAbility(item.OnUseAbility);
            if (onUse != null) return onUse;
            var a = new AbilityDef
            {
                Id = "item:" + item.Id, Name = item.Name, Kind = AbilityKind.Item, Icon = item.Icon,
                Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.AllyOnly,
            };
            if (item.Escape) { a.Escape = true; a.Shape = TargetShape.Self; a.Mode = TargetMode.Auto; return a; }
            if (item.Revives) { a.Revives = true; a.BaseHeal = Math.Max(1, item.BaseHealing); a.Shape = TargetShape.Self; a.Mode = TargetMode.Auto; return a; }
            if (item.GrantsOrbs > 0) { a.GrantsOrbs = item.GrantsOrbs; a.Shape = TargetShape.Self; a.Mode = TargetMode.Auto; return a; }
            if (item.BaseDamage > 0)
            {
                a.BaseDamage = item.BaseDamage; a.DamageType = item.DamageType; a.Filter = TargetFilter.EnemyOnly;
                a.Motion = ProjectileMotion.Bezier; a.BonusVsTag = item.BonusVsTag; a.BonusMultiplier = item.BonusMultiplier;
                return a;
            }
            if (item.BaseHealing > 0 || item.CuresBuff != null || item.CuresAll)
            {
                a.BaseHeal = item.BaseHealing; a.RemovesBuff = item.CuresBuff; a.RemovesDebuffs = item.CuresAll;
                return a;
            }
            return null;
        }

        private bool Refuse(Unit caster, string reason)
        {
            Emit(new AbilityRefusedEvent { CasterId = caster?.Id ?? -1, Reason = reason });
            if (caster != null) Announce(reason, caster.Id);
            return false;
        }

        private bool DoUseSlot(UseSlotCommand cmd)
        {
            var hero = S.UnitById(cmd.HeroId);
            if (hero == null || !hero.Alive || !hero.IsHero) return Refuse(null, "No such hero");
            var status = BattleEngine.GetSlotStatus(S, hero.Id, cmd.Slot);
            switch (status)
            {
                case SlotStatus.Ready: break;
                case SlotStatus.Silenced: return Refuse(hero, "Silenced!");
                case SlotStatus.Unaffordable: return Refuse(hero, "Not enough mana");
                case SlotStatus.CastCapReached: return Refuse(hero, "Too many spells!");
                case SlotStatus.Cooldown: return Refuse(hero, "Recharging");
                case SlotStatus.OutOfCharges: return Refuse(hero, "None left");
                case SlotStatus.CannotAct: return Refuse(hero, "Can't act");
                case SlotStatus.Locked: return Refuse(hero, "Locked");
                default: return Refuse(hero, "Empty");
            }

            var slot = hero.Bar[cmd.Slot];
            var a = AbilityForSlot(C, slot);
            if (a == null) return Refuse(hero, "Empty");

            Coord anchor = a.Mode == TargetMode.Auto ? TargetResolver.AutoAnchor(hero) : cmd.Target;
            if (a.Mode != TargetMode.Auto && !TargetResolver.IsValidPick(S, a, hero, anchor)) return Refuse(hero, "No target");
            if (a.Revives && !S.Units.Exists(u => u.IsHero && !u.Alive && u.Hp <= 0)) return Refuse(hero, "Nobody to revive");
            if (!a.IsTeleport && !a.Revives && !a.Escape && a.GrantsOrbs <= 0 && TargetResolver.Collect(S, a, hero, anchor).Count == 0 && a.Mode == TargetMode.Auto)
                return Refuse(hero, "No target");

            Emit(new ActionBeganEvent { ActorId = hero.Id, Label = a.Name });

            if (slot.IsWeapon)
            {
                SwapWeapon(hero, slot);
                return true;
            }
            if (slot.IsItem)
            {
                slot.Charges--;
                if (slot.FromInventory) Add(S.Results.ItemsConsumed, slot.ItemId, 1);
                Emit(new ItemUsedEvent { UserId = hero.Id, ItemId = slot.ItemId, ChargesLeft = slot.Charges });
                ResolveAbility(hero, a, anchor, TargetUnitAt(anchor, a));
            }
            else if (a.Kind == AbilityKind.Skill)
            {
                if (a.CooldownTurns > 0) hero.Cooldowns[a.Id] = a.CooldownTurns;
                ResolveAbility(hero, a, anchor, TargetUnitAt(anchor, a));
            }
            else
            {
                S.Bank.Spend(a.Cost);
                Emit(new OrbsSpentEvent { Count = a.Cost });
                if (a.CastTime > 0f)
                {
                    int ticks = Formulas.SecondsToTicks(Formulas.CastTime(a.CastTime, hero.Effective));
                    var target = TargetUnitAt(anchor, a);
                    var entry = new TimelineEntry
                    {
                        Id = S.NextEntryId++,
                        Kind = TimelineKind.Cast,
                        OwnerId = hero.Id,
                        AbilityId = a.Id,
                        CasterTeam = Team.Hero,
                        TargetUnitId = target?.Id ?? -1,
                        TargetTile = anchor,
                        TotalCastTicks = ticks,
                        RemainingTicks = ticks,
                    };
                    S.Timeline.Add(entry);
                    Emit(new CastQueuedEvent { EntryId = entry.Id, CasterId = hero.Id, AbilityId = a.Id, Ticks = ticks });
                }
                else
                {
                    ResolveAbility(hero, a, anchor, TargetUnitAt(anchor, a));
                }
            }
            CheckEnd();
            return true;
        }

        private Unit TargetUnitAt(Coord anchor, AbilityDef a) =>
            a.Mode == TargetMode.PickActor ? S.UnitAt(anchor) : null;

        /// <summary>A cast icon reached the trigger (or a Clutch forced it): resolve and remove it.</summary>
        private void ResolveCast(TimelineEntry cast)
        {
            S.Timeline.Remove(cast);
            var caster = S.UnitById(cast.OwnerId);
            var a = C.TryAbility(cast.AbilityId);
            if (caster == null || !caster.Alive || a == null)
            {
                Emit(new CastCancelledEvent { EntryId = cast.Id, CasterId = cast.OwnerId, AbilityId = cast.AbilityId, Reason = "caster down" });
                return;
            }
            if (cast.IsCharge) { ResolveCharge(caster, a, cast); CheckEnd(); return; }
            Coord anchor = cast.TargetTile;
            var tu = cast.TargetUnitId >= 0 ? S.UnitById(cast.TargetUnitId) : null;
            if (tu != null && tu.Alive) anchor = tu.Pos;
            if (a.Mode == TargetMode.Auto) anchor = caster.Pos;
            ResolveAbility(caster, a, anchor, tu != null && tu.Alive ? tu : null);
            CheckEnd();
        }

        /// <summary>The dispatcher (§6): per target, cleanse → fire×wet/frozen → steal → damage → blind roll → heal → hasten → buff.</summary>
        private void ResolveAbility(Unit caster, AbilityDef a, Coord anchor, Unit pickedUnit)
        {
            if (a.IsTeleport)
            {
                if (!S.FreeForSpawn(anchor)) { Announce("Blocked", caster.Id); return; }
                var from = caster.Pos;
                caster.Pos = anchor;
                Emit(new AbilityResolvedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = anchor, Tiles = { anchor } });
                Emit(new TeleportedEvent { UnitId = caster.Id, From = from, To = anchor });
                TriggerTrap(caster);
                if (caster.Alive && caster.IsHero) ResolvePincers(Team.Hero, caster);
                return;
            }

            if (a.Escape)
            {
                Emit(new AbilityResolvedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = caster.Pos });
                Announce("Escaped!");
                S.Phase = BattlePhase.Escaped;
                Emit(new BattleEndedEvent { Victory = false, Escaped = true });
                return;
            }
            if (a.Revives)
            {
                Emit(new AbilityResolvedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = caster.Pos });
                Revive(caster, a.BaseHeal);
                return;
            }
            if (a.GrantsOrbs > 0)
            {
                Emit(new AbilityResolvedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = caster.Pos, Targets = { caster.Id } });
                for (int i = 0; i < a.GrantsOrbs; i++) MintOrb(caster.Id, "item");
                return;
            }

            var targets = TargetResolver.Collect(S, a, caster, anchor);
            var resolved = new AbilityResolvedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = anchor };
            if (a.Shape != TargetShape.AllAllies && a.Shape != TargetShape.AllEnemies && a.Shape != TargetShape.Self)
                resolved.Tiles.AddRange(TargetResolver.Tiles(anchor, a.Shape, a.Radius));
            foreach (var t in targets) resolved.Targets.Add(t.Id);
            Emit(resolved);
            if (targets.Count == 0) { Announce("Missed!", caster.Id); return; }

            foreach (var t in targets)
            {
                if (S.IsOver) return;
                if (!t.Alive) continue;

                if (a.IsScan)
                {
                    Emit(new ScannedEvent { UnitId = t.Id, Stats = t.Effective, Hp = t.Hp, MaxHp = t.MaxHp });
                    S.Results.Scanned.Add(t.Def.Id);
                    S.Results.Seen.Add(t.Def.Id);
                }

                if (a.RemovesDebuffs) RemoveBuffsWhere(t, b => b.Def.Kind == BuffKind.Debuff, "cleansed");
                if (a.RemovesBuff != null) RemoveBuffsWhere(t, b => b.Def.Id == a.RemovesBuff, "cured");

                bool deals = a.BaseDamage > 0;
                if (deals && a.DamageType == DamageType.Fire)
                {
                    if (t.HasBuff("frozen")) ForceExpire(t, "frozen");
                    if (t.HasBuff("wet")) { RemoveBuffsWhere(t, b => b.Def.Id == "wet", "steam"); Announce("Steam!", t.Id); }
                }

                if (a.StealsMana)
                {
                    var cs = caster.Effective;
                    float chance = Math.Max(0f, Math.Min(1f, (cs.Luck + 0.5f * cs.Agility) / BattleConfig.StealDivisor));
                    if (S.Rng.Chance(chance)) { MintOrb(t.Id, "steal"); Announce("Steal!", t.Id); }
                    else Announce("Missed!", t.Id);
                    // Mug = Steal + its BaseDamage physical hit, handled by the damage step below.
                }

                if (deals && t.Alive)
                {
                    bool wet = a.DamageType == DamageType.Lightning && t.HasBuff("wet");
                    int dmg = Formulas.SpellDamage(a, t, wet);
                    if (a.BonusVsTag != null && t.Def.HasTag(a.BonusVsTag)) dmg = (int)Math.Round(dmg * a.BonusMultiplier);
                    bool crit = dmg > 0 && S.Rng.Chance(Formulas.CritPercent(caster.Effective) / 100f * BattleConfig.SpellCritFraction);
                    if (crit) dmg = (int)Math.Round(dmg * BattleConfig.SpellCritMultiplier);
                    Damage(t, dmg, caster, crit ? HitKind.Critical : HitKind.Normal, a.DamageType, wet ? "Shock!" : null);
                    if (crit && caster.IsHero) MintOrb(caster.Id, "critical");
                    if (t.Alive && a.DamageType == DamageType.Lightning && S.Rng.Chance(BattleConfig.LightningBlindChance))
                    {
                        var blind = C.TryBuff("blinded");
                        if (blind != null) ApplyBuff(t, blind, blind.Duration);
                    }
                }

                if (a.Knockback > 0 && t.Alive && t.Team != caster.Team) Knockback(caster, t, a.Knockback, a.BaseDamage);

                if (a.BaseHeal > 0 && t.Alive) Heal(t, a.BaseHeal, caster);

                if (a.HastenU > 0f && t.Alive) Hasten(t, a.HastenU);

                var buff = C.TryBuff(a.ApplyBuff);
                if (buff != null && t.Alive && S.Rng.Chance(a.ApplyBuffChance <= 0f ? 1f : a.ApplyBuffChance))
                {
                    int dur = buff.Duration;
                    if (buff.Id == "sleep" && t.HasBuff("warm")) dur = (int)Math.Ceiling(dur * BattleConfig.SleepWhenWarmMultiplier);
                    ApplyBuff(t, buff, dur);
                }
            }
        }

        /// <summary>Bar weapon swap: the slot's weapon goes in hand at factory durability; the old one takes the slot.</summary>
        private void SwapWeapon(Unit hero, AbilitySlot slot)
        {
            var incoming = C.TryItem(slot.WeaponId);
            var outgoing = C.TryItem(hero.WeaponId);
            if (outgoing != null) hero.GearStats = hero.GearStats + outgoing.Stats * -1f;
            if (incoming != null)
            {
                hero.GearStats = hero.GearStats + incoming.Stats;
                hero.WeaponId = incoming.Id;
                hero.WeaponPower = incoming.BaseDamage;
                hero.WeaponDurability = incoming.Durability;
            }
            Emit(new WeaponSwappedEvent { UnitId = hero.Id, FromWeapon = outgoing?.Id, ToWeapon = incoming?.Id });
            slot.WeaponId = outgoing?.Id;
        }

        /// <summary>Phoenix Down: the first fallen hero returns on the free tile nearest the user.</summary>
        private void Revive(Unit user, int hp)
        {
            var fallen = S.Units.Find(u => u.IsHero && !u.Alive && u.Hp <= 0);
            if (fallen == null) { Announce("Nobody to revive"); return; }
            Coord? spot = null; int best = int.MaxValue;
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    if (S.UnitAt(c) != null || S.TrapAt(c) != null) continue;
                    int d = c.Manhattan(user.Pos);
                    if (d < best) { best = d; spot = c; }
                }
            if (spot == null) { Announce("No room!"); return; }
            fallen.Pos = spot.Value;
            fallen.OnBoard = true;
            fallen.Hp = Math.Min(fallen.MaxHp, Math.Max(1, hp));
            Emit(new RevivedEvent { UnitId = fallen.Id, At = fallen.Pos, Hp = fallen.Hp });
        }

        /// <summary>Hasten/Quicken (§2.7.1): slide a turn icon forward, or rush a cast.</summary>
        private void Hasten(Unit t, float amountU)
        {
            var turn = S.TurnEntryOf(t.Id);
            if (turn != null)
            {
                float from = turn.U;
                turn.StunTicks = 0;
                turn.Ip = Math.Min(BattleConfig.BarLength, turn.Ip + (int)Math.Round(amountU * BattleConfig.BarLength));
                Emit(new HastenedEvent { EntryId = turn.Id, UnitId = t.Id, FromU = from, ToU = turn.U });
                return;
            }
            var cast = S.CastEntryOf(t.Id);
            if (cast != null)
            {
                float from = cast.U;
                int ticks = (int)Math.Round(amountU * BattleConfig.BarLength / BattleConfig.ReferencePacePerTick);
                cast.RemainingTicks = Math.Max(0, cast.RemainingTicks - ticks);
                Emit(new HastenedEvent { EntryId = cast.Id, UnitId = t.Id, FromU = from, ToU = cast.U });
            }
        }

        /// <summary>Enemy charge resolution (§13.4, §14.2): a magic hit on the target, or every hero still on the locked line.</summary>
        private void ResolveCharge(Unit caster, AbilityDef a, TimelineEntry cast)
        {
            // Support and area charges (howl, rally, mend, poison cloud) resolve through the shared dispatcher.
            if (a.Filter == TargetFilter.AllyOnly || (cast.ChargeShape != ChargeShape.Line && a.Shape != TargetShape.SingleActor))
            {
                var t = S.UnitById(cast.TargetUnitId);
                var anchor = t != null && t.Alive ? t.Pos : cast.TargetTile;
                if (a.Mode == TargetMode.Auto) anchor = caster.Pos;
                ResolveAbility(caster, a, anchor, t != null && t.Alive ? t : null);
                return;
            }
            var victims = new List<Unit>();
            if (cast.ChargeShape == ChargeShape.Line && cast.LineTiles != null)
            {
                foreach (var tile in cast.LineTiles)
                {
                    var u = S.UnitAt(tile);
                    if (u != null && u.IsHero && !victims.Contains(u)) victims.Add(u);
                }
            }
            else
            {
                var t = S.UnitById(cast.TargetUnitId);
                if (t != null && t.Alive) victims.Add(t);
            }
            var resolved = new AbilityResolvedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = cast.TargetTile };
            if (cast.LineTiles != null) resolved.Tiles.AddRange(cast.LineTiles);
            foreach (var v in victims) resolved.Targets.Add(v.Id);
            Emit(resolved);
            if (victims.Count == 0) { Announce("Dodged!", caster.Id); return; }
            foreach (var v in victims)
            {
                if (!v.Alive) continue;
                var roll = Formulas.Magic(caster, v, a.DamageType, S.Rng);
                int dmg = roll.Damage + (roll.Hit == HitKind.Miss ? 0 : Formulas.SpellDamage(a, v, false));
                Damage(v, dmg, caster, roll.Hit, a.DamageType);
                var buff = C.TryBuff(a.ApplyBuff);
                if (buff != null && v.Alive && roll.Hit != HitKind.Miss) ApplyBuff(v, buff, buff.Duration);
            }
        }
    }
}
