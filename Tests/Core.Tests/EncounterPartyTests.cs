using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T56: a random, level-appropriate hero party for an endless encounter.</summary>
    public class EncounterPartyTests
    {
        [Test]
        public void Same_seed_and_level_gives_the_same_party()
        {
            var db = Real;
            List<HeroSpec> Gen() => EncounterParty.Generate(db, 2026, 20);
            var a = Gen();
            var b = Gen();

            Assert.That(b.Select(h => h.ClassId), Is.EqualTo(a.Select(h => h.ClassId)));
            Assert.That(b.Select(h => h.WeaponId), Is.EqualTo(a.Select(h => h.WeaponId)));
            Assert.That(b.Select(h => string.Join(",", h.Bar)), Is.EqualTo(a.Select(h => string.Join(",", h.Bar))));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(999)]
        [TestCase(-7)]
        public void Different_seeds_still_produce_five_distinct_hero_classes(int seed)
        {
            var db = Real;
            var party = EncounterParty.Generate(db, seed, 10);

            Assert.That(party.Count, Is.EqualTo(EncounterParty.PartySize));
            Assert.That(party.Select(h => h.ClassId).Distinct().Count(), Is.EqualTo(party.Count), "party fields the same class twice");
            foreach (var h in party) Assert.That(db.Class(h.ClassId).Team, Is.EqualTo(Team.Hero));
        }

        [Test]
        public void Draws_from_every_hero_class_not_just_the_starting_roster()
        {
            var db = Real;
            var seen = new HashSet<string>();
            for (int seed = 1; seed <= 60; seed++)
                foreach (var h in EncounterParty.Generate(db, seed, 15)) seen.Add(h.ClassId);

            // StartingRoster is 3 of the 60+ hero classes; a seed sweep must surface classes outside it,
            // or this is quietly still "the starting trio" in a trenchcoat.
            Assert.That(seen.Except(Progression.StartingRoster).Any(), Is.True);
            Assert.That(seen.Count, Is.GreaterThan(Progression.StartingRoster.Length));
        }

        [Test]
        public void Every_hero_has_a_full_bar_and_at_least_a_weapon()
        {
            var db = Real;
            foreach (var h in EncounterParty.Generate(db, 55, 25))
            {
                Assert.That(h.Bar.Count, Is.EqualTo(Progression.MaxBarSlots));
                Assert.That(h.Bar, Has.None.Null);
                Assert.That(h.WeaponId, Is.Not.Null, $"{h.ClassId} has no weapon");
                foreach (var slot in BattleEngine.ArmorSlots)
                    Assert.That(h.Armor.ContainsKey(slot), Is.True, $"{h.ClassId} has nothing in {slot}");
            }
        }

        [Test]
        public void Higher_level_parties_are_better_equipped_than_lower_level_ones()
        {
            var db = Real;
            float GearScore(IEnumerable<HeroSpec> party) => party.Sum(h =>
                MetaHeuristics.Score(db.TryItem(h.WeaponId)) + h.Armor.Values.Sum(a => MetaHeuristics.Score(db.TryItem(a.ItemId))));

            var low = GearScore(EncounterParty.Generate(db, 7, 1));
            var high = GearScore(EncounterParty.Generate(db, 7, db.CampaignStages().Count));
            Assert.That(high, Is.GreaterThan(low));
        }

        [Test]
        public void Generated_party_is_playable_without_breaking_invariants()
        {
            var db = Real;
            var stage = EncounterGenerator.Generate(db, 2027);
            db.Stages[stage.Id] = stage;

            var setup = new BattleSetup
            {
                Stage = stage,
                Seed = 11,
                UnlockedSlots = Progression.MaxBarSlots,
                RecommendedLevel = stage.LevelOverride ?? 1,
                Party = EncounterParty.Generate(db, 2027, stage.LevelOverride ?? 1),
            };
            var s = BotRunner.Play(db, setup, new HeroBot(3), 300, (cmd, ev, st) => { AssertInvariants(st); AssertEventInvariants(ev); });
            Assert.That(s.IsOver, Is.True, "generated party did not resolve within the action cap");
        }
    }
}
