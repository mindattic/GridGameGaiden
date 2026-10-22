using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T174: Camp stations open one at a time; a closed one says when it arrives; nothing used ever relocks.</summary>
    public class CampUnlocksTests
    {
        private static SaveData Fresh() => new SaveData { HighestClearedStage = -1 };

        [Test]
        public void A_fresh_save_has_every_station_closed_until_its_keeper_or_milestone()
        {
            var s = Fresh();
            Assert.That(CampUnlocks.NpcOpen(s, CampNpcs.Merchant), Is.False);
            Assert.That(CampUnlocks.BountiesOpen(s), Is.False);
            Assert.That(CampUnlocks.SummonOpen(s), Is.False);
            Assert.That(CampUnlocks.CrucibleOpen(s), Is.False);
            Assert.That(CampUnlocks.CaravanOpen(s), Is.False);
        }

        [Test]
        public void Milestone_stations_open_as_stages_are_cleared()
        {
            var s = Fresh();
            s.HighestClearedStage = CampUnlocks.BountiesAfter - 1;
            Assert.That(CampUnlocks.BountiesOpen(s), Is.True);
            Assert.That(CampUnlocks.SummonOpen(s), Is.False);
            s.HighestClearedStage = CampUnlocks.CrucibleAfter - 1;
            Assert.That(CampUnlocks.CrucibleOpen(s) && CampUnlocks.SummonOpen(s), Is.True);
            Assert.That(CampUnlocks.CaravanOpen(s), Is.False);
            s.HighestClearedStage = CampUnlocks.CaravanAfter - 1;
            Assert.That(CampUnlocks.CaravanOpen(s), Is.True);
        }

        [Test]
        public void A_station_already_used_never_locks_again()
        {
            var s = Fresh();
            s.Bounty.ActiveId = "bounty_slime_culling";
            s.Recruits = 1;
            s.Crucible.Runs = 1;
            s.Caravan["wagon"] = 1;
            Assert.That(CampUnlocks.BountiesOpen(s) && CampUnlocks.SummonOpen(s) && CampUnlocks.CrucibleOpen(s) && CampUnlocks.CaravanOpen(s), Is.True);
        }

        [Test]
        public void Bot_and_new_game_plus_saves_see_everything()
        {
            Assert.That(CampUnlocks.CaravanOpen(new SaveData { TestMode = true, HighestClearedStage = -1 }), Is.True);
            Assert.That(CampUnlocks.NpcOpen(new SaveData { TestMode = true }, CampNpcs.Enchantress), Is.True);
            Assert.That(CampUnlocks.CaravanOpen(new SaveData { NewGamePlus = 1, HighestClearedStage = -1 }), Is.True);
        }

        [Test]
        public void Join_hints_name_the_story_beat_that_recruits_each_keeper()
        {
            var c = TestKit.Real;
            Assert.That(CampUnlocks.JoinHint(c, CampNpcs.Merchant), Does.StartWith("Joins at "));
            Assert.That(CampUnlocks.JoinHint(c, CampNpcs.Innkeeper), Does.StartWith("Joins after "));
            Assert.That(CampUnlocks.JoinHint(c, CampNpcs.Blacksmith), Is.EqualTo("Joins when gear wears below 60%"));
            Assert.That(CampUnlocks.JoinHint(c, CampNpcs.Alchemist), Is.EqualTo("Joins once you've looted enough to brew"));
            foreach (var npc in CampNpcs.All)
                Assert.That(CampUnlocks.JoinHint(c, npc), Does.Not.Contain("later on the road"), npc + " has no join beat");
        }

        [Test]
        public void The_blacksmith_opens_with_only_repair_until_the_alchemist_joins()
        {
            var s = Fresh();
            DialogueRunner.ApplyEffect(TestKit.Real, s, new GridGame.Core.Content.DialogueEffect { Type = GridGame.Core.Content.DialogueEffectType.JoinNpc, Id = CampNpcs.Blacksmith });
            Assert.That(CampUnlocks.NpcOpen(s, CampNpcs.Blacksmith), Is.True);
            Assert.That(CampUnlocks.SmithWorkshopOpen(s), Is.False, "Forge/Upgrade/Salvage wait");
            DialogueRunner.ApplyEffect(TestKit.Real, s, new GridGame.Core.Content.DialogueEffect { Type = GridGame.Core.Content.DialogueEffectType.JoinNpc, Id = CampNpcs.Alchemist });
            Assert.That(CampUnlocks.SmithWorkshopOpen(s), Is.True);
            Assert.That(CampUnlocks.SmithWorkshopOpen(new SaveData { TestMode = true }), Is.True);
            Assert.That(CampUnlocks.SmithWorkshopOpen(new SaveData { NewGamePlus = 1 }), Is.True);
        }

        [Test]
        public void Milestone_hints_name_a_real_stage()
        {
            var c = TestKit.Real;
            var stages = c.CampaignStages();
            Assert.That(CampUnlocks.MilestoneHint(c, CampUnlocks.BountiesAfter), Is.EqualTo("After " + stages[CampUnlocks.BountiesAfter - 1].Name));
        }
    }
}
