using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;
using Xunit;
using Xunit.Abstractions;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    public class ContentAndSoakTests
    {
        private readonly ITestOutputHelper output;
        public ContentAndSoakTests(ITestOutputHelper output) { this.output = output; }

        [Fact]
        public void Real_content_loads_and_cross_references_resolve()
        {
            var db = Real;
            Assert.NotEmpty(db.Classes);
            Assert.NotEmpty(db.Stages);
            var problems = ContentLoader.Validate(db);
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        public static List<HeroSpec> StarterParty(ContentDb db, int level)
        {
            var party = new List<HeroSpec>();
            foreach (var id in new[] { "paladin", "barbarian", "cleric" })
            {
                var def = db.Class(id);
                var spec = new HeroSpec { ClassId = id, Level = level, Bar = new List<string>(def.DefaultLoadout) };
                foreach (var b in spec.Bar.Where(b => b.StartsWith("item:"))) spec.ItemCounts[b.Substring(5).Split(':')[0]] = 3;
                party.Add(spec);
            }
            return party;
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.6f)]
        public void Bots_play_every_stage_without_breaking_invariants(float randomness)
        {
            var db = Real;
            var report = new StringBuilder();
            int total = 0, wins = 0, stalls = 0;
            var stages = db.StageOrder;
            int index = 0;
            foreach (var stage in stages)
            {
                index++;
                int stageWins = 0, stageRuns = 0, actions = 0;
                for (int seed = 1; seed <= 12; seed++)
                {
                    int level = stage.IsTest ? 5 : System.Math.Max(1, index);
                    var setup = new BattleSetup { Stage = stage, Seed = seed * 7919 + index, UnlockedSlots = 5, RecommendedLevel = stage.IsTest ? 1 : index, Party = StarterParty(db, level) };
                    var bot = new HeroBot(seed, randomness);
                    var s = BotRunner.Play(db, setup, bot, 300, (cmd, ev, st) => AssertInvariants(st));
                    stageRuns++; total++;
                    if (s.Phase == BattlePhase.Victory) { stageWins++; wins++; }
                    if (!s.IsOver) stalls++;
                    actions += s.Results.ActionsTaken;
                }
                report.AppendLine($"{stage.Id,-22} win {stageWins,2}/{stageRuns}  avg actions {actions / (float)stageRuns:F1}");
            }
            report.AppendLine($"TOTAL win {wins}/{total}, unfinished {stalls}");
            output.WriteLine(report.ToString());
        }

        [Fact]
        public void Battles_replay_identically_from_seed()
        {
            var db = Real;
            var stage = db.StageOrder.First(s => !s.IsTest);
            string Run()
            {
                var sb = new StringBuilder();
                var setup = new BattleSetup { Stage = stage, Seed = 42, UnlockedSlots = 5, Party = StarterParty(db, 3) };
                BotRunner.Play(db, setup, new HeroBot(5), 120, (cmd, ev, st) => { sb.AppendLine(cmd?.ToString()); foreach (var e in ev) sb.AppendLine(e.ToString()); });
                return sb.ToString();
            }
            Assert.Equal(Run(), Run());
        }
    }
}
