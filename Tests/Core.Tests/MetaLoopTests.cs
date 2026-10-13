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
            Assert.Equal(new[] { "paladin", "barbarian", "cleric", "redmage", "engineer" }, s.Party); // one of each role
            Assert.Equal(new[] { "Tank", "Striker", "Healer", "Mage", "Support" }, s.Party.Select(id => Real.Classes[id].Role));
            Assert.Equal(Progression.StartingGold, s.Gold);
            Assert.All(s.Roster, h => Assert.Contains(h.Bar, e => e != null));
            Assert.Equal(Progression.MaxBarSlots, Progression.UnlockedSlots(s)); // every slot open from the start (2026-10-11)
            // One full-life stack per hero (amended 2026-10-11: the second X-Potion slot became each
            // class's new unique 5th skill).
            Assert.All(s.Roster, h => Assert.Equal(1, h.Bar.Count(e => e == "item:x_potion")));
            Assert.Equal(s.Roster.Count * Real.Items["x_potion"].MaxStack, s.Count("x_potion"));  // one full stack of 5 each
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
                var st = BotRunner.Play(c, setup, new HeroBot(i), 900); // the first battle is a 20-wave gauntlet
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
            Assert.Equal(Progression.MaxPartySize, s.Party.Count);
            Assert.Equal(5, Progression.MaxPartySize);

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

        // ── Expeditions (§22.5) ──

        private static BattleState Fight(ContentDb c, SaveData s, string stageId, bool win)
        {
            Assert.True(Expeditions.CanLaunch(c, s, stageId, out var why), why);
            Expeditions.Begin(c, s, stageId);
            var setup = BattleFactory.Build(c, s, stageId, 7);
            var st = BattleEngine.Create(c, setup, new List<BattleEvent>());
            st.Phase = win ? BattlePhase.Victory : BattlePhase.Defeat; // the meta rules don't care how it went
            return st;
        }

        [Fact]
        public void An_expedition_runs_its_battles_in_order_carrying_mana_with_the_inn_closed()
        {
            var c = Real; var s = Progression.NewGame(c, "Ex");
            var theme = c.Campaign.Themes[0];
            Assert.False(Expeditions.CanLaunch(c, s, theme.Stages[1], out _), "expeditions start at battle 1");
            var b1 = Fight(c, s, theme.Stages[0], true);
            b1.Bank.Add(4);
            var rep = BattleFactory.Commit(c, s, b1);
            Assert.NotNull(s.Expedition);
            Assert.Equal(1, s.Expedition.Next);
            Assert.Equal(b1.Bank.Count, s.Expedition.CarriedOrbs);
            Assert.Equal(theme.Stages[1], rep.NextStageId);
            Assert.False(Expeditions.CanLaunch(c, s, c.Campaign.Themes[1].Stages[0], out _), "one expedition at a time");
            s.Roster[0].Hp = 1;
            Assert.False(Inn.Rest(c, s).Ok, "no inn on the road");
            var setup = BattleFactory.Build(c, s, theme.Stages[1], 3);
            Assert.Equal(s.Expedition.CarriedOrbs, setup.StartOrbs);
            var b2 = BattleEngine.Create(c, setup, new List<BattleEvent>());
            Assert.True(b2.Bank.Count >= setup.StartOrbs, "carried orbs seed the next battle's bank");
        }

        [Fact]
        public void Finishing_an_expedition_pays_a_bonus_and_rests_the_party_while_defeat_restarts_it()
        {
            var c = Real; var s = Progression.NewGame(c, "Ex2");
            var theme = c.Campaign.Themes[0];
            BattleReport last = null;
            foreach (var id in theme.Stages) last = BattleFactory.Commit(c, s, Fight(c, s, id, true));
            Assert.True(last.ExpeditionComplete);
            Assert.True(last.ExpeditionBonus >= 0);
            Assert.Null(s.Expedition);
            Assert.All(s.Party, id => Assert.Equal(0, s.Hero(id).Hp));

            var s2 = Progression.NewGame(c, "Ex3");
            BattleFactory.Commit(c, s2, Fight(c, s2, theme.Stages[0], true));
            var lost = BattleFactory.Commit(c, s2, Fight(c, s2, theme.Stages[1], false));
            Assert.True(lost.ExpeditionFailed);
            Assert.Null(s2.Expedition);
            Assert.True(Expeditions.CanLaunch(c, s2, theme.Stages[0], out _));
            Assert.False(Expeditions.CanLaunch(c, s2, theme.Stages[1], out _), "a failed expedition restarts from battle 1");
        }

        // ── Voluntary retreat (§22.2, FFIV-style partial loss, owner direction 2026-10-11) ──

        private static BattleState DirectBattle(ContentDb c, SaveData s, string stageId, int seed = 11) =>
            BattleEngine.Create(c, BattleFactory.Build(c, s, stageId, seed), new List<BattleEvent>());

        [Fact]
        public void A_voluntary_retreat_keeps_a_fraction_of_coins_and_items_but_not_the_rest()
        {
            var c = Real; var s = Progression.NewGame(c, "Retreat1");
            var st = DirectBattle(c, s, c.CampaignStages()[0].Id);
            st.Results.Coins = 100;
            st.Results.Items["ether"] = 4;
            st.Phase = BattlePhase.Retreated;
            int goldBefore = s.Gold;
            var rep = BattleFactory.Commit(c, s, st);

            Assert.True(rep.Retreated);
            int expectedGold = (int)System.Math.Floor(100 * BattleConfig.RetreatKeepFraction);
            Assert.Equal(expectedGold, rep.Gold);
            Assert.Equal(goldBefore + expectedGold, s.Gold);
            Assert.Equal((int)System.Math.Floor(4 * BattleConfig.RetreatKeepFraction), rep.Items.GetValueOrDefault("ether"));
        }

        [Fact]
        public void A_retreat_still_keeps_hard_won_xp_like_a_defeat()
        {
            var c = Real; var s = Progression.NewGame(c, "Retreat2");
            var st = DirectBattle(c, s, c.CampaignStages()[0].Id);
            st.Results.Xp = 40;
            st.Phase = BattlePhase.Retreated;
            var rep = BattleFactory.Commit(c, s, st);

            Assert.Equal(40, rep.XpEach);
            Assert.All(s.Party, id => Assert.True(s.Hero(id).TotalXp >= 40));
        }

        [Fact]
        public void Retreating_mid_expedition_ends_it_without_touching_already_banked_gold()
        {
            var c = Real; var s = Progression.NewGame(c, "Retreat3");
            var theme = c.Campaign.Themes[0];
            BattleFactory.Commit(c, s, Fight(c, s, theme.Stages[0], true));
            int goldAfterBattle1 = s.Gold;

            var b2 = DirectBattle(c, s, theme.Stages[1]);
            b2.Results.Coins = 60;
            b2.Phase = BattlePhase.Retreated;
            var rep = BattleFactory.Commit(c, s, b2);

            Assert.True(rep.ExpeditionFailed);
            Assert.Null(s.Expedition);
            int expectedKeep = (int)System.Math.Floor(60 * BattleConfig.RetreatKeepFraction);
            Assert.Equal(goldAfterBattle1 + expectedKeep, s.Gold);
        }

        [Fact]
        public void Defeat_still_grants_only_hard_won_xp_after_the_retreat_refactor()
        {
            var c = Real; var s = Progression.NewGame(c, "Defeat1");
            var st = DirectBattle(c, s, c.CampaignStages()[0].Id);
            st.Results.Coins = 100;
            st.Results.Items["ether"] = 4;
            st.Results.Xp = 40;
            st.Phase = BattlePhase.Defeat;
            int goldBefore = s.Gold;
            var rep = BattleFactory.Commit(c, s, st);

            Assert.False(rep.Retreated);
            Assert.Equal(0, rep.Gold);
            Assert.Equal(goldBefore, s.Gold);
            Assert.Empty(rep.Items);
            Assert.Equal(40, rep.XpEach);
        }

        // ── Guest allies (§25.11) ──

        private static StageDef GuestStage(string guest) => new StageDef { Id = "g", Name = "G", IsTest = true, Waves = { new WaveDef { Enemies = { new SpawnDef { Class = "slime00", Level = 1, X = 2, Y = 1 } }, Guests = { new SpawnDef { Class = guest, Level = 3, X = 0, Y = 5 } } } } };

        [Fact]
        public void A_guest_who_survives_a_victory_joins_the_roster()
        {
            var c = Real; var s = Progression.NewGame(c, "G1");
            var setup = BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 1);
            setup.Stage = GuestStage("dervish");
            var ev = new List<BattleEvent>();
            var st = BattleEngine.Create(c, setup, ev);
            var guest = st.Units.Single(u => u.IsGuest);
            Assert.Equal("dervish", guest.Def.Id);
            Assert.Contains(ev, e => e is GuestJoinedEvent);
            st.Phase = BattlePhase.Victory;
            var rep = BattleFactory.Commit(c, s, st);
            Assert.Contains("dervish", rep.GuestsJoined);
            Assert.NotNull(s.Hero("dervish"));
            Assert.Equal(Progression.MaxPartySize, s.Party.Count); // a full party: the rescued guest waits in reserve
        }

        [Fact]
        public void A_guest_who_falls_is_lost_forever()
        {
            var c = Real; var s = Progression.NewGame(c, "G2");
            var setup = BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 1);
            setup.Stage = GuestStage("drifter");
            var st = BattleEngine.Create(c, setup, new List<BattleEvent>());
            var guest = st.Units.Single(u => u.IsGuest);
            guest.Hp = 0; guest.OnBoard = false;
            st.Phase = BattlePhase.Victory;
            var rep = BattleFactory.Commit(c, s, st);
            Assert.Contains("drifter", rep.GuestsLost);
            Assert.Null(s.Hero("drifter"));
            Assert.Contains("drifter", s.LostGuests);
            var again = BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 2);
            again.Stage = GuestStage("drifter");
            Assert.DoesNotContain(BattleEngine.Create(c, again, new List<BattleEvent>()).Units, u => u.IsGuest);
            Assert.DoesNotContain(Summon.Pool(c, s), d => d.Id == "drifter");
        }

        // ── Caravan upgrades (§25.12) ──

        [Fact]
        public void Caravan_upgrades_cost_gold_and_change_how_expeditions_play()
        {
            var c = Real; var s = Progression.NewGame(c, "Car");
            Assert.True(c.Caravan.Count >= 5);
            s.Gold = 10000;
            var stage0 = c.Campaign.Themes[0].Stages[0];
            int orbsBefore = BattleFactory.Build(c, s, stage0, 1).StartOrbs;
            Assert.True(CaravanUpgrades.Buy(c, s, "mana_casks").Ok);
            Assert.Equal(orbsBefore + 1, BattleFactory.Build(c, s, stage0, 1).StartOrbs);
            Assert.True(10000 - c.Caravan["mana_casks"].Levels[0].Cost == s.Gold);

            // Armory: cheaper repairs.
            var sword = c.Items.Values.First(i => i.Durability > 2 && i.Slot == EquipmentSlot.Weapon);
            int full = Repair.Cost(c, s, sword, 0, 0);
            CaravanUpgrades.Buy(c, s, "armory");
            Assert.True(Repair.Cost(c, s, sword, 0, 0) < full);

            // Medic wagon: the inn opens on the road, at a premium.
            BattleFactory.Commit(c, s, Fight(c, s, stage0, true));
            Assert.NotNull(s.Expedition);
            s.Roster[0].Hp = 1;
            Assert.False(Inn.Rest(c, s).Ok);
            CaravanUpgrades.Buy(c, s, "medic_wagon");
            int premium = Inn.Cost(c, s);
            Assert.True(Inn.Rest(c, s).Ok, "the Medic Wagon opens the inn on the road");
            Assert.True(premium > 0);

            // Maxing out.
            while (CaravanUpgrades.NextLevel(c, s, "scouts") != null) Assert.True(CaravanUpgrades.Buy(c, s, "scouts").Ok);
            Assert.False(CaravanUpgrades.Buy(c, s, "scouts").Ok);
        }
    }
}
