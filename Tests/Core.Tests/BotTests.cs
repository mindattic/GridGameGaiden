using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    public class BotTests
    {
        // ── HeroBot.LPaths ──────────────────────────────────────────────────

        [Fact]
        public void LPaths_yields_one_path_for_a_straight_line()
        {
            var paths = HeroBot.LPaths(new Coord(1, 2), new Coord(4, 2)).ToList();
            Assert.Single(paths);
            Assert.Equal(new List<Coord> { new Coord(2, 2), new Coord(3, 2), new Coord(4, 2) }, paths[0]);
        }

        [Fact]
        public void LPaths_yields_both_L_orders_for_a_diagonal_destination()
        {
            var from = new Coord(1, 1);
            var to = new Coord(3, 4);
            var paths = HeroBot.LPaths(from, to).ToList();
            Assert.Equal(2, paths.Count);
            Assert.All(paths, p => Assert.Equal(to, p[p.Count - 1]));
            // First order moves X before Y: it turns the corner at (to.X, from.Y).
            Assert.Contains(new Coord(to.X, from.Y), paths[0]);
            Assert.DoesNotContain(new Coord(from.X, to.Y), paths[0]);
            // Second order moves Y before X: it turns the corner at (from.X, to.Y).
            Assert.Contains(new Coord(from.X, to.Y), paths[1]);
            Assert.DoesNotContain(new Coord(to.X, from.Y), paths[1]);
        }

        [Fact]
        public void LPaths_yields_nothing_when_from_equals_to()
        {
            var here = new Coord(2, 5);
            var paths = HeroBot.LPaths(here, here).ToList();
            Assert.Single(paths);
            Assert.Empty(paths[0]);
        }

        // ── HeroBot.Decide / BestMove ───────────────────────────────────────

        [Fact]
        public void BestMove_returns_null_when_no_hero_can_move()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            foreach (var h in s.LivingList(Team.Hero)) h.Buffs.Add(new BuffInstance { Def = db.Buffs["sleep"], Remaining = 3 });
            var bot = new HeroBot(1);
            Assert.Null(bot.BestMove(s));
        }

        [Fact]
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

        [Fact]
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
                var cmd = Assert.IsType<MoveCommand>(bot.Decide(s));
                Assert.Equal(knight.Id, cmd.HeroId);
                Assert.Equal(new List<Coord> { new Coord(4, 3) }, cmd.Path);
            }
        }

        [Fact]
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
                    Assert.True(BattleEngine.CanMove(s, mc.HeroId), "move command on a hero who cannot move");
                    var pv = BattleEngine.PreviewMove(s, mc.HeroId, mc.Path);
                    Assert.Equal(mc.Path.Count, pv.StepsTaken.Count);
                    break;
                case UseSlotCommand uc:
                    Assert.Equal(SlotStatus.Ready, BattleEngine.GetSlotStatus(s, uc.HeroId, uc.Slot));
                    var hero = s.UnitById(uc.HeroId);
                    var ability = BattleEngine.SlotAbility(s, hero, uc.Slot);
                    if (ability.Mode != TargetMode.Auto)
                        Assert.Contains(uc.Target, BattleEngine.ValidTargets(s, uc.HeroId, uc.Slot));
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
            Assert.Equal(a.GetType(), b.GetType());
            switch (a)
            {
                case MoveCommand ma when b is MoveCommand mb:
                    Assert.Equal(ma.HeroId, mb.HeroId);
                    Assert.Equal(ma.Path, mb.Path);
                    break;
                case UseSlotCommand ua when b is UseSlotCommand ub:
                    Assert.Equal(ua.HeroId, ub.HeroId);
                    Assert.Equal(ua.Slot, ub.Slot);
                    Assert.Equal(ua.Target, ub.Target);
                    break;
                case ShieldCommand _ when b is ShieldCommand _:
                    break;
                default:
                    Assert.Fail($"Mismatched commands {a} vs {b}");
                    break;
            }
        }

        // ── MetaHeuristics ──────────────────────────────────────────────────

        [Fact]
        public void Score_of_null_item_is_zero()
        {
            Assert.Equal(0f, MetaHeuristics.Score(null));
        }

        [Fact]
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
            Assert.Equal(41f, MetaHeuristics.Score(item));
        }

        [Fact]
        public void BestFirst_orders_items_by_descending_score()
        {
            var weak = new ItemDef { Id = "weak", Name = "Weak", Type = ItemType.Equipment, Stats = new Stats { Strength = 1 } };
            var strong = new ItemDef { Id = "strong", Name = "Strong", Type = ItemType.Equipment, Stats = new Stats { Strength = 10 } };
            var mid = new ItemDef { Id = "mid", Name = "Mid", Type = ItemType.Equipment, Stats = new Stats { Strength = 5 } };
            var ordered = MetaHeuristics.BestFirst(new[] { weak, strong, mid }).ToList();
            Assert.Equal(new[] { strong, mid, weak }, ordered);
        }
    }
}
