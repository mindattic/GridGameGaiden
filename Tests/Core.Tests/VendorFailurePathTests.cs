using System;
using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Content;
using Gaiden.Meta;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>Failure paths for the vendor/crafting/repair/inn gold economy (GG-T30): every rejection must
    /// leave gold and inventory untouched, and the pricing/growth formulas must hold at their edges.</summary>
    public class VendorFailurePathTests
    {
        /// <summary>A small, fully controlled catalog (on top of <see cref="Mini"/>'s hero classes) so prices,
        /// stacks and recipes are exact numbers rather than whatever the real content happens to contain.</summary>
        private static ContentDb BuildContent()
        {
            var c = Mini();

            ItemDef Item(string id, ItemType type, int baseCost, int maxStack = 99, int durability = 0, EquipmentSlot slot = EquipmentSlot.None, int baseHealing = 0) =>
                c.Items[id] = new ItemDef { Id = id, Name = id, Type = type, BaseCost = baseCost, MaxStack = maxStack, Durability = durability, Slot = slot, BaseHealing = baseHealing, SoldByVendor = true };

            Item("elixir", ItemType.Consumable, 20, maxStack: 5, baseHealing: 30);
            Item("herb", ItemType.CraftingMaterial, 2);
            Item("ore", ItemType.CraftingMaterial, 5);
            Item("trash", ItemType.CraftingMaterial, 0);
            Item("questkey", ItemType.QuestItem, 50);
            Item("warhammer", ItemType.Equipment, 100, maxStack: 1, durability: 100, slot: EquipmentSlot.Weapon);
            Item("finehammer", ItemType.Equipment, 150, maxStack: 1, durability: 100, slot: EquipmentSlot.Weapon);
            Item("plate", ItemType.Equipment, 100, maxStack: 1, durability: 100, slot: EquipmentSlot.Armor);

            c.Recipes["craft_elixir"] = new RecipeDef { Id = "craft_elixir", Station = CraftStation.Alchemy, Inputs = { new ItemCount { Item = "herb", Count = 2 } }, Gold = 10, Result = "elixir", ResultCount = 1 };
            c.Recipes["craft_warhammer"] = new RecipeDef { Id = "craft_warhammer", Station = CraftStation.Forge, Inputs = { new ItemCount { Item = "ore", Count = 2 } }, Gold = 30, Result = "warhammer", ResultCount = 1 };
            c.Recipes["upgrade_warhammer"] = new RecipeDef { Id = "upgrade_warhammer", Station = CraftStation.Forge, Inputs = { new ItemCount { Item = "ore", Count = 1 } }, Gold = 20, BaseItem = "warhammer", Result = "finehammer", ResultCount = 1 };

            c.Caravan["armory"] = new CaravanUpgradeDef { Id = "armory", Levels = { new CaravanLevel { Cost = 100, Value = 0.3f, Text = "Cheaper repairs" } } };
            c.Caravan["medic_wagon"] = new CaravanUpgradeDef { Id = "medic_wagon", Levels = { new CaravanLevel { Cost = 100, Value = 2f, Text = "Inn on the road" } } };
            return c;
        }

        private static SaveData NewSave(int gold = 0) => new SaveData { Gold = gold };

        // ── Shop.Buy ──

        [Fact]
        public void Buy_rejects_an_empty_cart_without_touching_gold()
        {
            var c = BuildContent();
            var s = NewSave(1000);
            var result = Shop.Buy(c, s, new Dictionary<string, int>());
            Assert.False(result.Ok);
            Assert.Equal(1000, s.Gold);
            Assert.Empty(s.Inventory);
        }

        [Fact]
        public void Buy_rejects_insufficient_gold_without_touching_gold_or_inventory()
        {
            var c = BuildContent();
            var s = NewSave(5); // elixir costs 20
            var result = Shop.Buy(c, s, new Dictionary<string, int> { ["elixir"] = 1 });
            Assert.False(result.Ok);
            Assert.Equal(5, s.Gold);
            Assert.Equal(0, s.Count("elixir"));
        }

        [Fact]
        public void Buy_rejects_quantity_above_MaxBuy_without_touching_gold_or_inventory()
        {
            var c = BuildContent();
            var s = NewSave(10000);
            int tooMany = Shop.MaxBuy(s, c.Item("elixir")) + 1;
            var result = Shop.Buy(c, s, new Dictionary<string, int> { ["elixir"] = tooMany });
            Assert.False(result.Ok);
            Assert.Equal(10000, s.Gold);
            Assert.Equal(0, s.Count("elixir"));
        }

        [Fact]
        public void Buy_rejects_a_negative_quantity_without_touching_gold_or_inventory()
        {
            var c = BuildContent();
            var s = NewSave(10000);
            var result = Shop.Buy(c, s, new Dictionary<string, int> { ["elixir"] = -1 });
            Assert.False(result.Ok);
            Assert.Equal(10000, s.Gold);
            Assert.Equal(0, s.Count("elixir"));
        }

        [Fact]
        public void Buy_rejects_an_unknown_item_without_touching_gold_or_inventory()
        {
            var c = BuildContent();
            var s = NewSave(10000);
            var result = Shop.Buy(c, s, new Dictionary<string, int> { ["no_such_item"] = 1 });
            Assert.False(result.Ok);
            Assert.Equal(10000, s.Gold);
            Assert.Empty(s.Inventory);
        }

        // ── Shop.Sell ──

        [Fact]
        public void Sell_rejects_selling_more_than_owned_without_touching_gold_or_inventory()
        {
            var c = BuildContent();
            var s = NewSave(0);
            s.AddItem("elixir", 2);
            var result = Shop.Sell(c, s, new Dictionary<string, int> { ["elixir"] = 3 });
            Assert.False(result.Ok);
            Assert.Equal(0, s.Gold);
            Assert.Equal(2, s.Count("elixir"));
        }

        // ── Shop.SellPrice / CartTotal / Sellable ──

        [Theory]
        [InlineData(1, 1)]   // floor(0.5) = 0, clamped to minimum 1
        [InlineData(7, 3)]   // floor(3.5) = 3
        [InlineData(100, 50)]
        public void SellPrice_is_half_BaseCost_floored_with_a_minimum_of_one(int baseCost, int expected)
        {
            var item = new ItemDef { Id = "x", BaseCost = baseCost };
            Assert.Equal(expected, Shop.SellPrice(item));
        }

        [Fact]
        public void CartTotal_prices_buying_at_BaseCost_and_selling_at_SellPrice()
        {
            var c = BuildContent();
            var cart = new Dictionary<string, int> { ["elixir"] = 3 };
            Assert.Equal(3 * c.Item("elixir").BaseCost, Shop.CartTotal(c, cart, selling: false));
            Assert.Equal(3 * Shop.SellPrice(c.Item("elixir")), Shop.CartTotal(c, cart, selling: true));
        }

        [Fact]
        public void Sellable_excludes_quest_items_and_zero_cost_items()
        {
            var c = BuildContent();
            var s = NewSave(0);
            s.AddItem("elixir", 1);
            s.AddItem("questkey", 1);
            s.AddItem("trash", 1);
            var sellable = Shop.Sellable(c, s);
            Assert.Contains(sellable, i => i.Id == "elixir");
            Assert.DoesNotContain(sellable, i => i.Id == "questkey");
            Assert.DoesNotContain(sellable, i => i.Id == "trash");
        }

        // ── Crafting.Craft / HasInputs ──

        [Fact]
        public void Craft_fails_on_gold_without_touching_gold_or_inventory()
        {
            var c = BuildContent();
            var s = NewSave(0);
            s.AddItem("herb", 2);
            var result = Crafting.Craft(c, s, c.Recipes["craft_elixir"]);
            Assert.False(result.Ok);
            Assert.Equal(0, s.Gold);
            Assert.Equal(2, s.Count("herb"));
            Assert.Equal(0, s.Count("elixir"));
        }

        [Fact]
        public void Craft_fails_on_missing_inputs_without_touching_gold_or_inventory()
        {
            var c = BuildContent();
            var s = NewSave(100);
            s.AddItem("herb", 1); // recipe needs 2
            var result = Crafting.Craft(c, s, c.Recipes["craft_elixir"]);
            Assert.False(result.Ok);
            Assert.Equal(100, s.Gold);
            Assert.Equal(1, s.Count("herb"));
            Assert.Equal(0, s.Count("elixir"));
            Assert.False(Crafting.HasInputs(s, c.Recipes["craft_elixir"]));
        }

        [Fact]
        public void Craft_fails_on_missing_BaseItem_without_touching_gold_or_inventory()
        {
            var c = BuildContent();
            var recipe = c.Recipes["upgrade_warhammer"];
            var s = NewSave(100);
            s.AddItem("ore", 1); // has the input, but no warhammer to upgrade
            Assert.False(Crafting.HasInputs(s, recipe));
            var result = Crafting.Craft(c, s, recipe);
            Assert.False(result.Ok);
            Assert.Equal(100, s.Gold);
            Assert.Equal(1, s.Count("ore"));
            Assert.Equal(0, s.Count("finehammer"));
        }

        [Fact]
        public void Craft_fails_at_the_result_stack_cap_without_touching_gold_or_inventory()
        {
            var c = BuildContent();
            var recipe = c.Recipes["craft_elixir"];
            var s = NewSave(100);
            s.AddItem("herb", 4);
            s.AddItem("elixir", c.Item("elixir").MaxStack); // already full
            var result = Crafting.Craft(c, s, recipe);
            Assert.False(result.Ok);
            Assert.Equal(100, s.Gold);
            Assert.Equal(4, s.Count("herb"));
            Assert.Equal(c.Item("elixir").MaxStack, s.Count("elixir"));
        }

        [Fact]
        public void HasInputs_is_true_only_once_every_input_and_the_base_item_are_present()
        {
            var c = BuildContent();
            var recipe = c.Recipes["upgrade_warhammer"];
            var s = NewSave(0);
            Assert.False(Crafting.HasInputs(s, recipe));
            s.AddItem("ore", 1);
            Assert.False(Crafting.HasInputs(s, recipe), "still missing the base item");
            s.AddItem("warhammer", 1);
            Assert.True(Crafting.HasInputs(s, recipe));
        }

        [Fact]
        public void SalvageYield_is_empty_when_there_is_no_base_forge_recipe()
        {
            var c = BuildContent();
            // "finehammer" is only produced by an upgrade recipe (BaseItem set), so it has no base forge recipe.
            Assert.Empty(Crafting.SalvageYield(c, c.Item("finehammer")));
            // "warhammer" does have one (craft_warhammer), so it should yield half its inputs.
            var yield = Crafting.SalvageYield(c, c.Item("warhammer"));
            Assert.Single(yield);
            Assert.Equal("ore", yield[0].Item);
            Assert.Equal(1, yield[0].Count); // half of 2, floored, minimum 1
        }

        // ── Repair ──

        [Fact]
        public void Repair_cost_is_zero_at_full_durability()
        {
            var item = new ItemDef { Id = "w", BaseCost = 100, Durability = 10 };
            Assert.Equal(0, Repair.Cost(item, Repair.EffectiveMax(item, 0), 0));
        }

        [Fact]
        public void Repair_cost_grows_roughly_1_6x_per_prior_repair()
        {
            var item = new ItemDef { Id = "w", BaseCost = 100, Durability = 100 };
            // Hold "missing" constant across repair counts by offsetting current with EffectiveMax, so only
            // the 1.6x-per-repair multiplier in Repair.Cost moves between calls.
            int cost0 = Repair.Cost(item, Repair.EffectiveMax(item, 0) - 10, 0);
            int cost1 = Repair.Cost(item, Repair.EffectiveMax(item, 1) - 10, 1);
            int cost2 = Repair.Cost(item, Repair.EffectiveMax(item, 2) - 10, 2);
            Assert.True(cost1 > cost0);
            Assert.True(cost2 > cost1);
            Assert.InRange((double)cost1 / cost0, 1.5, 1.8);
            Assert.InRange((double)cost2 / cost1, 1.5, 1.8);
        }

        [Fact]
        public void Repair_cost_never_drops_below_one_when_anything_is_missing()
        {
            var item = new ItemDef { Id = "w", BaseCost = 1, Durability = 1000 };
            Assert.Equal(1, Repair.Cost(item, Repair.EffectiveMax(item, 0) - 1, 0));
        }

        [Fact]
        public void EffectiveMax_floors_at_one_once_repairs_exceed_durability()
        {
            var item = new ItemDef { Id = "w", BaseCost = 10, Durability = 3 };
            Assert.Equal(1, Repair.EffectiveMax(item, 5));
        }

        [Fact]
        public void IsUneconomical_once_repeated_repairs_push_cost_to_or_past_BaseCost()
        {
            var item = new ItemDef { Id = "w", BaseCost = 10, Durability = 10 };
            Assert.False(Repair.IsUneconomical(item, 0, 0));
            Assert.True(Repair.IsUneconomical(item, 0, 4));
        }

        [Fact]
        public void Armory_discount_rounds_the_price_up_not_down()
        {
            var c = BuildContent();
            var s = NewSave(0);
            var item = new ItemDef { Id = "w", BaseCost = 100, Durability = 100 };
            // Pick a repair state whose undiscounted cost (7) times the 30% discount (0.7) lands on a
            // fraction (4.9), so a floor vs. ceiling would disagree.
            int current = Repair.EffectiveMax(item, 0) - 21;
            int full = Repair.Cost(item, current, 0);
            Assert.Equal(7, full);
            s.Caravan["armory"] = 1;
            int discounted = Repair.Cost(c, s, item, current, 0);
            Assert.True(discounted < full);
            Assert.Equal((int)Math.Ceiling(full * (1f - 0.3f)), discounted);
            Assert.NotEqual((int)Math.Floor(full * (1f - 0.3f)), discounted);
        }

        [Fact]
        public void RepairWeapon_fails_without_gold_and_succeeds_restoring_durability()
        {
            var c = BuildContent();
            var hero = Progression.NewHero(c, "knight");
            hero.WeaponId = "warhammer";
            hero.WeaponDurability = 1;
            hero.WeaponRepairs = 0;
            var s = NewSave(0);
            Assert.False(Repair.RepairWeapon(c, s, hero).Ok);
            Assert.Equal(1, hero.WeaponDurability);

            s.Gold = 10000;
            Assert.True(Repair.RepairWeapon(c, s, hero).Ok);
            Assert.Equal(Repair.EffectiveMax(c.Item("warhammer"), 0), hero.WeaponDurability);
            Assert.Equal(1, hero.WeaponRepairs);
        }

        [Fact]
        public void RepairArmor_fails_without_gold_and_succeeds_restoring_durability()
        {
            var c = BuildContent();
            var hero = Progression.NewHero(c, "knight");
            hero.ArmorId = "plate";
            hero.ArmorDurability = 1;
            hero.ArmorRepairs = 0;
            var s = NewSave(0);
            Assert.False(Repair.RepairArmor(c, s, hero).Ok);
            Assert.Equal(1, hero.ArmorDurability);

            s.Gold = 10000;
            Assert.True(Repair.RepairArmor(c, s, hero).Ok);
            Assert.Equal(Repair.EffectiveMax(c.Item("plate"), 0), hero.ArmorDurability);
            Assert.Equal(1, hero.ArmorRepairs);
        }

        // ── Inn ──

        [Fact]
        public void MissingHp_is_zero_at_full_health()
        {
            var c = BuildContent();
            var hero = Progression.NewHero(c, "knight");
            var s = NewSave(0);
            int max = Progression.Preview(c, hero).MaxHp;

            hero.Hp = 0; // save convention: 0 = full health
            Assert.Equal(0, Inn.MissingHp(c, s, hero));

            hero.Hp = max;
            Assert.Equal(0, Inn.MissingHp(c, s, hero));

            hero.Hp = max - 1;
            Assert.Equal(1, Inn.MissingHp(c, s, hero));
        }

        [Fact]
        public void Inn_cost_applies_the_medic_wagon_premium_only_while_on_an_expedition()
        {
            var c = BuildContent();
            var hero = Progression.NewHero(c, "knight");
            int max = Progression.Preview(c, hero).MaxHp;
            int missing = Math.Min(15, max - 1);
            hero.Hp = max - missing;
            var s = NewSave(0);
            s.Roster.Add(hero);

            int homeCost = Inn.Cost(c, s);
            Assert.Equal((int)Math.Ceiling(missing * Inn.GoldPerHp), homeCost);

            s.Caravan["medic_wagon"] = 1; // Levels[0].Value = 2x premium
            s.Expedition = new ExpeditionSave();
            int roadCost = Inn.Cost(c, s);
            Assert.Equal((int)Math.Ceiling(missing * Inn.GoldPerHp * 2f), roadCost);
            Assert.True(roadCost > homeCost);
        }
    }
}
