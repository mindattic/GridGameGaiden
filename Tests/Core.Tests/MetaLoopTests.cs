using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;
using Gaiden.Meta;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    public class MetaLoopTests
    {
        private readonly ITestOutputHelper output;
        public MetaLoopTests(ITestOutputHelper output) { this.output = output; }

        [Fact]
        public void New_game_has_starting_trio_gold_and_loadouts()
        {
            var s = Progression.NewGame(Real, "Test");
            Assert.Equal(new[] { "paladin", "barbarian", "cleric" }, s.Party);
            Assert.Equal(Progression.StartingGold, s.Gold);
            Assert.All(s.Roster, h => Assert.Contains(h.Bar, e => e != null));
            Assert.Equal(2, Progression.UnlockedSlots(s));
        }

        [Fact]
        public void Save_round_trips_through_json()
        {
            var s = Progression.NewGame(Real, "Round");
            s.ClearedStages.Add("greenvalley-01");
            s.Bestiary["slime00"] = new BestiaryEntry { Seen = true, Defeated = 2 };
            var json = JsonConvert.SerializeObject(s, Formatting.Indented, ContentLoader.Settings);
            var back = SaveMigrations.Migrate(JsonConvert.DeserializeObject<SaveData>(json, ContentLoader.Settings));
            Assert.Equal(s.Gold, back.Gold);
            Assert.Contains("greenvalley-01", back.ClearedStages);
            Assert.Equal(2, back.Bestiary["slime00"].Defeated);
            Assert.Equal(s.Roster[0].Bar, back.Roster[0].Bar);
        }

        [Fact]
        public void Full_loop_battle_commit_then_every_vendor()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Loop");
            var stages = c.CampaignStages();
            int victories = 0;
            for (int i = 0; i < 6; i++)
            {
                var stage = stages[System.Math.Min(i, stages.Count - 1)];
                if (!Progression.IsStageUnlocked(s, stages.IndexOf(stage))) break;
                var setup = BattleFactory.Build(c, s, stage.Id, 100 + i);
                var st = BotRunner.Play(c, setup, new HeroBot(i), 300);
                var rep = BattleFactory.Commit(c, s, st);
                if (rep.Victory) victories++;
                output.WriteLine($"{stage.Id}: {(rep.Victory ? "WIN" : "LOSS")} gold+{rep.Gold} xp+{rep.XpEach} items {string.Join(",", rep.Items.Select(kv => kv.Key + "x" + kv.Value))} lvls {string.Join(",", rep.LevelUps)}");
            }
            Assert.True(victories >= 3, "bot should win the opening stages");
            Assert.True(s.HighestClearedStage >= 2);
            Assert.True(Progression.UnlockedSlots(s) >= 3);
            output.WriteLine($"Gold {s.Gold}; inventory {string.Join(", ", s.Inventory.Select(kv => kv.Key + "x" + kv.Value))}");

            // Merchant
            var stock = Shop.Stock(c);
            Assert.NotEmpty(stock);
            s.Gold += 2000;
            var buy = new Dictionary<string, int> { [stock[0].Id] = 2 };
            int before = s.Count(stock[0].Id);
            Assert.True(Shop.Buy(c, s, buy).Ok);
            Assert.Equal(before + 2, s.Count(stock[0].Id));
            Assert.True(Shop.Sell(c, s, new Dictionary<string, int> { [stock[0].Id] = 1 }).Ok);

            // Forge something: give ourselves the inputs of the cheapest forge recipe
            var recipe = Crafting.Recipes(c, CraftStation.Forge).First(r => r.BaseItem == null);
            foreach (var ic in recipe.Inputs) s.AddItem(ic.Item, ic.Count);
            var craft = Crafting.Craft(c, s, recipe);
            Assert.True(craft.Ok, craft.Message);
            var forged = c.Item(recipe.Result);
            Assert.Equal(1, s.Count(forged.Id));

            // Equip it on whoever it fits, then wear + repair it
            var hero = s.Hero(s.Party[0]);
            if (forged.Slot == EquipmentSlot.Weapon || forged.Slot == EquipmentSlot.Armor)
            {
                Assert.True(Equipment.Equip(c, s, hero, forged.Slot, forged.Id).Ok);
                Assert.Equal(forged.Id, Equipment.Equipped(hero, forged.Slot));
                if (forged.Slot == EquipmentSlot.Weapon && forged.Durability > 1)
                {
                    hero.WeaponDurability = 1;
                    Assert.True(Repair.Cost(forged, hero.WeaponDurability, hero.WeaponRepairs) > 0);
                    Assert.True(Repair.RepairWeapon(c, s, hero).Ok);
                    Assert.Equal(forged.Durability, hero.WeaponDurability);
                    Assert.Equal(forged.Durability - 1, Repair.EffectiveMax(forged, hero.WeaponRepairs));
                }
            }

            // Inn
            hero.Hp = 5;
            Assert.True(Inn.Cost(c, s) > 0);
            Assert.True(Inn.Rest(c, s).Ok);
            Assert.Equal(0, hero.Hp);

            // Learn + slot
            var learn = Abilities.Learnable(c, hero).First();
            Assert.True(Abilities.Learn(c, s, hero, learn.Id).Ok);
            Assert.True(Abilities.Assign(c, s, hero, 0, learn.Id).Ok);
            Assert.Equal(learn.Id, hero.Bar[0]);

            // Summon
            var recruit = Summon.Pool(c, s).First(d => s.Hero(d.Id) == null);
            int price = Summon.Price(c, s);
            Assert.True(Summon.Recruit(c, s, recruit.Id).Ok);
            Assert.Equal(price + 250, Summon.Price(c, s));

            // Party cap
            while (s.Party.Count < Progression.MaxPartySize)
            {
                var extra = s.Roster.FirstOrDefault(h => !s.Party.Contains(h.ClassId));
                if (extra != null) { Assert.True(Party.Toggle(s, extra.ClassId).Ok); continue; }
                s.Gold += Summon.Price(c, s); // recruit until the party can fill (a recruit joins while there's room)
                Assert.True(Summon.Recruit(c, s, Summon.Pool(c, s).First(d => s.Hero(d.Id) == null).Id).Ok);
            }
            Assert.Equal(6, s.Party.Count);

            // Bounty
            var bounty = Bounties.Posted(c, s).First();
            Assert.True(Bounties.Accept(c, s, bounty.Id).Ok);
            Bounties.RecordKills(c, s, new Dictionary<string, int> { [bounty.TargetClass] = bounty.Kills });
            int g = s.Gold;
            Assert.True(Bounties.Claim(c, s).Ok);
            Assert.Equal(g + bounty.RewardGold, s.Gold);
        }

        [Fact]
        public void Defeat_resets_wounds_and_grants_nothing()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Lose");
            var stage = c.CampaignStages()[0];
            var setup = BattleFactory.Build(c, s, stage.Id, 3);
            var st = BattleEngine.Create(c, setup, new List<BattleEvent>());
            foreach (var u in st.Units.Where(u => u.IsHero)) u.Hp = 1;
            // shield until the party falls
            for (int i = 0; i < 400 && !st.IsOver; i++) BattleEngine.Apply(st, new ShieldCommand());
            Assert.Equal(BattlePhase.Defeat, st.Phase);
            int gold = s.Gold;
            var rep = BattleFactory.Commit(c, s, st);
            Assert.False(rep.Victory);
            Assert.Equal(gold, s.Gold);
            Assert.All(s.Roster, h => Assert.Equal(0, h.Hp));
            Assert.Equal(-1, s.HighestClearedStage);
        }
    }
}
