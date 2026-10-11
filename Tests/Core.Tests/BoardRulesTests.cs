using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    public class BoardRulesTests
    {
        [Fact]
        public void Slide_displaces_occupant_into_tile_just_left()
        {
            // bible §1.1: H A E E → (drag H east) A H E E → (again) A E H E
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 2, 0), ("tank", 3, 0)), ("knight", 0, 0), ("cleric", 1, 0));
            var knight = Hero(s, "knight");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((1, 0), (2, 0)) });
            Assert.Equal("cleric", At(s, 0, 0).Def.Id);
            Assert.Equal("tank", At(s, 1, 0).Def.Id);
            Assert.Same(knight, At(s, 2, 0));
            Assert.Equal("tank", At(s, 3, 0).Def.Id);
            Assert.Equal(2, ev.OfType<DisplacedEvent>().Count());
            AssertInvariants(s);
        }

        [Fact]
        public void Movement_never_deals_damage()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 1, 0)), ("knight", 0, 0), ("cleric", 5, 7));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((1, 0), (2, 0)) });
            Assert.DoesNotContain(ev, e => e is DamagedEvent d && d.TargetId == At(s, 0, 0).Id);
            Assert.DoesNotContain(ev.TakeWhile(e => !(e is TimelineAdvancedEvent)), e => e is DamagedEvent);
        }

        [Fact]
        public void Worked_example_1_4_slide_then_pincer()
        {
            // bible §1.4: [C](2,3) [g](3,3) [g](4,3) [K](5,3); K drags west → g shoved to (5,3); C+K pincer g(3,3).
            var db = Mini();
            var (s, _) = Battle(db, Stage(("goblin", 3, 3), ("goblin", 4, 3)), ("cleric", 2, 3), ("knight", 5, 3));
            var knight = Hero(s, "knight");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((4, 3)) });
            var pincer = ev.OfType<PincerFormedEvent>().Single();
            Assert.Contains(pincer.Attacker1, new[] { knight.Id, Hero(s, "cleric").Id });
            Assert.Single(pincer.Opponents);
            var charge = ev.OfType<ManaChargedEvent>().Single(c => c.Reason == "pincer");
            Assert.Equal(2, charge.Participants); // §3.1.9: a lean two-hero pincer charges at full rate
            Assert.True(charge.Amount > 0);
            Assert.Contains(ev, e => e is DisplacedEvent d && d.To == new Coord(5, 3));
            AssertInvariants(s);
        }

        [Fact]
        public void Gap_or_ally_in_line_breaks_pincer()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 1, 0), ("tank", 3, 0)), ("knight", 0, 2), ("cleric", 4, 0), ("mage", 5, 5));
            // knight to (0,0): line (0,0)…(4,0) has a gap at (2,0) → no pincer
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((0, 1), (0, 0)) });
            Assert.Empty(ev.OfType<PincerFormedEvent>());
        }

        [Fact]
        public void Diagonal_is_not_a_pincer_and_diagonal_step_is_refused()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 1, 1)), ("knight", 0, 0), ("cleric", 3, 3));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((2, 2)) });
            Assert.Contains(ev, e => e is StepBlockedEvent);
            Assert.Empty(ev.OfType<PincerFormedEvent>());
            Assert.Equal(new Coord(3, 3), Hero(s, "cleric").Pos);
        }

        [Fact]
        public void Off_board_step_stops_the_drag()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 5, 0)), ("knight", 0, 7), ("cleric", 3, 3));
            var k = Hero(s, "knight");
            BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((0, 6), (-1, 6), (-1, 5)) });
            Assert.Equal(new Coord(0, 6), k.Pos);
        }

        [Fact]
        public void Noop_drag_costs_no_time()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 5, 0)), ("knight", 0, 7), ("cleric", 3, 3));
            long clock = s.Clock;
            var k = Hero(s, "knight");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((0, 6), (0, 7)) });
            Assert.Equal(clock, s.Clock);
            Assert.DoesNotContain(ev, e => e is PlayerControlEvent);
        }

        [Fact]
        public void Two_by_two_boss_blocks_slides_and_is_pincered_across_its_width()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("boss", 1, 1)), ("knight", 0, 1), ("cleric", 4, 1));
            var boss = s.Units.Single(u => u.Def.Id == "boss");
            // slide into the boss is blocked
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((1, 1)) });
            Assert.Contains(ev, e => e is StepBlockedEvent b && b.Reason == "immovable");
            Assert.Equal(new Coord(1, 1), boss.Pos);
            // cleric steps west to (3,1): knight(0,1) … boss covers (1,1),(2,1) … cleric(3,1) → one opponent
            ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 1)) });
            var p = ev.OfType<PincerFormedEvent>().Single();
            Assert.Equal(new[] { boss.Id }, p.Opponents);
            AssertInvariants(s);
        }

        [Fact]
        public void Supporters_join_but_dilute_the_mana_charge()
        {
            var db = Mini();
            db.Classes["tank"].BaseStats.Vitality = 30; // a foe the pincer can dent but not kill
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4), ("mage", 1, 4));
            // cleric (3,4) → (3,3): knight(1,3) tank(2,3) cleric(3,3); mage (1,4) adjacent to knight = supporter
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            var p = ev.OfType<PincerFormedEvent>().Single();
            Assert.Contains(Hero(s, "mage").Id, p.Supporters);
            var charge = ev.OfType<ManaChargedEvent>().Single(c => c.Reason == "pincer");
            Assert.Equal(3, charge.Participants);
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            float share = (tank.MaxHp - tank.Hp) / (float)tank.MaxHp;
            Assert.Equal(System.Math.Max(BattleConfig.MinPincerCharge, (int)System.Math.Round(BattleConfig.PincerChargePerHp * share / 1.5f)), charge.Amount); // ×1.5 divisor for 3 heroes
            Assert.DoesNotContain(ev, e => e is OrbMintedEvent o && o.Reason == "support");
            Assert.Contains(ev, e => e is AttackEvent a && a.IsSupport);
        }

        [Fact]
        public void Chain_pincers_resolve_in_order()
        {
            var db = Mini();
            // knight(0,0) t(1,0) [cleric drops at (2,0)] t(3,0) mage(4,0): two pairs sharing cleric.
            var (s, _) = Battle(db, Stage(("tank", 1, 0), ("tank", 3, 0)), ("knight", 0, 0), ("cleric", 2, 1), ("mage", 4, 0));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((2, 0)) });
            var ps = ev.OfType<PincerFormedEvent>().ToList();
            Assert.Equal(2, ps.Count);
            Assert.Equal(0, ps[0].ChainIndex);
            Assert.Equal(1, ps[1].ChainIndex);
            var cleric = Hero(s, "cleric").Id;
            Assert.True(ps[0].Attacker1 == cleric || ps[0].Attacker2 == cleric);
        }

        [Fact]
        public void Killing_last_enemy_of_last_wave_is_victory_with_loot()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("goblin", 1, 0)), ("knight", 0, 0), ("cleric", 2, 1));
            for (int i = 0; i < 6 && !s.IsOver; i++)
            {
                var c = Hero(s, "cleric");
                var path = c.Pos == new Coord(2, 0) ? P((2, 1)) : P((2, 0));
                BattleEngine.Apply(s, new MoveCommand { HeroId = c.Id, Path = path });
                AssertInvariants(s);
            }
            Assert.Equal(BattlePhase.Victory, s.Phase);
            Assert.Equal(3, s.Results.Coins);
            Assert.Equal(1, s.Results.Items["fang"]);
            Assert.Equal(1, s.Results.Kills["goblin"]);
            Assert.True(s.Results.Xp > 0);
        }

        [Fact]
        public void Waves_spawn_in_sequence()
        {
            var db = Mini();
            var stage = StageWaves(new[] { ("goblin", 1, 0) }, new[] { ("goblin", 4, 4) });
            var (s, _) = Battle(db, stage, ("knight", 0, 0), ("cleric", 2, 1));
            for (int i = 0; i < 6 && s.Wave == 0; i++)
            {
                var c = Hero(s, "cleric");
                BattleEngine.Apply(s, new MoveCommand { HeroId = c.Id, Path = c.Pos == new Coord(2, 0) ? P((2, 1)) : P((2, 0)) });
            }
            Assert.Equal(1, s.Wave);
            Assert.False(s.IsOver);
            Assert.NotNull(At(s, 4, 4));
        }

        // ── Physical blocks (§1.1.1) ────────────────────────────────────────────

        [Fact]
        public void A_hero_cannot_step_onto_a_block()
        {
            var db = Mini();
            var stage = Stage(("tank", 5, 0));
            stage.Blocks.Add(new Coord(1, 3));
            var (s, _) = Battle(db, stage, ("knight", 0, 3), ("cleric", 5, 7));
            var knight = Hero(s, "knight");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((1, 3)) });
            Assert.Contains(ev, e => e is StepBlockedEvent b && b.Reason == "block");
            Assert.Equal(new Coord(0, 3), knight.Pos);
        }

        [Fact]
        public void A_block_in_the_line_breaks_the_pincer()
        {
            var db = Mini();
            var stage = Stage(("goblin", 3, 3));
            stage.Blocks.Add(new Coord(4, 3));
            var (s, _) = Battle(db, stage, ("cleric", 2, 3), ("knight", 5, 2));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((5, 3)) });
            Assert.DoesNotContain(ev, e => e is PincerFormedEvent);
        }

        [Fact]
        public void Nobody_spawns_or_walks_onto_blocks()
        {
            var db = Mini();
            for (int seed = 0; seed < 40; seed++)
            {
                var stage = Stage(("wolf", -1, -1), ("wolf", -1, -1), ("wolf", -1, -1), ("wolf", -1, -1));
                for (int x = 0; x < 6; x++) if (x != 2) stage.Blocks.Add(new Coord(x, 2));
                var setup = new BattleSetup { Stage = stage, Seed = seed, UnlockedSlots = 2 };
                setup.Party.Add(new HeroSpec { ClassId = "knight" });
                setup.Party.Add(new HeroSpec { ClassId = "cleric" });
                var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
                for (int i = 0; i < 6 && !s.IsOver; i++)
                {
                    BattleEngine.Apply(s, new ShieldCommand());
                    Assert.All(s.Units.Where(u => u.Alive), u => Assert.False(s.IsBlock(u.Pos), $"{u.Name} on a block at {u.Pos}"));
                }
            }
        }

        [Fact]
        public void Real_stages_carry_their_blocks()
        {
            Assert.True(Real.Stages["cave-03"].Blocks.Count >= 2);
            Assert.All(Real.Stages.Values.SelectMany(st => st.Blocks), b => Assert.True(b.Y <= 5, "blocks stay clear of the hero start rows"));
        }
    }
}
