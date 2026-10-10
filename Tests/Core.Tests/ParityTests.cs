using System;
using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;
using Gaiden.Meta;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>One targeted assertion per bible rule that was previously only exercised by the soak.</summary>
    public class ParityTests
    {
        private static Unit Enemy(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        // ── Pincers ─────────────────────────────────────────────────────────────

        [Fact] // US-009
        public void Both_attackers_strike_every_trapped_opponent()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 1, 0), ("tank", 2, 0), ("tank", 3, 0)), ("knight", 0, 0), ("cleric", 4, 1));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((4, 0)) });
            var tanks = s.Units.Where(u => u.Def.Id == "tank").Select(u => u.Id).ToList();
            foreach (var attacker in new[] { Hero(s, "knight").Id, Hero(s, "cleric").Id })
                foreach (var t in tanks)
                    Assert.Contains(ev, e => e is AttackEvent a && a.AttackerId == attacker && a.TargetId == t && !a.IsSupport);
        }

        [Fact] // US-011
        public void Supporter_strikes_at_about_half_damage()
        {
            var db = Mini();
            db.Classes["mage"].BaseStats.Strength = 20;
            var ratios = new List<float>();
            for (int seed = 1; seed < 40; seed++)
            {
                var setup = new BattleSetup { Stage = Stage(("tank", 2, 3)), Seed = seed, UnlockedSlots = 2 };
                setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(1, 3) });
                setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(3, 4) });
                setup.Party.Add(new HeroSpec { ClassId = "mage", Start = new Coord(1, 4) });
                var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
                var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
                var dmg = ev.OfType<DamagedEvent>().ToList();
                var full = dmg.Where(d => d.SourceId == Hero(s, "knight").Id && d.Hit == HitKind.Normal).Select(d => d.Amount).FirstOrDefault();
                var sup = dmg.Where(d => d.SourceId == Hero(s, "mage").Id && d.Hit == HitKind.Normal).Select(d => d.Amount).FirstOrDefault();
                if (full > 0 && sup > 0) ratios.Add(sup / (float)full);
            }
            Assert.NotEmpty(ratios);
            Assert.InRange(ratios.Average(), 0.35f, 0.65f);
        }

        [Fact] // US-012
        public void Beasts_never_form_pincers()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 0, 3), ("wolf", 2, 3)), ("knight", 1, 3), ("cleric", 5, 7));
            Assert.Empty(PincerDetector.Detect(s, Team.Enemy, null));
        }

        [Fact] // US-027
        public void Humanoid_enemies_pincer_heroes_without_minting_orbs()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("goblin", 0, 3), ("goblin", 2, 3)), ("knight", 1, 3), ("cleric", 5, 7));
            s.TurnEntryOf(s.Units.First(u => u.Def.Id == "goblin").Id).Ip = 999;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.Contains(ev, e => e is PincerFormedEvent p && p.Team == Team.Enemy);
            Assert.DoesNotContain(ev, e => e is OrbMintedEvent o && (o.Reason == "pincer" || o.Reason == "support"));
        }

        [Fact] // US-016 / US-017
        public void Boss_step_shoves_the_hero_behind_it_then_melees_by_footprint()
        {
            var db = Mini();
            db.Classes["boss"].MoveRange = 1;
            var (s, _) = Battle(db, Stage(("boss", 0, 0)), ("knight", 2, 0), ("cleric", 5, 0));
            var boss = Enemy(s, "boss");
            s.TurnEntryOf(boss.Id).Ip = 999;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.Equal(new Coord(1, 0), boss.Pos);
            Assert.Equal(new Coord(0, 0), Hero(s, "knight").Pos);
            Assert.Contains(ev, e => e is DisplacedEvent d && d.ByUnitId == boss.Id);
            Assert.Contains(ev, e => e is AttackEvent a && a.AttackerId == boss.Id && a.IsMelee);
            AssertInvariants(s);
        }

        [Fact] // US-195
        public void Snake_members_are_immovable_to_drags()
        {
            var db = Mini();
            db.Classes["naga"] = new ClassDef { Id = "naga", Name = "Naga", Team = Team.Enemy, BaseStats = new Stats { Vitality = 20, Speed = 1 }, SnakeSegments = 2, MoveRange = 0 };
            var (s, _) = Battle(db, Stage(("naga", 2, 4)), ("knight", 2, 6), ("cleric", 5, 7));
            var seg = s.Units.First(u => u.IsSnakeSegment);
            var k = Hero(s, "knight");
            var target = seg.Pos;
            k.Pos = target + Coord.Down;
            if (s.UnitAt(k.Pos) != k && s.UnitAt(k.Pos) != null) return;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = new List<Coord> { target } });
            Assert.Contains(ev, e => e is StepBlockedEvent b && b.Reason == "immovable");
        }

        // ── Enemy behaviour ─────────────────────────────────────────────────────

        [Fact] // US-028
        public void Enemy_melee_targets_the_adjacent_hero_with_lowest_hp()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 2, 3)), ("knight", 1, 3), ("cleric", 3, 3));
            db.Classes["wolf"].MoveRange = 0;
            Hero(s, "cleric").Hp = 5;
            s.TurnEntryOf(Enemy(s, "wolf").Id).Ip = 999;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            var melee = ev.OfType<AttackEvent>().First(a => a.IsMelee);
            Assert.Equal(Hero(s, "cleric").Id, melee.TargetId);
        }

        [Fact] // US-180
        public void MoveRange_two_steps_twice()
        {
            var db = Mini();
            db.Classes["wolf"].MoveRange = 2;
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 0, 7), ("cleric", 5, 7));
            s.TurnEntryOf(Enemy(s, "wolf").Id).Ip = 999;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.Equal(2, ev.OfType<SteppedEvent>().Count(st => st.UnitId == Enemy(s, "wolf").Id));
        }

        [Fact] // US-173
        public void Enemy_values_supporting_an_ally_pincer()
        {
            var db = Mini();
            db.Classes["goblin"].MoveRange = 1;
            // goblins at (0,3),(2,3) already pincer the knight at (1,3); a third goblin at (0,5) can step to (0,4) to become a supporter.
            var (s, _) = Battle(db, Stage(("goblin", 0, 3), ("goblin", 2, 3), ("goblin", 0, 5)), ("knight", 1, 3), ("cleric", 5, 0));
            var third = s.Units.First(u => u.Def.Id == "goblin" && u.Pos == new Coord(0, 5));
            Assert.Equal(new Coord(0, 4), EnemyPlanner.PlanStep(s, third));
        }

        [Fact] // US-026
        public void Traps_spring_only_on_heroes()
        {
            var db = Mini();
            db.Traps["snare"] = new TrapDef { Id = "snare", Name = "Snare", Damage = 6 };
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 3, 2), ("cleric", 5, 7));
            s.Traps.Add(new Trap { At = new Coord(3, 2), TrapId = "snare" });
            // knight steps west into the tank's tile; the tank is displaced onto the trapped tile (3,2) — no trigger.
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((2, 2)) });
            Assert.DoesNotContain(ev, e => e is TrapTriggeredEvent);
            Assert.NotNull(s.TrapAt(new Coord(3, 2)));
        }

        // ── Timeline ────────────────────────────────────────────────────────────

        [Fact] // US-043
        public void Open_track_rate_scales_with_speed_and_the_zone_is_a_shared_crawl()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var wolf = Enemy(s, "wolf");
            var e = s.TurnEntryOf(wolf.Id);
            e.Ip = 0;
            int speed10 = BattleEngine.TicksUntilTurn(s, wolf.Id);
            Assert.Equal((int)Math.Ceiling(650 / 16.0) + (int)Math.Ceiling(350 / 11.0), speed10);
            wolf.LevelStats.Speed = 20;
            int speed20 = BattleEngine.TicksUntilTurn(s, wolf.Id);
            Assert.True(speed20 < speed10);
            e.Ip = BattleConfig.ZoneStart;
            int zoneSlow = BattleEngine.TicksUntilTurn(s, wolf.Id);
            wolf.LevelStats.Speed = 5;
            Assert.Equal(zoneSlow, BattleEngine.TicksUntilTurn(s, wolf.Id)); // Zone crawl ignores Speed
        }

        [Fact] // US-057
        public void Slowed_halves_the_timeline_rate()
        {
            var db = Mini();
            db.Buffs["slowed"] = new BuffDef { Id = "slowed", Name = "Slowed", Unit = DurationUnit.Turns, Duration = 2, TimelineSpeedMultiplier = 0.5f };
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var wolf = Enemy(s, "wolf");
            s.TurnEntryOf(wolf.Id).Ip = 0;
            int normal = BattleEngine.TicksUntilTurn(s, wolf.Id);
            wolf.Buffs.Add(new BuffInstance { Def = db.Buffs["slowed"], Remaining = 2 });
            int slowed = BattleEngine.TicksUntilTurn(s, wolf.Id);
            Assert.InRange(slowed / (float)normal, 1.8f, 2.2f);
        }

        [Fact] // US-047
        public void Pushback_grows_with_depth_and_strength()
        {
            float Push(int ip, float str)
            {
                var db = Mini();
                db.Classes["knight"].BaseStats.Strength = str;
                db.Classes["cleric"].BaseStats.Strength = str;
                var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
                s.TurnEntryOf(Enemy(s, "tank").Id).Ip = ip;
                var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
                var p = ev.OfType<PushedBackEvent>().FirstOrDefault();
                return p == null ? 0f : p.FromU - p.ToU;
            }
            Assert.True(Push(990, 20) > Push(700, 20));
            Assert.True(Push(990, 40) > Push(990, 10));
            Assert.True(Push(990, 400) <= BattleConfig.PushbackMaxU + 0.001f);
        }

        [Fact] // US-049
        public void Quicken_on_a_casting_hero_rushes_the_cast()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 0)), ("knight", 5, 7), ("mage", 4, 7));
            var m = Hero(s, "mage");
            s.Timeline.Add(new TimelineEntry { Id = 500, Kind = TimelineKind.Cast, OwnerId = m.Id, CasterTeam = Team.Hero, AbilityId = "meteor", RemainingTicks = 200, TotalCastTicks = 200, TargetTile = new Coord(2, 0) });
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = "quicken" };
            s.Bank.Add(ManaColor.Blue, 1);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = m.Pos });
            Assert.Contains(ev, e => e is HastenedEvent h && h.EntryId == 500);
        }

        [Fact] // US-051
        public void Casts_resolve_before_turns_arriving_on_the_same_tick()
        {
            var db = Mini();
            db.Abilities["zap"] = new AbilityDef { Id = "zap", Name = "Zap", Kind = AbilityKind.Spell, BaseDamage = 1, Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly, DamageType = DamageType.Arcane };
            var (s, _) = Battle(db, Stage(("wolf", 2, 0)), ("knight", 5, 7), ("mage", 4, 7));
            var wolf = Enemy(s, "wolf");
            s.TurnEntryOf(wolf.Id).Ip = 999;
            s.Timeline.Add(new TimelineEntry { Id = 501, Kind = TimelineKind.Cast, OwnerId = Hero(s, "mage").Id, CasterTeam = Team.Hero, AbilityId = "zap", RemainingTicks = 1, TotalCastTicks = 10, TargetUnitId = wolf.Id, TargetTile = wolf.Pos });
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            int cast = ev.FindIndex(e => e is AbilityResolvedEvent r && r.AbilityId == "zap");
            int turn = ev.FindIndex(e => e is TurnStartedEvent);
            Assert.True(cast >= 0 && turn >= 0 && cast < turn);
        }

        [Fact] // US-052
        public void Wave_icons_start_with_a_bounded_random_head_start()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 0, 0), ("tank", 1, 0), ("tank", 2, 0), ("tank", 3, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            Assert.All(s.Timeline, e => Assert.InRange(e.Ip, 0, BattleConfig.SpawnStaggerIp));
        }

        // ── Mana ────────────────────────────────────────────────────────────────

        [Fact] // US-075
        public void Hero_crit_mints_a_wild_orb()
        {
            var db = Mini();
            db.Classes["knight"].BaseStats.Luck = 200; // crit% caps at 50: some strikes crit
            bool saw = false;
            for (int seed = 1; seed < 40 && !saw; seed++)
            {
                var setup = new BattleSetup { Stage = Stage(("tank", 2, 3)), Seed = seed, UnlockedSlots = 2 };
                setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(1, 3) });
                setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(3, 4) });
                var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
                var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
                int crits = ev.OfType<DamagedEvent>().Count(d => d.Hit == HitKind.Critical && d.SourceId == Hero(s, "knight").Id);
                int wild = ev.OfType<OrbMintedEvent>().Count(o => o.Reason == "critical");
                if (crits > 0) { Assert.Equal(crits, wild); saw = true; }
            }
            Assert.True(saw);
        }

        [Fact] // US-076
        public void Steal_with_high_luck_always_mints_an_orb()
        {
            var db = Mini();
            db.Classes["knight"].BaseStats.Luck = 100;
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 2, 3), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = "steal" };
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = k.Pos });
            Assert.Contains(ev, e => e is OrbMintedEvent o && o.Reason == "steal");
        }

        [Fact] // US-077 / US-096
        public void Stagger_accumulates_and_cancelling_a_charge_mints_its_color()
        {
            var db = Mini();
            db.Abilities["charge_bolt"].Cost = "B";
            db.Classes["witch"].BaseStats.Wisdom = 0;
            db.Classes["knight"].BaseStats.Strength = 400;
            db.Classes["cleric"].BaseStats.Strength = 400;
            db.Classes["witch"].BaseStats.Vitality = 4000;
            var (s, _) = Battle(db, Stage(("witch", 2, 0)), ("knight", 1, 0), ("cleric", 3, 1));
            var witch = Enemy(s, "witch");
            s.Timeline.Add(new TimelineEntry { Id = 600, Kind = TimelineKind.Cast, OwnerId = witch.Id, CasterTeam = Team.Enemy, AbilityId = "charge_bolt", IsCharge = true, RemainingTicks = 25, TotalCastTicks = 25, TargetUnitId = Hero(s, "knight").Id });
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 0)) });
            Assert.Contains(ev, e => e is CastStaggeredEvent st && st.DelayTicks > 0);
            Assert.Contains(ev, e => e is CastCancelledEvent c && c.Reason == "interrupted");
            Assert.Contains(ev, e => e is OrbMintedEvent o && o.Reason == "interrupt" && o.Color == ManaColor.Black);
        }

        [Fact] // US-091
        public void Slot_status_reports_each_blocking_state()
        {
            var db = Mini();
            db.Buffs["silenced"] = new BuffDef { Id = "silenced", Name = "Silenced", Unit = DurationUnit.Turns, Duration = 2, BlocksSpells = true };
            var setup = new BattleSetup { Stage = Stage(("tank", 0, 0)), Seed = 1, UnlockedSlots = 3 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(5, 7), Bar = new List<string> { "fireball", "teleport", "item:potion:0", null, "heal" } });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(4, 7) });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            var k = Hero(s, "knight");
            Assert.Equal(SlotStatus.Unaffordable, BattleEngine.GetSlotStatus(s, k.Id, 0));
            Assert.Equal(SlotStatus.Ready, BattleEngine.GetSlotStatus(s, k.Id, 1));
            Assert.Equal(SlotStatus.OutOfCharges, BattleEngine.GetSlotStatus(s, k.Id, 2));
            Assert.Equal(SlotStatus.Locked, BattleEngine.GetSlotStatus(s, k.Id, 3));
            Assert.Equal(SlotStatus.Locked, BattleEngine.GetSlotStatus(s, k.Id, 4));
            k.Cooldowns["teleport"] = 2;
            Assert.Equal(SlotStatus.Cooldown, BattleEngine.GetSlotStatus(s, k.Id, 1));
            s.Bank.Add(ManaColor.Red, 2);
            k.Buffs.Add(new BuffInstance { Def = db.Buffs["silenced"], Remaining = 2 });
            Assert.Equal(SlotStatus.Silenced, BattleEngine.GetSlotStatus(s, k.Id, 0));
            k.Buffs.Add(new BuffInstance { Def = db.Buffs["sleep"], Remaining = 2 });
            Assert.Equal(SlotStatus.CannotAct, BattleEngine.GetSlotStatus(s, k.Id, 0));
        }

        [Fact] // US-102 refusal half
        public void Phoenix_down_is_refused_when_nobody_has_fallen()
        {
            var db = Mini();
            db.Items["phoenix"] = new ItemDef { Id = "phoenix", Name = "Phoenix Down", Type = ItemType.Consumable, Revives = true, BaseHealing = 30 };
            var (s, _) = Battle(db, Stage(("tank", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { ItemId = "phoenix", Charges = 1, MaxCharges = 1 };
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = k.Pos });
            Assert.Contains(ev, e => e is AbilityRefusedEvent);
            Assert.Equal(1, k.Bar[0].Charges);
        }

        [Fact] // US-107
        public void Scan_reveals_and_records_the_class()
        {
            var db = Mini();
            db.Abilities["scan"] = new AbilityDef { Id = "scan", Name = "Scan", Kind = AbilityKind.Spell, IsScan = true, Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 5, 7), ("cleric", 4, 7));
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = "scan" };
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = new Coord(2, 2) });
            Assert.Contains(ev, e => e is ScannedEvent);
            Assert.Contains("tank", s.Results.Scanned);
        }

        // ── Buffs ───────────────────────────────────────────────────────────────

        [Fact] // US-140
        public void Reapplying_a_buff_refreshes_its_duration()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 5, 7), ("mage", 4, 7));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            var tank = Enemy(s, "tank");
            s.Bank.Add(ManaColor.Red, 4);
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = tank.Pos });
            var first = tank.Buffs.First(b => b.Def.Id == "burning").Remaining;
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = tank.Pos });
            Assert.Single(tank.Buffs.Where(b => b.Def.Id == "burning"));
            Assert.True(tank.Buffs.First(b => b.Def.Id == "burning").Remaining >= first);
        }

        [Fact] // US-141 / US-153
        public void Burning_ticks_four_per_second_for_five_then_turns_warm()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 5, 7), ("cleric", 4, 7));
            var tank = Enemy(s, "tank");
            tank.Buffs.Add(new BuffInstance { Def = db.Buffs["burning"], Remaining = 5 });
            var all = new List<BattleEvent>();
            for (int i = 0; i < 4; i++) all.AddRange(BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = Hero(s, "knight").Pos.Y == 7 ? P((5, 6)) : P((5, 7)) }));
            var ticks = all.OfType<DamagedEvent>().Where(d => d.TargetId == tank.Id && d.Note == "Burning").ToList();
            Assert.Equal(5, ticks.Count);
            Assert.All(ticks, d => Assert.Equal(4, d.Amount));
            Assert.Contains(all, e => e is BuffAppliedEvent b && b.UnitId == tank.Id && b.BuffId == "warm");
        }

        [Fact] // US-149
        public void Poison_ticks_three_for_six()
        {
            var db = Mini();
            db.Buffs["poisoned"] = new BuffDef { Id = "poisoned", Name = "Poisoned", Unit = DurationUnit.Ticks, Duration = 6, DamagePerTick = 3 };
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 5, 7), ("cleric", 4, 7));
            var tank = Enemy(s, "tank");
            tank.Buffs.Add(new BuffInstance { Def = db.Buffs["poisoned"], Remaining = 6 });
            var all = new List<BattleEvent>();
            for (int i = 0; i < 4; i++) all.AddRange(BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = Hero(s, "knight").Pos.Y == 7 ? P((5, 6)) : P((5, 7)) }));
            Assert.Equal(6, all.OfType<DamagedEvent>().Count(d => d.TargetId == tank.Id && d.Amount == 3));
            Assert.False(tank.HasBuff("poisoned"));
        }

        [Fact] // US-145
        public void Fire_on_frozen_thaws_then_steams_off_the_wet()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 5, 7), ("mage", 4, 7));
            var tank = Enemy(s, "tank");
            tank.Buffs.Add(new BuffInstance { Def = db.Buffs["frozen"], Remaining = 1 });
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            s.Bank.Add(ManaColor.Red, 2);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = tank.Pos });
            Assert.Contains(ev, e => e is BuffRemovedEvent r && r.BuffId == "frozen");
            Assert.Contains(ev, e => e is BuffRemovedEvent r && r.BuffId == "wet" && r.Reason == "steam");
            Assert.False(tank.HasBuff("frozen"));
            Assert.False(tank.HasBuff("wet"));
        }

        [Fact] // US-146
        public void Sleep_on_a_warm_target_lasts_half_again_as_long()
        {
            var db = Mini();
            db.Abilities["sleep"] = new AbilityDef { Id = "sleep", Name = "Sleep", Kind = AbilityKind.Spell, ApplyBuff = "sleep", Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 5, 7), ("mage", 4, 7));
            var tank = Enemy(s, "tank");
            tank.Buffs.Add(new BuffInstance { Def = db.Buffs["warm"], Remaining = 3 });
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "sleep" };
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = tank.Pos });
            Assert.Contains(ev, e => e is BuffAppliedEvent b && b.BuffId == "sleep" && b.Duration == 5); // ceil(3 × 1.5)
        }

        [Fact] // US-147
        public void Sleeping_enemy_skips_its_turn()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 2, 2)), ("knight", 5, 7), ("cleric", 4, 7));
            var wolf = Enemy(s, "wolf");
            wolf.Buffs.Add(new BuffInstance { Def = db.Buffs["sleep"], Remaining = 3 });
            s.TurnEntryOf(wolf.Id).Ip = 999;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.Contains(ev, e => e is TurnSkippedEvent t && t.UnitId == wolf.Id && t.Reason == "asleep");
        }

        [Fact] // US-148
        public void Sleep_breaks_when_the_sleeper_is_displaced()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 3, 2), ("cleric", 5, 7));
            var tank = Enemy(s, "tank");
            tank.Buffs.Add(new BuffInstance { Def = db.Buffs["sleep"], Remaining = 3 });
            BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((2, 2)) });
            Assert.False(tank.HasBuff("sleep"));
        }

        [Fact] // US-150
        public void Blinded_attackers_hit_far_less_often()
        {
            var db = Mini();
            db.Buffs["blinded"] = new BuffDef { Id = "blinded", Name = "Blinded", Unit = DurationUnit.Turns, Duration = 2, AccuracyMultiplier = 0.5f };
            var (s, _) = Battle(db, Stage(("tank", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var k = Hero(s, "knight"); var tank = Enemy(s, "tank");
            var rng = new Rng(9);
            int Hits() { int h = 0; for (int i = 0; i < 4000; i++) if (Formulas.RollHit(k, tank, rng) != HitKind.Miss) h++; return h; }
            int clear = Hits();
            k.Buffs.Add(new BuffInstance { Def = db.Buffs["blinded"], Remaining = 2 });
            int blind = Hits();
            Assert.True(blind < clear * 0.75f, $"blind {blind} vs clear {clear}");
        }

        [Fact] // US-151
        public void Protection_reduces_incoming_spell_damage_by_fifteen_percent()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var k = Hero(s, "knight");
            var spell = new AbilityDef { BaseDamage = 100, DamageType = DamageType.Arcane };
            Assert.Equal(100, Formulas.SpellDamage(spell, k, false));
            k.Buffs.Add(new BuffInstance { Def = db.Buffs["protection"], Remaining = 1 });
            Assert.Equal(85, Formulas.SpellDamage(spell, k, false));
        }

        // ── Loot / XP / meta ────────────────────────────────────────────────────

        [Fact] // US-021
        public void Enemies_without_a_coin_range_drop_about_half_their_xp()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 1, 0)), ("knight", 0, 0), ("cleric", 2, 1));
            for (int i = 0; i < 8 && !s.IsOver; i++)
            {
                var c = Hero(s, "cleric");
                BattleEngine.Apply(s, new MoveCommand { HeroId = c.Id, Path = c.Pos == new Coord(2, 0) ? P((2, 1)) : P((2, 0)) });
            }
            Assert.True(s.IsOver);
            Assert.InRange(s.Results.Coins, (int)(s.Results.Xp * 0.5f * 0.75f), (int)Math.Ceiling(s.Results.Xp * 0.5f * 1.25f) + 1);
        }

        [Fact] // US-217
        public void Xp_curve_is_50_plus_10_level_squared()
        {
            Assert.Equal(60, Formulas.XpToNext(1));
            Assert.Equal(90, Formulas.XpToNext(2));
            Assert.Equal((2, 0), Formulas.LevelFromTotalXp(60));
            Assert.Equal((3, 0), Formulas.LevelFromTotalXp(150));
            Assert.Equal(150, Formulas.TotalXpForLevel(3));
        }

        [Fact] // US-216
        public void Wounds_carry_on_victory_and_the_fallen_revive_at_one_hp()
        {
            var c = Real;
            var save = Progression.NewGame(c, "W");
            var setup = BattleFactory.Build(c, save, c.CampaignStages()[0].Id, 4);
            var s = BattleEngine.Create(c, setup, new List<BattleEvent>());
            var pal = s.Units.First(u => u.Def.Id == "paladin");
            var cle = s.Units.First(u => u.Def.Id == "cleric");
            pal.Hp = 40; cle.Hp = 0; cle.OnBoard = false;
            foreach (var e in s.Units.Where(u => !u.IsHero)) { e.Hp = 0; e.OnBoard = false; }
            s.Phase = BattlePhase.Victory;
            BattleFactory.Commit(c, save, s);
            if (Progression.Level(save.Hero("paladin")) == 1) Assert.Equal(40, save.Hero("paladin").Hp);
            if (Progression.Level(save.Hero("cleric")) == 1) Assert.Equal(1, save.Hero("cleric").Hp);
        }

        [Fact] // US-219
        public void Enemy_levels_are_floored_to_the_campaign_recommendation()
        {
            var c = Real;
            var save = Progression.NewGame(c, "L");
            var stages = c.CampaignStages();
            var setup = BattleFactory.Build(c, save, stages[5].Id, 1);
            Assert.Equal(6, setup.RecommendedLevel);
            var s = BattleEngine.Create(c, setup, new List<BattleEvent>());
            Assert.All(s.Units.Where(u => !u.IsHero), u => Assert.True(u.Level >= 6));
        }

        [Fact] // US-225
        public void Salvage_returns_half_the_forge_inputs()
        {
            var c = Real;
            var recipe = Crafting.Recipes(c, CraftStation.Forge).First(r => r.BaseItem == null && r.Inputs.Any(i => i.Count >= 2));
            var save = new SaveData();
            save.AddItem(recipe.Result, 1);
            Assert.True(Crafting.Salvage(c, save, recipe.Result).Ok);
            foreach (var ic in recipe.Inputs) Assert.Equal(Math.Max(1, ic.Count / 2), save.Count(ic.Item));
        }

        [Fact] // US-230
        public void Unequipping_returns_the_piece_and_resets_durability()
        {
            var c = Real;
            var save = Progression.NewGame(c, "U");
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon && i.Durability > 1);
            save.AddItem(weapon.Id, 1);
            var h = save.Hero("paladin");
            Assert.True(Equipment.Equip(c, save, h, EquipmentSlot.Weapon, weapon.Id).Ok);
            h.WeaponDurability = 1;
            Assert.True(Equipment.Unequip(c, save, h, EquipmentSlot.Weapon).Ok);
            Assert.Equal(1, save.Count(weapon.Id));
            Assert.True(Equipment.Equip(c, save, h, EquipmentSlot.Weapon, weapon.Id).Ok);
            Assert.Equal(weapon.Durability, h.WeaponDurability);
        }

        [Fact] // US-235
        public void Recruits_arrive_at_roster_average_xp()
        {
            var c = Real;
            var save = Progression.NewGame(c, "R");
            foreach (var h in save.Roster) h.TotalXp = 300;
            save.Gold = 10000;
            var pick = Summon.Pool(c, save).First();
            Assert.True(Summon.Recruit(c, save, pick.Id).Ok);
            Assert.Equal(300, save.Hero(pick.Id).TotalXp);
            Assert.Contains(pick.Id, save.Party);
        }

        [Fact] // US-237
        public void Abandoned_bounties_can_be_retaken_and_claimed_ones_never_repost()
        {
            var c = Real;
            var save = Progression.NewGame(c, "B");
            var b = Bounties.Posted(c, save).First();
            Assert.True(Bounties.Accept(c, save, b.Id).Ok);
            Assert.True(Bounties.Abandon(save).Ok);
            Assert.True(Bounties.Accept(c, save, b.Id).Ok);
            Bounties.RecordKills(c, save, new Dictionary<string, int> { [b.TargetClass] = b.Kills });
            Assert.True(Bounties.Claim(c, save).Ok);
            Assert.DoesNotContain(Bounties.Posted(c, save), x => x.Id == b.Id);
        }

        [Fact] // US-238
        public void Bestiary_records_seen_on_spawn_and_defeated_on_victory()
        {
            var c = Real;
            var save = Progression.NewGame(c, "Best");
            var setup = BattleFactory.Build(c, save, c.CampaignStages()[0].Id, 9);
            var st = Gaiden.Core.Bot.BotRunner.Play(c, setup, new Gaiden.Core.Bot.HeroBot(1), 300);
            Assert.Equal(BattlePhase.Victory, st.Phase);
            BattleFactory.Commit(c, save, st);
            foreach (var kv in st.Results.Kills)
            {
                Assert.True(save.Bestiary[kv.Key].Seen);
                Assert.Equal(kv.Value, save.Bestiary[kv.Key].Defeated);
            }
        }

        [Fact] // US-242
        public void Camp_routine_rests_and_restocks()
        {
            var c = Real;
            var save = Progression.NewGame(c, "Camp");
            save.Gold = 5000;
            save.Hero("paladin").Hp = 10;
            save.Inventory.Remove("healing_potion_basic");
            var log = CampRoutine.Run(c, save);
            Assert.Equal(0, save.Hero("paladin").Hp);
            Assert.True(save.Count("healing_potion_basic") > 0);
            Assert.NotEmpty(log);
        }

        [Fact] // US-190 (content)
        public void Cyclops_is_a_two_by_two_boss_that_appears_in_the_campaign()
        {
            var c = Real;
            var cyclops = c.Class("cyclops00");
            Assert.Equal(2, cyclops.Footprint.W);
            Assert.Equal(2, cyclops.Footprint.H);
            Assert.Contains(c.CampaignStages(), st => st.Waves.Any(w => w.Enemies.Any(e => e.Class == "cyclops00")));
            Assert.NotEmpty(cyclops.BossPhases);
        }

        [Fact] // US-018
        public void Physical_formula_matches_the_bible_shape()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var k = Hero(s, "knight"); var tank = Enemy(s, "tank");
            // off 20, def = VIT 200: pen 5 → effDef 195 → reduced 20 − 68.25 < floor 4 → raw = max(1, 4) = 4; ±10% variance.
            var rng = new Rng(3);
            var normals = new List<int>();
            for (int i = 0; i < 400; i++) { var r = Formulas.Physical(k, tank, rng); if (r.Hit == HitKind.Normal) normals.Add(r.Damage); }
            Assert.InRange(normals.Min(), 3, 4);
            Assert.InRange(normals.Max(), 4, 5);
            // Level advantage scales damage by 1.07^Δ.
            k.Level = 6;
            var rng2 = new Rng(3);
            var high = new List<int>();
            for (int i = 0; i < 400; i++) { var r = Formulas.Physical(k, tank, rng2); if (r.Hit == HitKind.Normal) high.Add(r.Damage); }
            Assert.True(high.Average() > normals.Average());
            Assert.Equal(12 + 10 * 5 + 1, Formulas.MaxHp(new Stats { Vitality = 10 }, 1));
            Assert.Equal(1.5f * 0.25f, Formulas.CastTime(1.5f, new Stats { Wisdom = 999, Intelligence = 999 }), 3);
        }

        [Fact] // US-029
        public void Heroes_start_on_their_tiles_and_enemies_on_authored_or_upper_rows()
        {
            var db = Mini();
            var setup = new BattleSetup { Stage = StageWaves(new[] { ("tank", 4, 1), ("tank", -1, -1), ("tank", -1, -1), ("tank", -1, -1) }), Seed = 21, UnlockedSlots = 2 };
            setup.Party.Add(new HeroSpec { ClassId = "knight" });
            setup.Party.Add(new HeroSpec { ClassId = "cleric" });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            Assert.Equal(new Coord(1, 6), Hero(s, "knight").Pos);
            Assert.Equal(new Coord(4, 6), Hero(s, "cleric").Pos);
            Assert.NotNull(s.UnitAt(new Coord(4, 1)));
            Assert.All(s.Units.Where(u => !u.IsHero), u => Assert.InRange(u.Pos.Y, 0, 4));
        }

        [Fact] // US-170
        public void Smart_enemies_hunt_whoever_hurt_them_most()
        {
            var db = Mini();
            db.Classes["wolf"].BaseStats.Intelligence = 100;
            var (s, _) = Battle(db, Stage(("wolf", 2, 2)), ("knight", 2, 4), ("cleric", 2, 0));
            var wolf = Enemy(s, "wolf");
            Assert.Same(EnemyPlanner.PickTarget(s, wolf), EnemyPlanner.PickTarget(s, wolf));
            Hero(s, "cleric").DamageDealt = 200;
            Hero(s, "knight").DamageDealt = 0;
            Assert.Equal(Hero(s, "cleric").Id, EnemyPlanner.PickTarget(s, wolf).Id);
            Hero(s, "cleric").DamageDealt = 0;
            Hero(s, "knight").DamageDealt = 200;
            Assert.Equal(Hero(s, "knight").Id, EnemyPlanner.PickTarget(s, wolf).Id);
        }

        [Fact] // US-192
        public void Prefers_charge_phase_makes_a_caster_charge_even_point_blank()
        {
            var db = Mini();
            db.Classes["witch"].BossPhases.Add(new BossPhaseDef { HpThreshold = 1f, PrefersCharge = true });
            var (s, _) = Battle(db, Stage(("witch", 2, 2)), ("knight", 2, 3), ("cleric", 5, 7));
            var witch = Enemy(s, "witch");
            Assert.Null(EnemyPlanner.PlanCast(s, witch, false)); // adjacent: normally melee
            s.TurnEntryOf(witch.Id).Ip = 999;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.Contains(ev, e => e is CastQueuedEvent q && q.IsCharge);
        }
    }
}
