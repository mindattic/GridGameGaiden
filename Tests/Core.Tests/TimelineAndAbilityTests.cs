using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    public class TimelineAndAbilityTests
    {
        private static void Wiggle(BattleState s, string cls)
        {
            var h = Hero(s, cls);
            var t = new Coord(h.Pos.X, h.Pos.Y == 7 ? 6 : h.Pos.Y + 1);
            if (s.UnitAt(t) != null) t = new Coord(h.Pos.X, h.Pos.Y - 1);
            BattleEngine.Apply(s, new MoveCommand { HeroId = h.Id, Path = new() { t } });
        }

        [Fact]
        public void Clock_only_advances_on_committed_actions()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            long c0 = s.Clock;
            int ip0 = s.Timeline[0].Ip;
            Assert.Equal(c0, s.Clock); // creating/inspecting costs nothing
            Wiggle(s, "knight");
            Assert.Equal(c0 + BattleConfig.ActionTicks, s.Clock);
            Assert.NotEqual(ip0, s.Timeline[0].Ip);
        }

        [Fact]
        public void Enemy_acts_when_its_icon_reaches_the_trigger()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            int turns = 0;
            for (int i = 0; i < 10; i++)
            {
                Wiggle(s, "knight");
                AssertInvariants(s);
            }
            // every action is 2s; speed-10 wolf takes ~7s per turn → several turns in 20s
            var wolf = s.Units.Single(u => u.Def.Id == "wolf");
            Assert.NotEqual(new Coord(0, 0), wolf.Pos);
            _ = turns;
        }

        [Fact]
        public void Shield_protects_heroes_and_skips_to_next_enemy_turn()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.Contains(ev, e => e is BuffAppliedEvent b && b.BuffId == "protection");
            Assert.Contains(ev, e => e is TurnStartedEvent);
            Assert.True(ev.Last() is PlayerControlEvent);
        }

        [Fact]
        public void Pincer_on_in_zone_enemy_pushes_its_icon_back()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            var entry = s.TurnEntryOf(tank.Id);
            entry.Ip = 920;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            var push = ev.OfType<PushedBackEvent>().First();
            Assert.True(push.ToU < push.FromU);
            Assert.True(push.StunTicks > 0);
        }

        [Fact]
        public void Pincer_outside_zone_has_no_pushback()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            s.TurnEntryOf(s.Units.Single(u => u.Def.Id == "tank").Id).Ip = 100;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            Assert.Empty(ev.OfType<PushedBackEvent>());
        }

        [Fact]
        public void Instant_spell_spends_exact_orbs_damages_and_applies_buff()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            s.Bank.Add(3);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = new Coord(2, 0) });
            Assert.Equal(1 + ev.OfType<OrbMintedEvent>().Count(o => o.Landed), s.Bank.Count);
            Assert.Contains(ev, e => e is DamagedEvent d && d.Amount == 18);
            Assert.Contains(ev, e => e is BuffAppliedEvent b && b.BuffId == "burning");
        }

        [Fact]
        public void Unaffordable_or_silenced_spell_is_refused_without_cost()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            s.Bank.Add(1);
            long clock = s.Clock;
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = new Coord(2, 0) });
            Assert.Contains(ev, e => e is AbilityRefusedEvent);
            Assert.Equal(1, s.Bank.Count);
            Assert.Equal(clock, s.Clock);
        }

        [Fact]
        public void Any_orbs_pay_for_any_spell()
        {
            var bank = new ManaBank();
            bank.Add(3);
            Assert.True(bank.CanAfford(2));
            Assert.True(bank.Spend(2));
            Assert.Equal(1, bank.Count);
            Assert.False(bank.CanAfford(2));
            Assert.False(bank.Spend(2));
            Assert.Equal(1, bank.Count);
        }

        [Fact]
        public void Bank_overflow_fades_extra_orbs()
        {
            var bank = new ManaBank();
            Assert.Equal(12, bank.Add(15));
            Assert.Equal(0, bank.Add(1));
            Assert.Equal(12, bank.Count);
        }

        [Fact]
        public void Cast_time_spell_rides_timeline_then_resolves()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 0)), ("knight", 5, 7), ("mage", 4, 7));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "meteor" };
            s.Bank.Add(2);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = new Coord(2, 0) });
            Assert.Contains(ev, e => e is CastQueuedEvent);
            Assert.Equal(0, s.Bank.Count); // orbs spent at cast start
            bool resolved = ev.Any(e => e is AbilityResolvedEvent r && r.AbilityId == "meteor");
            for (int i = 0; i < 5 && !resolved; i++)
            {
                ev = BattleEngine.Apply(s, new ShieldCommand());
                resolved = ev.Any(e => e is AbilityResolvedEvent r && r.AbilityId == "meteor");
            }
            Assert.True(resolved);
            Assert.Null(s.CastEntryOf(m.Id));
        }

        [Fact]
        public void Fifth_hero_cast_is_refused_by_cast_cap()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 0)), ("knight", 5, 7), ("mage", 4, 7));
            var m = Hero(s, "mage");
            for (int i = 0; i < 4; i++)
                s.Timeline.Add(new TimelineEntry { Id = 900 + i, Kind = TimelineKind.Cast, OwnerId = m.Id, CasterTeam = Team.Hero, AbilityId = "meteor", RemainingTicks = 999, TotalCastTicks = 999 });
            m.Bar[0] = new AbilitySlot { AbilityId = "meteor" };
            s.Bank.Add(2);
            Assert.Equal(SlotStatus.CastCapReached, BattleEngine.GetSlotStatus(s, m.Id, 0));
        }

        [Fact]
        public void Frozen_halts_the_enemy_icon_and_expires_into_wet()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 5, 7), ("mage", 4, 7));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "frost" };
            s.Bank.Add(2);
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = new Coord(2, 2) });
            Assert.Contains(ev, e => e is BuffAppliedEvent b && b.BuffId == "frozen");
            // Frozen survives the action that applied it and thaws into Wet at the next turn boundary.
            ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((5, 6)) });
            Assert.Contains(ev, e => e is BuffRemovedEvent r && r.BuffId == "frozen");
            Assert.True(tank.HasBuff("wet"));
        }

        [Fact]
        public void Lightning_on_wet_hits_harder_and_fire_on_wet_makes_steam()
        {
            var db = Mini();
            db.Abilities["bolt"] = new AbilityDef { Id = "bolt", Name = "Bolt", Kind = AbilityKind.Spell, Cost = 0, BaseDamage = 14, DamageType = DamageType.Lightning, Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor };
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 5, 7), ("mage", 4, 7));
            var m = Hero(s, "mage");
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            tank.Buffs.Add(new BuffInstance { Def = db.Buffs["wet"], Remaining = 6 });
            m.Bar[0] = new AbilitySlot { AbilityId = "bolt" };
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = new Coord(2, 2) });
            Assert.Contains(ev, e => e is DamagedEvent d && d.Amount == 21);

            m.Bar[1] = new AbilitySlot { AbilityId = "fireball" };
            s.Bank.Add(2);
            tank.Buffs.RemoveAll(b => b.Def.Id == "wet");
            tank.Buffs.Add(new BuffInstance { Def = db.Buffs["wet"], Remaining = 6 });
            ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 1, Target = tank.Pos });
            Assert.Contains(ev, e => e is BuffRemovedEvent r && r.Reason == "steam");
        }

        [Fact]
        public void Potion_item_heals_and_consumes_a_charge()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 5, 7), ("mage", 4, 7));
            var k = Hero(s, "knight");
            k.Hp = 10;
            k.Bar[0] = new AbilitySlot { ItemId = "potion", Charges = 2, MaxCharges = 9, FromInventory = true };
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = k.Pos });
            Assert.Contains(ev, e => e is HealedEvent h && h.Amount == 30);
            Assert.Equal(1, k.Bar[0].Charges);
            Assert.Equal(1, s.Results.ItemsConsumed["potion"]);
        }

        [Fact]
        public void Teleport_relocates_and_fires_pincer_then_goes_on_cooldown()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 0)), ("knight", 1, 0), ("mage", 5, 7));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "teleport" };
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = new Coord(3, 0) });
            Assert.Contains(ev, e => e is TeleportedEvent);
            Assert.Contains(ev, e => e is PincerFormedEvent);
            Assert.Equal(SlotStatus.Cooldown, BattleEngine.GetSlotStatus(s, m.Id, 0));
        }

        [Fact]
        public void Quicken_hastens_an_enemy_icon()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 0)), ("knight", 5, 7), ("mage", 4, 7));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "quicken" };
            s.Bank.Add(1);
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            s.TurnEntryOf(tank.Id).Ip = 0;
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = tank.Pos });
            var h = ev.OfType<HastenedEvent>().Single();
            Assert.Equal(0.3f, h.ToU - h.FromU, 2);
        }

        [Fact]
        public void Enemy_charge_is_telegraphed_and_can_be_interrupted()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("witch", 2, 0)), ("knight", 5, 7), ("cleric", 0, 7));
            var witch = s.Units.Single(u => u.Def.Id == "witch");
            s.TurnEntryOf(witch.Id).Ip = 990;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.Contains(ev, e => e is CastQueuedEvent q && q.IsCharge);
            var cast = s.CastEntryOf(witch.Id);
            Assert.NotNull(cast);
            // Pincer the witch repeatedly; hits stagger and eventually cancel the charge (minting an orb).
            s.Units.Single(u => u.Def.Id == "knight").Pos = new Coord(1, 0);
            var cleric = Hero(s, "cleric");
            cleric.Pos = new Coord(3, 1);
            ev = BattleEngine.Apply(s, new MoveCommand { HeroId = cleric.Id, Path = P((3, 0)) });
            Assert.Contains(ev, e => e is CastStaggeredEvent || e is CastCancelledEvent || e is DiedEvent);
        }

        [Fact]
        public void Sleep_breaks_on_damage()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            tank.Buffs.Add(new BuffInstance { Def = db.Buffs["sleep"], Remaining = 3 });
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            if (ev.OfType<DamagedEvent>().Any(d => d.TargetId == tank.Id && d.Hit != HitKind.Miss))
                Assert.False(tank.HasBuff("sleep"));
        }

        [Fact]
        public void Weapon_wears_per_pincer_and_shatters_at_zero()
        {
            var db = Mini();
            var stage = Stage(("tank", 2, 3));
            var setup = new BattleSetup { Stage = stage, Seed = 3, UnlockedSlots = 2 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(1, 3), WeaponId = "sword", WeaponDurability = 2 });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(3, 4) });
            var s = BattleEngine.Create(db, setup, new());
            var cleric = Hero(s, "cleric");
            var all = new System.Collections.Generic.List<BattleEvent>();
            for (int i = 0; i < 4; i++)
                all.AddRange(BattleEngine.Apply(s, new MoveCommand { HeroId = cleric.Id, Path = cleric.Pos.Y == 4 ? P((3, 3)) : P((3, 4)) }));
            Assert.Contains(all, e => e is WeaponWornEvent);
            Assert.Contains(all, e => e is WeaponShatteredEvent);
            Assert.Null(Hero(s, "knight").WeaponId);
        }
    

        [Fact]
        public void Double_attack_passive_strikes_twice_and_counter_strikes_back()
        {
            var db = Mini();
            db.Abilities["double_attack"] = new AbilityDef { Id = "double_attack", Name = "Double Attack", Kind = AbilityKind.Passive };
            db.Abilities["counter_attack"] = new AbilityDef { Id = "counter_attack", Name = "Counter", Kind = AbilityKind.Passive };
            db.Classes["knight"].Abilities.Add("double_attack");
            db.Classes["tank"].Abilities.Add("counter_attack");
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            int knightHits = ev.OfType<AttackEvent>().Count(a => a.AttackerId == Hero(s, "knight").Id);
            Assert.True(knightHits >= 2, "knight should strike twice");
            if (ev.OfType<DamagedEvent>().Any(d => d.TargetId == s.Units.Single(u => u.Def.Id == "tank").Id && d.Hit != HitKind.Miss))
                Assert.Contains(ev, e => e is AttackEvent a && a.AttackerId == s.Units.Single(u => u.Def.Id == "tank").Id);
        }
    }
}
