using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;
using Gaiden.Meta;
using NUnit.Framework;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>
    /// GG-T97: there can never be two actors occupying the same tile once a slide is over (owner 2026-10-19). The
    /// whole suite runs with <see cref="BattleEngine.StrictTiles"/> on, so any conflict anywhere throws; these
    /// tests aim one at every path that writes a position or adds a unit, at its worst case.
    /// </summary>
    [Parallelizable(ParallelScope.Children)]
    public class TileOccupancyTests
    {
        private static Unit Enemy(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        /// <summary>Blocks on every tile except <paramref name="open"/> — a deliberately crowded board.</summary>
        private static void OnlyOpen(StageDef st, params (int x, int y)[] open)
        {
            var keep = new HashSet<Coord>(open.Select(o => new Coord(o.x, o.y)));
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                    if (!keep.Contains(new Coord(x, y))) st.Blocks.Add(new Coord(x, y));
        }

        private static void AssertBoardClean(BattleState s)
        {
            var seen = new Dictionary<Coord, Unit>();
            foreach (var u in s.Units.Where(u => u.Alive))
                foreach (var t in u.Tiles())
                {
                    Assert.That(s.Walkable(t), Is.True, $"{u} on a non-walkable tile {t}");
                    Assert.That(seen.ContainsKey(t), Is.False, $"{u} and {(seen.TryGetValue(t, out var o) ? o : null)} share {t}");
                    seen[t] = u;
                }
            foreach (var m in s.Movables)
                Assert.That(seen.ContainsKey(m.Pos), Is.False, $"movable under {(seen.TryGetValue(m.Pos, out var o) ? o : null)} at {m.Pos}");
        }

        // ── The net itself ──

        [Test]
        public void Strict_mode_throws_on_a_tile_conflict_instead_of_healing_it()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 3, 3)), ("knight", 0, 0), ("cleric", 5, 7));
            Enemy(s, "tank").Pos = new Coord(0, 0); // corrupt the board by hand: something the engine itself never does
            Assert.Throws<TileConflictException>(() => BattleEngine.Apply(s, new ShieldCommand()));
        }

        [Test]
        public void Release_mode_still_heals_a_conflict_loudly_as_a_last_resort()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 3, 3)), ("knight", 0, 0), ("cleric", 5, 7));
            s.StrictTilesOverride = false;
            var tank = Enemy(s, "tank");
            tank.Pos = new Coord(0, 0);
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            var conflict = ev.OfType<TileConflictEvent>().Single();
            Assert.That(conflict.UnitId, Is.EqualTo(tank.Id), "the newer unit is the one nudged");
            Assert.That(ev.OfType<DisplacedEvent>().Any(d => d.UnitId == tank.Id && d.From == new Coord(0, 0)), Is.True);
            AssertBoardClean(s);
        }

        [Test]
        public void Release_mode_heals_a_unit_standing_on_the_amplifier()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 3, 3)), ("knight", 0, 0), ("cleric", 5, 7));
            s.StrictTilesOverride = false;
            s.Amplifier = new Coord(0, 0);
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(ev.OfType<TileConflictEvent>().Single().OtherUnitId, Is.EqualTo(-1));
            AssertBoardClean(s);
        }

        // ── Hero drag + displacement ──

        [Test]
        public void A_long_drag_through_a_crowd_swaps_each_occupant_into_the_tile_just_left()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("tank", 1, 0), ("tank", 2, 0), ("tank", 3, 0)), ("knight", 0, 0), ("cleric", 4, 0), ("mage", 5, 7));
            s.Amplifier = new Coord(5, 0);
            var knight = Hero(s, "knight");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (5, 1)) });
            AssertBoardClean(s);
            Assert.That(knight.Pos, Is.EqualTo(new Coord(5, 1)));
            Assert.That(ev.OfType<DisplacedEvent>().Count(), Is.EqualTo(4)); // three tanks and the cleric each took the tile just left
            Assert.That(s.Amplifier, Is.EqualTo(new Coord(4, 0)));
        }

        // ── Knockback ──

        private static (BattleState s, Unit knight) Bash(ContentDb db, StageDef stage)
        {
            var (s, _) = Battle(db, stage, ("knight", 2, 4), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = "shield_bash" };
            return (s, k);
        }

        [TestCase("unit")]
        [TestCase("edge")]
        [TestCase("block")]
        [TestCase("amp-then-block")]
        [TestCase("amp-free")]
        public void Knockback_into_anything_never_lands_on_it(string into)
        {
            var db = Mini();
            var st = Stage(("tank", 2, 3));
            if (into == "unit") st.Waves[0].Enemies.Add(new SpawnDef { Class = "tank", Level = 1, X = 2, Y = 2 });
            if (into == "block" || into == "amp-then-block") st.Blocks.Add(new Coord(2, 1));
            var (s, k) = Bash(db, st);
            var tank = s.Units.First(u => u.Alive && u.Pos == new Coord(2, 3));
            if (into == "edge") { tank.Pos = new Coord(2, 0); k.Pos = new Coord(2, 1); }
            if (into.StartsWith("amp")) s.Amplifier = new Coord(2, 2);
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = tank.Pos });
            AssertBoardClean(s);
            if (into == "amp-free" && tank.Alive) { Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 1))); Assert.That(s.Amplifier, Is.EqualTo(new Coord(2, 0))); }
            if (into == "amp-then-block" && tank.Alive) { Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 3))); Assert.That(s.Amplifier, Is.EqualTo(new Coord(2, 2))); }
        }

        // ── 2×2 footprint steps and VacatedTile shoves ──

        [TestCase(false)]
        [TestCase(true)]
        public void A_two_by_two_shoving_a_crowded_lane_sends_each_occupant_to_its_own_vacated_tile(bool ampInLane)
        {
            var db = Mini();
            db.Classes["boss"].MoveRange = 1;
            var st = Stage(("boss", 0, 0));
            st.Blocks.Add(new Coord(0, 2)); st.Blocks.Add(new Coord(1, 2)); // the only way out is east, through the heroes
            var (s, _) = ampInLane
                ? Battle(db, st, ("knight", 2, 0), ("mage", 3, 1), ("cleric", 5, 7))
                : Battle(db, st, ("knight", 2, 0), ("cleric", 2, 1), ("mage", 3, 1));
            if (ampInLane) s.Amplifier = new Coord(2, 1);
            var boss = Enemy(s, "boss");
            s.TurnEntryOf(boss.Id).Ip = 999;
            BattleEngine.Apply(s, new ShieldCommand());
            AssertBoardClean(s);
            Assert.That(boss.Pos, Is.EqualTo(new Coord(1, 0)));
            Assert.That(Hero(s, "knight").Pos, Is.EqualTo(new Coord(0, 0)));
            if (ampInLane) Assert.That(s.Amplifier, Is.EqualTo(new Coord(0, 1)));
            else Assert.That(Hero(s, "cleric").Pos, Is.EqualTo(new Coord(0, 1)));
        }

        [Test]
        public void A_two_by_two_never_steps_onto_another_enemy_body()
        {
            var db = Mini();
            db.Classes["boss"].MoveRange = 1;
            var st = Stage(("boss", 0, 0), ("tank", 2, 1));
            st.Blocks.Add(new Coord(0, 2)); st.Blocks.Add(new Coord(1, 2));
            var (s, _) = Battle(db, st, ("knight", 2, 0), ("cleric", 5, 7));
            var boss = Enemy(s, "boss");
            s.TurnEntryOf(boss.Id).Ip = 999;
            BattleEngine.Apply(s, new ShieldCommand());
            AssertBoardClean(s);
            Assert.That(boss.Pos, Is.EqualTo(new Coord(0, 0)), "a tank in the entered lane blocks the step");
        }

        // ── Snake chains ──

        [Test]
        public void A_snake_head_stepping_onto_the_amplifier_shoves_it_behind_its_tail_not_under_a_segment()
        {
            var db = Mini();
            db.Classes["naga"] = new ClassDef { Id = "naga", Name = "Naga", Team = Team.Enemy, Tags = { "Boss" }, BaseStats = new Stats { Strength = 5, Vitality = 20, Speed = 12, Agility = 5 }, SnakeSegments = 3, MoveRange = 1, Targeting = Targeting.Closest };
            var (s, _) = Battle(db, Stage(("naga", 2, 3)), ("knight", 2, 7), ("cleric", 5, 7));
            var head = s.Units.First(u => u.Def.Id == "naga" && u.SnakeIndex == 0);
            var tail = s.Units.Where(u => u.SnakeHeadId == head.Id).OrderBy(u => u.SnakeIndex).Last();
            var tailBefore = tail.Pos;
            s.Amplifier = new Coord(2, 4);
            s.TurnEntryOf(head.Id).Ip = 999;
            BattleEngine.Apply(s, new ShieldCommand());
            AssertBoardClean(s);
            Assert.That(head.Pos, Is.EqualTo(new Coord(2, 4)));
            Assert.That(s.Amplifier, Is.EqualTo(tailBefore));
        }

        // ── Teleports and blinks ──

        [Test]
        public void A_delayed_teleport_onto_a_tile_taken_meanwhile_is_blocked()
        {
            var db = Mini();
            db.Abilities["warp"] = new AbilityDef { Id = "warp", Name = "Warp", Kind = AbilityKind.Spell, Cost = 1, CastTime = 30f, IsTeleport = true, Shape = TargetShape.SingleTile, Mode = TargetMode.PickTile, Filter = TargetFilter.EmptyOnly };
            var (s, _) = Battle(db, Stage(("tank", 0, 0)), ("mage", 5, 7), ("knight", 4, 7));
            var mage = Hero(s, "mage");
            mage.Bar[0] = new AbilitySlot { AbilityId = "warp" };
            s.Bank.Add(3);
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = mage.Id, Slot = 0, Target = new Coord(3, 3) });
            Assert.That(s.CastEntryOf(mage.Id), Is.Not.Null);
            // Meanwhile the knight walks onto the landing tile.
            var knight = Hero(s, "knight");
            BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((4, 6), (4, 5), (4, 4), (4, 3), (3, 3)) });
            Assert.That(knight.Pos, Is.EqualTo(new Coord(3, 3)));
            List<BattleEvent> ev = new List<BattleEvent>();
            for (int i = 0; i < 200 && s.CastEntryOf(mage.Id) != null && !s.IsOver; i++) ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(s.CastEntryOf(mage.Id), Is.Null, "the warp resolved");
            Assert.That(ev.OfType<AnnounceEvent>().Any(a => a.Text == "Blocked"), Is.True);
            AssertBoardClean(s);
            Assert.That(mage.Pos, Is.EqualTo(new Coord(5, 7)));
        }

        [Test]
        public void A_blinker_surfaces_only_on_a_free_tile_beside_its_prey()
        {
            var db = Mini();
            db.Classes["imp"] = new ClassDef { Id = "imp", Name = "Imp", Team = Team.Enemy, Tags = { "Beast" }, BaseStats = new Stats { Strength = 5, Vitality = 20, Speed = 10, Agility = 5 }, MoveRange = 1, Brain = Brain.Blinker };
            // The knight is boxed in on three sides; the one open side is the only legal surface.
            var (s, _) = Battle(db, Stage(("imp", 0, 0), ("tank", 2, 4), ("tank", 4, 4)), ("knight", 3, 4), ("cleric", 3, 5), ("mage", 5, 7));
            s.Amplifier = new Coord(3, 3);
            var imp = Enemy(s, "imp");
            imp.TurnsTaken = 1; // its next turn is a blink turn
            s.TurnEntryOf(imp.Id).Ip = 999;
            BattleEngine.Apply(s, new ShieldCommand());
            AssertBoardClean(s);
            Assert.That(imp.Pos, Is.Not.EqualTo(new Coord(3, 3)), "never onto the amplifier boxed in beside its prey");
        }

        // ── Spawns ──

        [Test]
        public void Party_and_enemies_asking_for_the_same_tiles_all_land_on_their_own()
        {
            var db = Mini();
            var st = Stage(("tank", 0, 0), ("tank", 0, 0), ("boss", 0, 0), ("goblin", 2, 6));
            var setup = new BattleSetup { Stage = st, Seed = 3, UnlockedSlots = 5 };
            foreach (var h in new[] { "knight", "cleric", "mage" }) setup.Party.Add(new HeroSpec { ClassId = h, Start = new Coord(2, 6) });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            AssertBoardClean(s);
            Assert.That(s.Units.Count(u => u.Alive), Is.EqualTo(7));
        }

        [Test]
        public void A_party_bigger_than_the_board_seats_the_extras_out_instead_of_stacking_them()
        {
            var db = Mini();
            var st = Stage(("tank", 0, 0));
            OnlyOpen(st, (0, 0), (1, 0), (2, 0));
            var setup = new BattleSetup { Stage = st, Seed = 3, UnlockedSlots = 5 };
            foreach (var h in new[] { "knight", "cleric", "mage" }) setup.Party.Add(new HeroSpec { ClassId = h, Start = new Coord(1, 0) });
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(1, 0) });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            AssertBoardClean(s);
            Assert.That(s.Units.Count(u => u.IsHero && u.Alive), Is.EqualTo(3));
            Assert.That(s.Units.Count(u => u.IsHero && !u.OnBoard), Is.EqualTo(1));
            Assert.That(s.Units.Any(u => !u.IsHero), Is.False, "no room left for the wave's tank: it is skipped, not stacked");
        }

        [Test]
        public void A_guest_whose_tile_is_taken_joins_on_a_free_one()
        {
            var db = Mini();
            db.Classes["ranger"] = new ClassDef { Id = "ranger", Name = "Ranger", Team = Team.Hero, Tags = { "Humanoid" }, BaseStats = new Stats { Strength = 10, Vitality = 10, Speed = 10, Agility = 10 } };
            var st = Stage(("tank", 0, 0));
            st.Waves[0].Guests.Add(new SpawnDef { Class = "ranger", Level = 1, X = 3, Y = 6 });
            var (s, ev) = Battle(db, st, ("knight", 3, 6), ("cleric", 5, 7));
            AssertBoardClean(s);
            var joined = ev.OfType<GuestJoinedEvent>().Single();
            Assert.That(joined.At, Is.Not.EqualTo(new Coord(3, 6)));
            Assert.That(s.UnitById(joined.UnitId).Pos, Is.EqualTo(joined.At));
        }

        private static StageDef DelayedStage(int delay, params (int x, int y)[] open)
        {
            var st = Stage(("tank", 0, 0));
            st.Waves[0].Enemies.Add(new SpawnDef { Class = "wolf", Level = 1, X = 0, Y = 0, DelayTicks = delay });
            if (open.Length > 0) OnlyOpen(st, open);
            return st;
        }

        [Test]
        public void A_reinforcement_onto_its_own_taken_tile_lands_on_a_free_one_and_reports_where()
        {
            var db = Mini();
            var (s, _) = Battle(db, DelayedStage(5), ("knight", 3, 6), ("cleric", 5, 7));
            List<BattleEvent> ev = null;
            for (int i = 0; i < 5 && !s.IsOver; i++) { ev = BattleEngine.Apply(s, new ShieldCommand()); if (ev.OfType<ReinforcementsArrivedEvent>().Any()) break; }
            var arrived = ev.OfType<ReinforcementsArrivedEvent>().Single();
            var wolf = s.UnitById(arrived.Spawned.Single());
            Assert.That(arrived.SpawnedAt.Single(f => f.UnitId == wolf.Id).At, Is.Not.EqualTo(new Coord(0, 0)), "(0,0) is the tank's");
            AssertBoardClean(s);
        }

        [Test]
        public void A_reinforcement_onto_a_full_board_waits_for_a_free_tile_instead_of_stacking()
        {
            var db = Mini();
            // Three open tiles: the tank and two heroes fill them all.
            var (s, _) = Battle(db, DelayedStage(3, (0, 0), (1, 0), (2, 0)), ("knight", 1, 0), ("cleric", 2, 0));
            var tank = Enemy(s, "tank");
            for (int i = 0; i < 3; i++) BattleEngine.Apply(s, new ShieldCommand());
            AssertBoardClean(s);
            Assert.That(s.Units.Any(u => u.Def.Id == "wolf"), Is.False, "nowhere to stand yet");
            Assert.That(s.PendingSpawns.Count(p => !p.Consumed), Is.EqualTo(1), "it waits, still queued");
            // The tank falls: its tile frees, and the wolf arrives there on the next tick.
            tank.Hp = 0; tank.OnBoard = false; s.Timeline.RemoveAll(e => e.OwnerId == tank.Id);
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            var arrived = ev.OfType<ReinforcementsArrivedEvent>().Single();
            Assert.That(arrived.SpawnedAt.Single().At, Is.EqualTo(new Coord(0, 0)));
            AssertBoardClean(s);
        }

        [Test]
        public void An_egg_is_laid_beside_its_layer_and_the_event_says_where_it_really_is()
        {
            var db = Mini();
            db.Classes["naga"] = new ClassDef { Id = "naga", Name = "Naga", Team = Team.Enemy, Tags = { "Aquatic" }, BaseStats = new Stats { Vitality = 10, Speed = 10 }, MoveRange = 0, LaysEgg = "nagaegg", EggInterval = 1 };
            db.Classes["nagaegg"] = new ClassDef { Id = "nagaegg", Name = "Naga Egg", Team = Team.Enemy, Tags = { "Egg" }, BaseStats = new Stats { Vitality = 2, Speed = 1 }, MoveRange = 0 };
            // Three of the naga's four sides are taken; a trap sits on the fourth's neighbour — the egg must take (3,2).
            var (s, _) = Battle(db, Stage(("naga", 3, 3), ("tank", 2, 3), ("tank", 4, 3)), ("knight", 3, 4), ("cleric", 5, 7));
            var naga = Enemy(s, "naga");
            s.TurnEntryOf(naga.Id).Ip = 999;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            var laid = ev.OfType<EggLaidEvent>().Single();
            Assert.That(laid.At, Is.EqualTo(new Coord(3, 2)));
            Assert.That(s.UnitById(laid.EggId).Pos, Is.EqualTo(laid.At));
            AssertBoardClean(s);
        }

        [Test]
        public void Remains_and_risen_undead_take_only_their_own_tile()
        {
            var db = Mini();
            db.Classes["grave"] = new ClassDef { Id = "grave", Name = "Gravestone", Team = Team.Enemy, Tags = { "Remains" }, BaseStats = new Stats { Vitality = 2, Speed = 40 }, MoveRange = 0 };
            db.Classes["wolf"].Remains = "grave";
            var (s, _) = Battle(db, Stage(("wolf", 3, 3), ("tank", 0, 0)), ("cleric", 2, 3), ("knight", 5, 2));
            Enemy(s, "wolf").Hp = 1;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((5, 3), (4, 3)) });
            Assert.That(ev.OfType<RemainsLeftEvent>().Single().At, Is.EqualTo(new Coord(3, 3)));
            AssertBoardClean(s);
            for (int i = 0; i < 6 && !s.IsOver; i++) { BattleEngine.Apply(s, new ShieldCommand()); AssertBoardClean(s); }
        }

        // ── Revival Charm ──

        private static (BattleState s, Unit knight, Unit cleric) ReviveSetup(StageDef st, (int x, int y) knightAt, (int x, int y) clericAt)
        {
            var db = Mini();
            db.Items["charm"] = new ItemDef { Id = "charm", Name = "Revival Charm", Type = ItemType.Consumable, Revives = true, BaseHealing = 10, MaxStack = 9 };
            var (s, _) = Battle(db, st, ("knight", knightAt.x, knightAt.y), ("cleric", clericAt.x, clericAt.y));
            var knight = Hero(s, "knight");
            knight.Bar[0] = new AbilitySlot { ItemId = "charm", Charges = 2, MaxCharges = 9 };
            var cleric = Hero(s, "cleric");
            cleric.Hp = 0; cleric.OnBoard = false; // fell on its tile
            return (s, knight, cleric);
        }

        [Test]
        public void A_revived_hero_never_returns_onto_the_tile_it_fell_on_if_someone_stands_there()
        {
            var (s, knight, cleric) = ReviveSetup(Stage(("tank", 0, 0)), (2, 3), (3, 3));
            var fellAt = cleric.Pos;
            Enemy(s, "tank").Pos = fellAt; // an enemy walked onto the body's tile
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = knight.Pos });
            Assert.That(cleric.Alive, Is.True);
            Assert.That(cleric.Pos, Is.Not.EqualTo(fellAt));
            AssertBoardClean(s);
        }

        [Test]
        public void A_revival_charm_on_a_full_board_is_refused_and_not_spent()
        {
            var st = Stage(("tank", 0, 0));
            OnlyOpen(st, (0, 0), (1, 0), (2, 0));
            var (s, knight, cleric) = ReviveSetup(st, (1, 0), (2, 0));
            s.Units.First(u => u.Def.Id == "tank").Pos = new Coord(0, 0);
            // The board's last free tile (the cleric's) is taken by a second tank.
            var filler = Enemy(s, "tank").Clone(); filler.Id = s.NextUnitId++; filler.Pos = new Coord(2, 0); s.Units.Add(filler);
            int actions = s.Results.ActionsTaken;
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = knight.Pos });
            Assert.That(ev.OfType<AbilityRefusedEvent>().Any(r => r.Reason == "No room!"), Is.True);
            Assert.That(knight.Bar[0].Charges, Is.EqualTo(2));
            Assert.That(s.Results.ActionsTaken, Is.EqualTo(actions));
            Assert.That(cleric.Alive, Is.False);
            AssertBoardClean(s);
        }

        // ── The bot soak, strict, long and wide ──

        private static IEnumerable<TestCaseData> SoakStages()
        {
            var stages = Real.StageOrder;
            for (int i = 0; i < stages.Count; i++) yield return new TestCaseData(stages[i].Id, i + 1).SetName($"Strict_soak_{stages[i].Id}");
        }

        /// <summary>
        /// Every stage, 24 seeds, two bot temperaments, up to 1500 actions a battle (the old soak capped at 300; a
        /// -demo run once needed 412) with <see cref="BattleEngine.StrictTiles"/> on, so a conflict at any enemy turn,
        /// cast or reinforcement inside a command throws right there.
        /// </summary>
        [TestCaseSource(nameof(SoakStages))]
        public void Strict_bot_soak_never_puts_two_actors_on_one_tile(string stageId, int index)
        {
            var db = Real;
            var stage = db.Stages[stageId];
            var report = new StringBuilder();
            foreach (var randomness in new[] { 0f, 0.6f })
                for (int seed = 1; seed <= 24; seed++)
                {
                    int level = stage.IsTest ? 5 : System.Math.Max(1, index);
                    var setup = new BattleSetup { Stage = stage, Seed = seed * 104729 + index * 31 + (randomness > 0 ? 7 : 0), UnlockedSlots = 5, RecommendedLevel = stage.IsTest ? 1 : index, Party = ContentAndSoakTests.StarterParty(db, level) };
                    var s = BotRunner.Play(db, setup, new HeroBot(seed, randomness), 1500, (cmd, ev, st) =>
                    {
                        Assert.That(ev.OfType<TileConflictEvent>().Any(), Is.False);
                        AssertInvariants(st);
                    });
                    report.Append(s.Phase.ToString()[0]);
                }
            TestContext.Out.WriteLine($"{stageId}: {report}");
        }

        [Test]
        public void Strict_bot_soak_survives_long_crucible_runs()
        {
            var c = Real;
            var save = Progression.NewGame(c, "Soak");
            save.HighestClearedStage = c.CampaignStages().Count - 1;
            foreach (var h in save.Roster) h.TotalXp = Formulas.TotalXpForLevel(20);
            for (int run = 0; run < 6; run++)
            {
                var stage = Crucible.Generate(c, save, 900 + run);
                var setup = new BattleSetup { Stage = stage, Seed = 77 + run, UnlockedSlots = 5, RecommendedLevel = 1, Party = ContentAndSoakTests.StarterParty(c, 20) };
                BotRunner.Play(c, setup, new HeroBot(run, 0.3f), 2500, (cmd, ev, st) => AssertInvariants(st));
            }
        }
    }
}
