using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;
using Gaiden.Meta;
using NUnit.Framework;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>Test Mode (`-testmode`, GG-T93): enemy level follows the party on a test save and is unchanged
    /// otherwise; stage picking keeps the loop cycling; the headless loop runs battle → results → camp.</summary>
    public class TestModeTests
    {
        private static void SetPartyLevel(SaveData s, int level)
        {
            foreach (var id in s.Party) s.Hero(id).TotalXp = Formulas.TotalXpForLevel(level);
        }

        private static List<int> SpawnedEnemyLevels(SaveData s, string stageId)
        {
            var st = BattleEngine.Create(Real, BattleFactory.Build(Real, s, stageId, 11), new List<BattleEvent>());
            return st.Units.Where(u => !u.IsHero && u.Team == Team.Enemy).Select(u => u.Level).ToList();
        }

        [Test]
        public void Party_level_is_the_rounded_average_of_the_party()
        {
            var s = Progression.NewGame(Real, "PL");
            Assert.That(Progression.PartyLevel(s), Is.EqualTo(1));
            SetPartyLevel(s, 6);
            s.Hero(s.Party[0]).TotalXp = Formulas.TotalXpForLevel(11); // one hero at 11, four at 6 → 7
            Assert.That(Progression.PartyLevel(s), Is.EqualTo(7));
        }

        [Test]
        public void In_test_mode_enemy_level_rises_with_party_level()
        {
            var s = TestLoop.NewSave(Real);
            string stage = Real.CampaignStages()[0].Id;
            SetPartyLevel(s, 1);
            var low = SpawnedEnemyLevels(s, stage);
            int lowRec = BattleFactory.Build(Real, s, stage, 1).RecommendedLevel;
            SetPartyLevel(s, 12);
            var high = SpawnedEnemyLevels(s, stage);
            Assert.That(BattleFactory.Build(Real, s, stage, 1).RecommendedLevel, Is.EqualTo(12).And.GreaterThan(lowRec));
            Assert.That(low, Is.Not.Empty);
            Assert.That(high.Min(), Is.GreaterThanOrEqualTo(12), "spawns stay max(authored, party level)");
            Assert.That(high.Max(), Is.GreaterThan(low.Max()));
            Assert.That(TestLoop.EnemyLevels(Real, s, stage).max, Is.EqualTo(high.Max()));
        }

        [Test]
        public void Outside_test_mode_enemy_level_ignores_party_level()
        {
            var s = Progression.NewGame(Real, "Normal");
            var stages = Real.CampaignStages();
            int idx = stages.FindIndex(x => x.LevelOverride == null);
            string stage = stages[idx].Id;
            SetPartyLevel(s, 1);
            var low = SpawnedEnemyLevels(s, stage);
            SetPartyLevel(s, 12);
            var high = SpawnedEnemyLevels(s, stage);
            Assert.That(BattleFactory.Build(Real, s, stage, 1).RecommendedLevel, Is.EqualTo(Progression.RecommendedLevel(idx)));
            Assert.That(high.OrderBy(x => x), Is.EqualTo(low.OrderBy(x => x)));
        }

        [Test]
        public void New_test_save_skips_tutorial_and_crawls()
        {
            var s = TestLoop.NewSave(Real);
            Assert.That(s.TestMode && s.TutorialDone, Is.True);
            Assert.That(Real.Campaign.Themes.All(t => s.SeenCrawls.Contains(t.Id)), Is.True);
            Assert.That(s.Party, Is.EquivalentTo(Progression.StartingRoster));
        }

        [Test]
        public void Stage_pick_follows_the_expedition_then_the_highest_unlocked_theme_and_steps_back_on_a_wall()
        {
            var c = Real; var s = TestLoop.NewSave(c);
            var themes = c.Campaign.Themes;
            Assert.That(TestLoop.PickStage(c, s), Is.EqualTo(themes[0].Stages[0]));
            // Clear the first theme: the next theme's battle 1 becomes the pick.
            s.HighestClearedStage = themes[0].Stages.Count - 1;
            Assert.That(TestLoop.PickStage(c, s), Is.EqualTo(themes[1].Stages[0]));
            Assert.That(TestLoop.PickStage(c, s, TestLoop.LossesBeforeStepBack), Is.EqualTo(themes[0].Stages[0]));
            Assert.That(TestLoop.PickStage(c, s, 100), Is.EqualTo(themes[0].Stages[0]), "never steps back past the first theme");
            // Mid-expedition, the next battle of that expedition wins over everything.
            Expeditions.Begin(c, s, themes[1].Stages[0]);
            s.Expedition.Next = 1;
            Assert.That(TestLoop.PickStage(c, s, 100), Is.EqualTo(themes[1].Stages[1]));
        }

        [Test]
        public void Headless_loop_runs_battle_results_and_camp_every_cycle()
        {
            var c = Real; var s = TestLoop.NewSave(c);
            int losses = 0, seed = 93;
            var lines = new List<TestCycle>();
            for (int n = 1; n <= 4; n++)
            {
                string stageId = TestLoop.PickStage(c, s, losses);
                Expeditions.Begin(c, s, stageId);
                var cycle = TestCycle.Begin(c, s, stageId, n);
                Assert.That(cycle.EnemyMin, Is.GreaterThanOrEqualTo(cycle.PartyLevel));
                var st = BotRunner.Play(c, BattleFactory.Build(c, s, stageId, seed++), new HeroBot(seed), 900);
                var report = BattleFactory.Commit(c, s, st);
                cycle.Battle(c, s, report);
                losses = report.Victory ? 0 : losses + 1;
                cycle.RunCamp(c, s);
                lines.Add(cycle);
                TestContext.Out.WriteLine(cycle.ToString());
                Assert.That(cycle.ToString(), Does.StartWith($"[TestMode] cycle {n} | stage {stageId}"));
                Assert.That(cycle.ToString(), Does.Contain("| camp gold"));
            }
            Assert.That(lines.Any(l => l.Report.XpEach > 0), Is.True, "the loop earns XP");
            Assert.That(lines.Last().PartyLevelAfter, Is.GreaterThan(lines.First().PartyLevel), "the party levels over the run");
            Assert.That(lines.Last().EnemyMax, Is.GreaterThan(lines.First().EnemyMax), "enemy level climbs with it");
        }
    }
}
