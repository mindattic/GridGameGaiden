using System.IO;
using System.Linq;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;
using NUnit.Framework;
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

        [Test]
        public void Same_seed_gives_the_same_encounter()
        {
            var db = Real;
            StageDef Gen() => EncounterGenerator.Generate(db, 12345);
            var a = Gen();
            var b = Gen();

            Assert.That(b.Id, Is.EqualTo(a.Id));
            Assert.That(b.Theme, Is.EqualTo(a.Theme));
            Assert.That(b.Blocks.Select(c => (c.X, c.Y)), Is.EqualTo(a.Blocks.Select(c => (c.X, c.Y))));
            Assert.That(b.Waves.Count, Is.EqualTo(a.Waves.Count));
            for (int i = 0; i < a.Waves.Count; i++)
            {
                Assert.That(b.Waves[i].Enemies.Select(e => (e.Class, e.Level)), Is.EqualTo(a.Waves[i].Enemies.Select(e => (e.Class, e.Level))));
            }
        }
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(999)]
        [TestCase(-7)]
        public void Different_seeds_still_produce_five_waves_one_family_each(int seed)
        {
            var db = Real;
            var stage = EncounterGenerator.Generate(db, seed);

            Assert.That(stage.Waves.Count, Is.EqualTo(EncounterGenerator.WaveCount));
            foreach (var w in stage.Waves) {
                Assert.That(w.Enemies, Is.Not.Empty);
                var families = w.Enemies.Select(e => db.Class(e.Class).DropTable).Distinct().ToList();
                Assert.That(families.Count == 1, Is.True, $"wave mixes families: {string.Join(", ", families)}");
            }
        }

        [Test]
        public void Families_vary_across_the_five_waves()
        {
            var db = Real;
            var stage = EncounterGenerator.Generate(db, 55);
            var familyPerWave = stage.Waves.Select(w => db.Class(w.Enemies[0].Class).DropTable).ToList();
            Assert.That(familyPerWave.Distinct().Count(), Is.EqualTo(familyPerWave.Count));
        }
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(999)]
        public void Levels_are_inside_the_campaign_band_and_rise_across_waves(int seed)
        {
            var db = Real;
            var stage = EncounterGenerator.Generate(db, seed);
            int campaignMax = db.CampaignStages().Count;

            var levels = stage.Waves.Select(w => w.Enemies[0].Level).ToList();
            foreach (var lvl in levels) Assert.That(lvl, Is.InRange(1, campaignMax));
            for (int i = 1; i < levels.Count; i++) Assert.That(levels[i] >= levels[i - 1], Is.True, "levels must not fall from wave to wave");
            Assert.That(stage.LevelOverride ?? 0, Is.InRange(1, campaignMax));
        }

        [Test]
        public void Generated_encounter_passes_ContentLoader_Validate()
        {
            var db = FreshReal();
            var stage = EncounterGenerator.Generate(db, 777);
            db.Stages[stage.Id] = stage;

            var problems = ContentLoader.Validate(db);
            Assert.That(problems.Count == 0, Is.True, string.Join("\n", problems));
        }

        [Test]
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
            var s = BotRunner.Play(db, setup, new HeroBot(3), 300, (cmd, ev, st) => { AssertInvariants(st); AssertEventInvariants(ev); });
            Assert.That(s.IsOver, Is.True, "generated encounter did not resolve within the action cap");
        }
    }
}
