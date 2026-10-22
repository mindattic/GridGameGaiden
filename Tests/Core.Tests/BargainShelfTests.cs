using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Covers the Merchant's rotating "Used / Bargain" shelf: discounted, pre-worn weapons and
    /// armor with per-instance durability (bible §25.1, US-246, GG-T38).</summary>
    public class BargainShelfTests
    {
        [Test]
        public void Shelf_rolls_only_eligible_weapons_and_armor_within_the_price_and_durability_bands()
        {
            var c = Real;
            for (int rotation = 0; rotation < 20; rotation++)
            {
                var s = new SaveData { BargainRotation = rotation };
                var offers = Bargains.Shelf(c, s);
                Assert.That(offers.Count, Is.EqualTo(Bargains.ShelfSize));
                var seen = new HashSet<string>();
                foreach (var o in offers)
                {
                    Assert.That(seen.Add(o.Item.Id), Is.True, "the shelf should never roll the same item twice in one rotation");
                    Assert.That(o.Item.Type, Is.EqualTo(ItemType.Equipment));
                    Assert.That(o.Item.Slot == EquipmentSlot.Weapon || System.Array.IndexOf(BattleEngine.ArmorSlots, o.Item.Slot) >= 0, Is.True);
                    Assert.That(o.Item.Durability > 0, Is.True);
                    Assert.That(o.Price, Is.InRange((int)(o.Item.BaseCost * Bargains.MinPriceFraction) - 1, (int)System.Math.Ceiling(o.Item.BaseCost * Bargains.MaxPriceFraction) + 1));
                    Assert.That(o.Durability, Is.InRange(1, (int)System.Math.Ceiling(o.Item.Durability * Bargains.MaxDurabilityFraction) + 1));
                    Assert.That(o.Durability < o.Item.Durability, Is.True, "a bargain piece is always worn, never factory-fresh");
                }
            }
        }

        [Test]
        public void Shelf_is_stable_until_rotated_then_changes()
        {
            var c = Real;
            var s = new SaveData { BargainRotation = 3 };
            var first = Bargains.Shelf(c, s);
            var again = Bargains.Shelf(c, s); // reopening the Merchant must not reroll it
            Assert.That(again.Select(o => (o.Item.Id, o.Price, o.Durability)), Is.EqualTo(first.Select(o => (o.Item.Id, o.Price, o.Durability))));

            Bargains.Rotate(s);
            Assert.That(s.BargainRotation, Is.EqualTo(4));
            var rotated = Bargains.Shelf(c, s);
            Assert.That(rotated.Select(o => (o.Item.Id, o.Price, o.Durability)), Is.Not.EqualTo(first.Select(o => (o.Item.Id, o.Price, o.Durability))));
        }

        [Test]
        public void Buy_charges_gold_and_grants_a_worn_copy_at_the_rolled_durability()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Bargain");
            s.Gold = 100000;
            var offer = Bargains.Shelf(c, s).First();
            int goldBefore = s.Gold;

            var r = Bargains.Buy(c, s, offer.Item.Id);

            Assert.That(r.Ok, Is.True, r.Message);
            Assert.That(s.Gold, Is.EqualTo(goldBefore - offer.Price));
            Assert.That(s.Count(offer.Item.Id), Is.EqualTo(1));
            Assert.That(s.Worn.TryGetValue(offer.Item.Id, out var worn), Is.True);
            Assert.That(worn, Has.Exactly(1).Items);
            Assert.That(worn[0].Durability, Is.EqualTo(offer.Durability));
            Assert.That(worn[0].Repairs, Is.EqualTo(0));
        }

        [Test]
        public void Equipping_a_bought_bargain_weapon_carries_its_rolled_durability_not_factory_fresh()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Equip");
            s.Gold = 100000;
            var hero = s.Hero(s.Party[0]);
            // A weapon this hero is trained for (GG-T52): rotate the shelf until one turns up.
            var trained = Progression.EffectiveWeaponProficiencies(c, hero);
            BargainOffer offer = null;
            for (int i = 0; i < 200; i++)
            {
                offer = Bargains.Shelf(c, s).FirstOrDefault(o => o.Item.Slot == EquipmentSlot.Weapon && trained.Contains(o.Item.WeaponClass));
                if (offer != null) break;
                Bargains.Rotate(s);
            }
            Assert.That(offer, Is.Not.Null);
            Assert.That(Bargains.Buy(c, s, offer.Item.Id).Ok, Is.True);

            var r = Equipment.Equip(c, s, hero, EquipmentSlot.Weapon, offer.Item.Id);

            Assert.That(r.Ok, Is.True, r.Message);
            Assert.That(hero.WeaponId, Is.EqualTo(offer.Item.Id));
            Assert.That(hero.WeaponDurability, Is.EqualTo(offer.Durability));
            Assert.That(hero.WeaponDurability < offer.Item.Durability, Is.True);
        }

        [Test]
        public void Buy_is_refused_without_enough_gold_or_when_already_owned()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Refuse");
            var offer = Bargains.Shelf(c, s).First();
            s.Gold = 0;
            Assert.That(Bargains.Buy(c, s, offer.Item.Id).Ok, Is.False);

            s.Gold = 100000;
            Assert.That(Bargains.Buy(c, s, offer.Item.Id).Ok, Is.True);
            Assert.That(Bargains.Buy(c, s, offer.Item.Id).Ok, Is.False); // MaxStack 1: already own the only copy
        }

        [Test]
        public void Battle_commit_rotates_the_shelf_win_or_lose()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Rotate");
            var stageId = c.CampaignStages()[0].Id;
            Assert.That(s.BargainRotation, Is.EqualTo(0));

            var win = BattleEngine.Create(c, BattleFactory.Build(c, s, stageId, 11), new List<BattleEvent>());
            win.Phase = BattlePhase.Victory;
            BattleFactory.Commit(c, s, win);
            Assert.That(s.BargainRotation, Is.EqualTo(1));

            var loss = BattleEngine.Create(c, BattleFactory.Build(c, s, stageId, 12), new List<BattleEvent>());
            loss.Phase = BattlePhase.Defeat;
            BattleFactory.Commit(c, s, loss);
            Assert.That(s.BargainRotation, Is.EqualTo(2));
        }
    }
}
