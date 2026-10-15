using System.IO;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    public class EncounterGeneratorTests
    {
        // A fresh, independent load (not the shared TestKit.Real singleton) so tests that register the
        // generated stage into a ContentDb don't leak it into other tests sharing that cache.
        private static ContentDb FreshReal() => ContentLoader.Load(name =>
        {
            var p = Path.Combine(ContentDir, name + ".json");
            return File.Exists(p) ? File.ReadAllText(p) : null;
        });

        [Fact]
        public void Same_seed_gives_the_same_encounter()
        {
            var db = Real;
            StageDef Gen() => EncounterGenerator.Generate(db, 12345);
            var a = Gen();
            var b = Gen();

            Assert.Equal(a.Id, b.Id);
            Assert.Equal(a.Theme, b.Theme);
            Assert.Equal(a.Blocks.Select(c => (c.X, c.Y)), b.Blocks.Select(c => (c.X, c.Y)));
            Assert.Equal(a.Waves.Count, b.Waves.Count);
            for (int i = 0; i < a.Waves.Count; i++)
            {
                Assert.Equal(a.Waves[i].Enemies.Select(e => (e.Class, e.Level)), b.Waves[i].Enemies.Select(e => (e.Class, e.Level)));
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(999)]
        [InlineData(-7)]
        public void Different_seeds_still_produce_five_waves_one_family_each(int seed)
        {
            var db = Real;
            var stage = EncounterGenerator.Generate(db, seed);

            Assert.Equal(EncounterGenerator.WaveCount, stage.Waves.Count);
            Assert.All(stage.Waves, w =>
            {
                Assert.NotEmpty(w.Enemies);
                var families = w.Enemies.Select(e => db.Class(e.Class).DropTable).Distinct().ToList();
                Assert.True(families.Count == 1, $"wave mixes families: {string.Join(", ", families)}");
            });
        }

        [Fact]
        public void Families_vary_across_the_five_waves()
        {
            var db = Real;
            var stage = EncounterGenerator.Generate(db, 55);
            var familyPerWave = stage.Waves.Select(w => db.Class(w.Enemies[0].Class).DropTable).ToList();
            Assert.Equal(familyPerWave.Count, familyPerWave.Distinct().Count());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(999)]
        public void Levels_are_inside_the_campaign_band_and_rise_across_waves(int seed)
        {
            var db = Real;
            var stage = EncounterGenerator.Generate(db, seed);
            int campaignMax = db.CampaignStages().Count;

            var levels = stage.Waves.Select(w => w.Enemies[0].Level).ToList();
            Assert.All(levels, lvl => Assert.InRange(lvl, 1, campaignMax));
            for (int i = 1; i < levels.Count; i++) Assert.True(levels[i] >= levels[i - 1], "levels must not fall from wave to wave");
            Assert.InRange(stage.LevelOverride ?? 0, 1, campaignMax);
        }

        [Fact]
        public void Generated_encounter_passes_ContentLoader_Validate()
        {
            var db = FreshReal();
            var stage = EncounterGenerator.Generate(db, 777);
            db.Stages[stage.Id] = stage;

            var problems = ContentLoader.Validate(db);
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void Generated_encounter_is_playable_without_breaking_invariants()
        {
            var db = FreshReal();
            var stage = EncounterGenerator.Generate(db, 2026);
            db.Stages[stage.Id] = stage;

            var setup = new BattleSetup
            {
                Stage = stage,
                Seed = 11,
                UnlockedSlots = 5,
                RecommendedLevel = stage.LevelOverride ?? 1,
                Party = ContentAndSoakTests.StarterParty(db, stage.LevelOverride ?? 1),
            };
            var s = BotRunner.Play(db, setup, new HeroBot(3), 300, (cmd, ev, st) => AssertInvariants(st));
            Assert.True(s.IsOver, "generated encounter did not resolve within the action cap");
        }
    }
}
