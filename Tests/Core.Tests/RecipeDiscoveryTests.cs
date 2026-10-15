using System.Linq;
using Gaiden.Core.Content;
using Gaiden.Meta;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>Recipe discovery (§24.6.2, GG-T36): recipes start hidden and are learned from a recipe
    /// scroll, first-ever pickup of the recipe's rarest input, or the station reaching the recipe's tier.</summary>
    public class RecipeDiscoveryTests
    {
        [Fact]
        public void Tier_one_recipes_are_discovered_from_the_start_higher_tiers_are_hidden()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco1");
            var t1 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 1);
            var t2 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 2);
            var t3 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 3);
            Assert.True(RecipeDiscovery.IsDiscovered(s, t1));
            Assert.False(RecipeDiscovery.IsDiscovered(s, t2));
            Assert.False(RecipeDiscovery.IsDiscovered(s, t3));
        }

        [Fact]
        public void Reaching_a_station_tier_discovers_its_recipes_without_ever_touching_the_discovered_set()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco2");
            var t1 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 1);
            var t2 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 2);
            Assert.False(RecipeDiscovery.IsDiscovered(s, t2));

            void CraftT1()
            {
                s.Gold += t1.Gold;
                foreach (var ic in t1.Inputs) s.AddItem(ic.Item, ic.Count);
                Assert.True(Crafting.Craft(c, s, t1).Ok);
                s.AddItem(t1.Result, -s.Count(t1.Result));
            }
            for (int i = 0; i < 20 && StationProgress.Level(s, StationKind.Blacksmith) < 2; i++) CraftT1();

            Assert.Equal("Journeyman", StationProgress.LevelName(s, StationKind.Blacksmith));
            Assert.True(RecipeDiscovery.IsDiscovered(s, t2), "reaching Journeyman should discover its tier-2 recipes");
            Assert.DoesNotContain(t2.Id, s.DiscoveredRecipes); // derived from the tier reached, not stored
        }

        [Fact]
        public void First_ever_pickup_of_a_recipes_rarest_input_discovers_it_even_while_still_tier_locked()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco3");
            var recipe = c.Recipe("recipe_phoenix_down"); // rarest input: mat_ghost_ectoplasm (Epic)
            var rarest = RecipeDiscovery.RarestInput(c, recipe);
            Assert.Equal("mat_ghost_ectoplasm", rarest.Id);
            Assert.False(RecipeDiscovery.IsDiscovered(s, recipe));

            // Picking up a common input first shouldn't discover it.
            RecipeDiscovery.OnItemAcquired(c, s, "mat_arcane_dust");
            Assert.False(RecipeDiscovery.IsDiscovered(s, recipe));

            RecipeDiscovery.OnItemAcquired(c, s, "mat_ghost_ectoplasm");
            Assert.True(RecipeDiscovery.IsDiscovered(s, recipe));
            Assert.False(Crafting.IsUnlocked(s, recipe)); // discovered early, still locked by tier
        }

        [Fact]
        public void Learn_reads_a_scroll_and_discovers_its_recipe_then_consumes_it()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco4");
            var scroll = c.Item("scroll_recipe_holy_water");
            var recipe = c.Recipe(scroll.TeachesRecipe);
            s.AddItem(scroll.Id, 1);
            Assert.False(RecipeDiscovery.IsDiscovered(s, recipe));

            var res = RecipeDiscovery.Learn(c, s, scroll.Id);
            Assert.True(res.Ok, res.Message);
            Assert.Equal(0, s.Count(scroll.Id));
            Assert.True(RecipeDiscovery.IsDiscovered(s, recipe));
        }

        [Fact]
        public void Learn_fails_without_the_scroll_in_hand_or_for_a_non_scroll_item()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco5");
            Assert.False(RecipeDiscovery.Learn(c, s, "scroll_recipe_holy_water").Ok); // none owned
            Assert.False(RecipeDiscovery.Learn(c, s, "mat_iron_ore").Ok); // not a scroll
        }

        [Fact]
        public void OneAwayMissing_flags_exactly_one_missing_input_and_nothing_else()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco6");
            var recipe = c.Recipe("recipe_potion_x"); // mat_slime_gel x10, mat_arcane_dust x5, mat_demon_shard x1 — tier 3
            Assert.Null(RecipeDiscovery.OneAwayMissing(s, recipe)); // owns nothing: more than one short

            s.AddItem("mat_slime_gel", 10);
            s.AddItem("mat_arcane_dust", 5);
            var missing = RecipeDiscovery.OneAwayMissing(s, recipe);
            Assert.NotNull(missing);
            Assert.Equal("mat_demon_shard", missing.Item);

            s.AddItem("mat_demon_shard", 1); // now fully stocked but still undiscovered
            Assert.Null(RecipeDiscovery.OneAwayMissing(s, recipe));
            Assert.False(RecipeDiscovery.IsDiscovered(s, recipe));
        }

        [Fact]
        public void OneAwayMissing_counts_a_missing_base_item_and_catches_two_things_missing_at_once()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco7");
            var upgrade = Crafting.Recipes(c, CraftStation.Forge).First(r => r.BaseItem != null); // upgrade: base item + inputs
            Assert.Null(RecipeDiscovery.OneAwayMissing(s, upgrade)); // missing the base item AND every input: more than one short

            foreach (var ic in upgrade.Inputs) s.AddItem(ic.Item, ic.Count); // inputs stocked, base item still missing
            var missing = RecipeDiscovery.OneAwayMissing(s, upgrade);
            Assert.NotNull(missing);
            Assert.Equal(upgrade.BaseItem, missing.Item);

            s.AddItem(upgrade.Inputs[0].Item, -upgrade.Inputs[0].Count); // now missing both the base item and an input
            Assert.Null(RecipeDiscovery.OneAwayMissing(s, upgrade));
        }

        [Fact]
        public void Every_recipe_scroll_item_teaches_a_real_recipe()
        {
            var c = Real;
            foreach (var i in c.Items.Values.Where(i => i.Type == ItemType.RecipeScroll))
            {
                Assert.NotNull(i.TeachesRecipe);
                Assert.True(c.Recipes.ContainsKey(i.TeachesRecipe), $"{i.Id}: unknown recipe '{i.TeachesRecipe}'");
            }
        }
    }
}
