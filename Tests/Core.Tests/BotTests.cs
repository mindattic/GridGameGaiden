using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;
using NUnit.Framework;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    public class BotTests
    {
        // ── HeroBot.LPaths ──────────────────────────────────────────────────

        [Test]
        public void LPaths_yields_one_path_for_a_straight_line()
        {
            var paths = HeroBot.LPaths(new Coord(1, 2), new Coord(4, 2)).ToList();
            Assert.That(paths, Has.Exactly(1).Items);
            Assert.That(paths[0], Is.EqualTo(new List<Coord> { new Coord(2, 2), new Coord(3, 2), new Coord(4, 2) }));
        }

        [Test]
        public void LPaths_yields_both_L_orders_for_a_diagonal_destination()
        {
            var from = new Coord(1, 1);
            var to = new Coord(3, 4);
            var paths = HeroBot.LPaths(from, to).ToList();
            Assert.That(paths.Count, Is.EqualTo(2));
            foreach (var p in paths) Assert.That(p[p.Count - 1], Is.EqualTo(to));
            // First order moves X before Y: it turns the corner at (to.X, from.Y).
            Assert.That(paths[0], Does.Contain(new Coord(to.X, from.Y)));
            Assert.That(paths[0], Does.Not.Contain(new Coord(from.X, to.Y)));
            // Second order moves Y before X: it turns the corner at (from.X, to.Y).
            Assert.That(paths[1], Does.Contain(new Coord(from.X, to.Y)));
            Assert.That(paths[1], Does.Not.Contain(new Coord(to.X, from.Y)));
        }

        [Test]
        public void LPaths_yields_nothing_when_from_equals_to()
        {
            var here = new Coord(2, 5);
            var paths = HeroBot.LPaths(here, here).ToList();
            Assert.That(paths, Has.Exactly(1).Items);
            Assert.That(paths[0], Is.Empty);
        }

        // ── HeroBot.Decide / BestMove ───────────────────────────────────────

        [Test]
        public void BestMove_returns_null_when_no_hero_can_move()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            foreach (var h in s.LivingList(Team.Hero)) h.Buffs.Add(new BuffInstance { Def = db.Buffs["sleep"], Remaining = 3 });
            var bot = new HeroBot(1);
            Assert.That(bot.BestMove(s), Is.Null);
        }

        [Test]
        public void Decide_is_deterministic_for_a_fixed_seed_and_battle_state()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 2, 2), ("tank", 4, 5)), ("knight", 0, 0), ("mage", 5, 7));
            var mage = Hero(s, "mage");
            mage.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            s.Bank.Add(2);

            var bot1 = new HeroBot(12345, randomness: 0f) { SkillLevel = 50f };
            var bot2 = new HeroBot(12345, randomness: 0f) { SkillLevel = 50f };
            var cmd1 = bot1.Decide(s);
            var cmd2 = bot2.Decide(s);
            AssertSameCommand(cmd1, cmd2);
        }

        [Test]
        public void SkillLevel_100_always_picks_the_clearly_best_move()
        {
            // bible §1.4 setup: C(2,3) g(3,3) g(4,3) K(5,3) — dragging K to (4,3) shoves the far goblin
            // and pincers the near one with the cleric, a huge score lead over any other destination.
            var db = Mini();
            var (s, _) = Battle(db, Stage(("goblin", 3, 3), ("goblin", 4, 3)), ("cleric", 2, 3), ("knight", 5, 3));
            var cleric = Hero(s, "cleric");
            cleric.Buffs.Add(new BuffInstance { Def = db.Buffs["sleep"], Remaining = 3 }); // only the knight may move
            var knight = Hero(s, "knight");

            for (int seed = 1; seed <= 10; seed++)
            {
                var bot = new HeroBot(seed) { SkillLevel = 100f };
                var decided = bot.Decide(s);
                Assert.That(decided, Is.TypeOf<MoveCommand>());
                var cmd = (MoveCommand)decided;
                Assert.That(cmd.HeroId, Is.EqualTo(knight.Id));
                Assert.That(cmd.Path, Is.EqualTo(new List<Coord> { new Coord(4, 3) }));
            }
        }

        [Test]
        public void Randomness_and_softened_skill_still_only_return_legal_commands()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 2, 2), ("tank", 3, 5)), ("knight", 0, 0), ("mage", 5, 7));
            var mage = Hero(s, "mage");
            mage.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            s.Bank.Add(6);

            for (int seed = 1; seed <= 40; seed++)
            {
                var bot = new HeroBot(seed, randomness: 1f) { SkillLevel = 30f };
                var cmd = bot.Decide(s);
                AssertLegal(s, cmd);
            }
        }

        private static void AssertLegal(BattleState s, BattleCommand cmd)
        {
            switch (cmd)
            {
                case MoveCommand mc:
                    Assert.That(BattleEngine.CanMove(s, mc.HeroId), Is.True, "move command on a hero who cannot move");
                    var pv = BattleEngine.PreviewMove(s, mc.HeroId, mc.Path);
                    Assert.That(pv.StepsTaken.Count, Is.EqualTo(mc.Path.Count));
                    break;
                case UseSlotCommand uc:
                    Assert.That(BattleEngine.GetSlotStatus(s, uc.HeroId, uc.Slot), Is.EqualTo(SlotStatus.Ready));
                    var hero = s.UnitById(uc.HeroId);
                    var ability = BattleEngine.SlotAbility(s, hero, uc.Slot);
                    if (ability.Mode != TargetMode.Auto)
                        Assert.That(BattleEngine.ValidTargets(s, uc.HeroId, uc.Slot), Does.Contain(uc.Target));
                    break;
                case ShieldCommand _:
                    break;
                default:
                    Assert.Fail($"Unexpected command type {cmd?.GetType()}");
                    break;
            }
        }

        private static void AssertSameCommand(BattleCommand a, BattleCommand b)
        {
            Assert.That(b.GetType(), Is.EqualTo(a.GetType()));
            switch (a)
            {
                case MoveCommand ma when b is MoveCommand mb:
                    Assert.That(mb.HeroId, Is.EqualTo(ma.HeroId));
                    Assert.That(mb.Path, Is.EqualTo(ma.Path));
                    break;
                case UseSlotCommand ua when b is UseSlotCommand ub:
                    Assert.That(ub.HeroId, Is.EqualTo(ua.HeroId));
                    Assert.That(ub.Slot, Is.EqualTo(ua.Slot));
                    Assert.That(ub.Target, Is.EqualTo(ua.Target));
                    break;
                case ShieldCommand _ when b is ShieldCommand _:
                    break;
                default:
                    Assert.Fail($"Mismatched commands {a} vs {b}");
                    break;
            }
        }

        // ── MetaHeuristics ──────────────────────────────────────────────────

        [Test]
        public void Score_of_null_item_is_zero()
        {
            Assert.That(MetaHeuristics.Score(null), Is.EqualTo(0f));
        }

        [Test]
        public void Score_sums_stats_plus_half_base_damage()
        {
            var item = new ItemDef
            {
                Id = "blade",
                Name = "Blade",
                Type = ItemType.Equipment,
                BaseDamage = 10,
                Stats = new Stats { Strength = 3, Vitality = 2, Agility = 1, Speed = 4, Intelligence = 5, Wisdom = 6, Luck = 7, Stamina = 8 },
            };
            // 3+2+1+4+5+6+7+8 = 36, plus half of 10 base damage = 41.
            Assert.That(MetaHeuristics.Score(item), Is.EqualTo(41f));
        }

        [Test]
        public void BestFirst_orders_items_by_descending_score()
        {
            var weak = new ItemDef { Id = "weak", Name = "Weak", Type = ItemType.Equipment, Stats = new Stats { Strength = 1 } };
            var strong = new ItemDef { Id = "strong", Name = "Strong", Type = ItemType.Equipment, Stats = new Stats { Strength = 10 } };
            var mid = new ItemDef { Id = "mid", Name = "Mid", Type = ItemType.Equipment, Stats = new Stats { Strength = 5 } };
            var ordered = MetaHeuristics.BestFirst(new[] { weak, strong, mid }).ToList();
            Assert.That(ordered, Is.EqualTo(new[] { strong, mid, weak }));
        }
    }
}
