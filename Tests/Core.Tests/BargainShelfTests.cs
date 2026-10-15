using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Meta;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>Covers the Merchant's rotating "Used / Bargain" shelf: discounted, pre-worn weapons and
    /// armor with per-instance durability (bible §25.1, US-246, GG-T38).</summary>
    public class BargainShelfTests
    {
        [Fact]
        public void Shelf_rolls_only_eligible_weapons_and_armor_within_the_price_and_durability_bands()
        {
            var c = Real;
            for (int rotation = 0; rotation < 20; rotation++)
            {
                var s = new SaveData { BargainRotation = rotation };
                var offers = Bargains.Shelf(c, s);
                Assert.Equal(Bargains.ShelfSize, offers.Count);
                var seen = new HashSet<string>();
                foreach (var o in offers)
                {
                    Assert.True(seen.Add(o.Item.Id), "the shelf should never roll the same item twice in one rotation");
                    Assert.Equal(ItemType.Equipment, o.Item.Type);
                    Assert.True(o.Item.Slot == EquipmentSlot.Weapon || o.Item.Slot == EquipmentSlot.Armor);
                    Assert.True(o.Item.Durability > 0);
                    Assert.InRange(o.Price, (int)(o.Item.BaseCost * Bargains.MinPriceFraction) - 1, (int)System.Math.Ceiling(o.Item.BaseCost * Bargains.MaxPriceFraction) + 1);
                    Assert.InRange(o.Durability, 1, (int)System.Math.Ceiling(o.Item.Durability * Bargains.MaxDurabilityFraction) + 1);
                    Assert.True(o.Durability < o.Item.Durability, "a bargain piece is always worn, never factory-fresh");
                }
            }
        }

        [Fact]
        public void Shelf_is_stable_until_rotated_then_changes()
        {
            var c = Real;
            var s = new SaveData { BargainRotation = 3 };
            var first = Bargains.Shelf(c, s);
            var again = Bargains.Shelf(c, s); // reopening the Merchant must not reroll it
            Assert.Equal(first.Select(o => (o.Item.Id, o.Price, o.Durability)), again.Select(o => (o.Item.Id, o.Price, o.Durability)));

            Bargains.Rotate(s);
            Assert.Equal(4, s.BargainRotation);
            var rotated = Bargains.Shelf(c, s);
            Assert.NotEqual(first.Select(o => (o.Item.Id, o.Price, o.Durability)), rotated.Select(o => (o.Item.Id, o.Price, o.Durability)));
        }

        [Fact]
        public void Buy_charges_gold_and_grants_a_worn_copy_at_the_rolled_durability()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Bargain");
            s.Gold = 100000;
            var offer = Bargains.Shelf(c, s).First();
            int goldBefore = s.Gold;

            var r = Bargains.Buy(c, s, offer.Item.Id);

            Assert.True(r.Ok, r.Message);
            Assert.Equal(goldBefore - offer.Price, s.Gold);
            Assert.Equal(1, s.Count(offer.Item.Id));
            Assert.True(s.Worn.TryGetValue(offer.Item.Id, out var worn));
            Assert.Single(worn);
            Assert.Equal(offer.Durability, worn[0].Durability);
            Assert.Equal(0, worn[0].Repairs);
        }

        [Fact]
        public void Equipping_a_bought_bargain_weapon_carries_its_rolled_durability_not_factory_fresh()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Equip");
            s.Gold = 100000;
            var offer = Bargains.Shelf(c, s).First(o => o.Item.Slot == EquipmentSlot.Weapon);
            Assert.True(Bargains.Buy(c, s, offer.Item.Id).Ok);
            var hero = s.Hero(s.Party[0]);

            var r = Equipment.Equip(c, s, hero, EquipmentSlot.Weapon, offer.Item.Id);

            Assert.True(r.Ok, r.Message);
            Assert.Equal(offer.Item.Id, hero.WeaponId);
            Assert.Equal(offer.Durability, hero.WeaponDurability);
            Assert.True(hero.WeaponDurability < offer.Item.Durability);
        }

        [Fact]
        public void Buy_is_refused_without_enough_gold_or_when_already_owned()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Refuse");
            var offer = Bargains.Shelf(c, s).First();
            s.Gold = 0;
            Assert.False(Bargains.Buy(c, s, offer.Item.Id).Ok);

            s.Gold = 100000;
            Assert.True(Bargains.Buy(c, s, offer.Item.Id).Ok);
            Assert.False(Bargains.Buy(c, s, offer.Item.Id).Ok); // MaxStack 1: already own the only copy
        }

        [Fact]
        public void Battle_commit_rotates_the_shelf_win_or_lose()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Rotate");
            var stageId = c.CampaignStages()[0].Id;
            Assert.Equal(0, s.BargainRotation);

            var win = BattleEngine.Create(c, BattleFactory.Build(c, s, stageId, 11), new List<BattleEvent>());
            win.Phase = BattlePhase.Victory;
            BattleFactory.Commit(c, s, win);
            Assert.Equal(1, s.BargainRotation);

            var loss = BattleEngine.Create(c, BattleFactory.Build(c, s, stageId, 12), new List<BattleEvent>());
            loss.Phase = BattlePhase.Defeat;
            BattleFactory.Commit(c, s, loss);
            Assert.Equal(2, s.BargainRotation);
        }
    }
}
