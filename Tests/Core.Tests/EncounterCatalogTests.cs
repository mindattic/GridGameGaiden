using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T184: 100+ distinct authored enemy groups, spread across the campaign and the Crucible.</summary>
    public class EncounterCatalogTests
    {
        private static ContentDb FreshReal() => ContentLoader.Load(name =>
        {
            var p = Path.Combine(ContentDir, name + ".json");
            return File.Exists(p) ? File.ReadAllText(p) : null;
        });

        [Test]
        public void At_least_a_hundred_groups_and_no_two_alike()
        {
            var c = Real;
            Assert.That(c.EncounterOrder.Count, Is.GreaterThanOrEqualTo(100));
            var sigs = c.EncounterOrder.Select(g => EncounterCatalog.Signature(g.Enemies, false)).ToList();
            Assert.That(sigs.Distinct().Count(), Is.EqualTo(sigs.Count), "two groups are the same foes on the same tiles");
            Assert.That(c.EncounterOrder.Select(g => g.Formation).Distinct().Count(), Is.GreaterThanOrEqualTo(10), "formations should vary");
            foreach (var g in c.EncounterOrder)
                Assert.That(g.Enemies.Select(e => c.Class(e.Class).DropTable).Distinct().Count(), Is.EqualTo(1), $"{g.Id} mixes families");
        }

        [Test]
        public void Every_crucible_family_has_a_group_of_every_wave_size()
        {
            var c = Real;
            var families = c.Classes.Values.Where(EncounterCatalog.IsGroupable).Select(d => d.DropTable).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var f in families)
                for (int n = Crucible.FirstWaveEnemies; n <= Crucible.MaxWaveEnemies; n++)
                    Assert.That(EncounterCatalog.ForFamily(c, f).Any(g => g.Enemies.Count == n), Is.True, $"{f} has no group of {n}");
        }

        [Test]
        public void No_two_campaign_waves_are_the_same_encounter()
        {
            var c = Real;
            var waves = c.CampaignStages().SelectMany(s => s.Waves).Where(w => w.Enemies.Count > 0).ToList();
            Assert.That(waves.Count, Is.GreaterThanOrEqualTo(100));
            var sigs = waves.Select(w => EncounterCatalog.Signature(w.Enemies, true)).ToList();
            Assert.That(sigs.Distinct().Count(), Is.EqualTo(sigs.Count));
            var groupWaves = waves.Where(w => w.Group != null).ToList();
            Assert.That(groupWaves, Is.Not.Empty, "the campaign's repeated waves were replaced by catalog groups");
            Assert.That(groupWaves.Select(w => w.Group).Distinct().Count(), Is.EqualTo(groupWaves.Count));
            foreach (var w in groupWaves)
                Assert.That(EncounterCatalog.Signature(w.Enemies, false), Is.EqualTo(EncounterCatalog.Signature(c.TryEncounter(w.Group).Enemies, false)));
        }

        [Test]
        public void Validate_reports_twin_groups_and_repeated_campaign_waves()
        {
            var db = FreshReal();
            var first = db.EncounterOrder[0];
            var twin = new EncounterGroupDef { Id = "twin", Name = "Twin", Formation = "Twin", Level = 1, Enemies = first.Enemies.ToList() };
            db.EncounterOrder.Add(twin); db.Encounters[twin.Id] = twin;
            var campaign = db.CampaignStages();
            campaign[1].Waves[0] = new WaveDef { Enemies = campaign[0].Waves[0].Enemies.ToList() };
            campaign[2].Waves[0] = new WaveDef { Group = "nope" };
            var problems = ContentLoader.Validate(db);
            Assert.That(problems.Any(x => x.Contains("encounter twin") && x.Contains(first.Id)), Is.True, string.Join("\n", problems));
            Assert.That(problems.Any(x => x.Contains("the same foes, levels and tiles")), Is.True, string.Join("\n", problems));
            Assert.That(problems.Any(x => x.Contains("unknown encounter group 'nope'")), Is.True, string.Join("\n", problems));
        }

        [Test]
        public void Validate_rejects_bosses_off_board_tiles_and_mixed_families()
        {
            var db = FreshReal();
            var g = new EncounterGroupDef
            {
                Id = "bad", Name = "Bad", Formation = "Bad", Level = 1,
                Enemies = { new SpawnDef { Class = "cyclops00", X = 0, Y = 0 }, new SpawnDef { Class = "slime00", X = 0, Y = 7 }, new SpawnDef { Class = "wolf00", X = 1, Y = 1 } },
            };
            db.EncounterOrder.Add(g); db.Encounters[g.Id] = g;
            var problems = ContentLoader.Validate(db).Where(x => x.StartsWith("encounter bad")).ToList();
            Assert.That(problems.Any(x => x.Contains("cyclops00")), Is.True, string.Join("\n", problems));
            Assert.That(problems.Any(x => x.Contains("off the enemy half")), Is.True, string.Join("\n", problems));
            Assert.That(problems.Any(x => x.Contains("mixes families")), Is.True, string.Join("\n", problems));
        }

        [Test]
        public void A_crucible_run_walks_through_authored_groups_without_repeating_one_early()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Late");
            s.HighestClearedStage = c.CampaignStages().Count - 1; // every family in the campaign is reached
            foreach (var h in s.Roster) h.TotalXp = GridGame.Core.Rules.Formulas.TotalXpForLevel(10);
            var st = Crucible.Generate(c, s, 4242);
            var used = new List<string>();
            for (int i = 0; i < st.Waves.Count; i++)
            {
                var w = st.Waves[i];
                if (w.Group == null) continue; // a family with no groups (e.g. the naga) still fights as random foes
                var g = c.TryEncounter(w.Group);
                Assert.That(g, Is.Not.Null);
                Assert.That(w.Enemies.Take(g.Enemies.Count).Select(e => (e.Class, e.X, e.Y)), Is.EqualTo(g.Enemies.Select(e => (e.Class, e.X, e.Y))), $"wave {i} isn't its group's formation");
                if (used.Contains(g.Id))
                {
                    // A repeat only once every same-size group of that family has had its turn.
                    var family = EncounterCatalog.Family(c, g);
                    var siblings = EncounterCatalog.ForFamily(c, family).Where(x => x.Enemies.Count == g.Enemies.Count).Select(x => x.Id);
                    Assert.That(siblings.All(used.Contains), Is.True, $"wave {i} repeats {g.Id} while its family has fresh groups");
                }
                used.Add(g.Id);
            }
            Assert.That(used.Distinct().Count(), Is.GreaterThanOrEqualTo(30), "a long run should show many different groups");
        }

        [Test]
        public void Endless_encounters_use_authored_groups()
        {
            var c = Real;
            int grouped = 0;
            for (int seed = 1; seed <= 20; seed++)
                grouped += EncounterGenerator.Generate(c, seed).Waves.Count(w => w.Group != null);
            Assert.That(grouped, Is.GreaterThan(50));
        }
    }
}
