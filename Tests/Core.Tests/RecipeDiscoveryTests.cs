using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Recipe discovery (§24.6.2, GG-T36): recipes start hidden and are learned from a recipe
    /// scroll, first-ever pickup of the recipe's rarest input, or the station reaching the recipe's tier.</summary>
    public class RecipeDiscoveryTests
    {
        [Test]
        public void Tier_one_recipes_are_discovered_from_the_start_higher_tiers_are_hidden()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco1");
            var t1 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 1);
            var t2 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 2);
            var t3 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 3);
            Assert.That(RecipeDiscovery.IsDiscovered(s, t1), Is.True);
            Assert.That(RecipeDiscovery.IsDiscovered(s, t2), Is.False);
            Assert.That(RecipeDiscovery.IsDiscovered(s, t3), Is.False);
        }

        [Test]
        public void Reaching_a_station_tier_discovers_its_recipes_without_ever_touching_the_discovered_set()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco2");
            var t1 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 1);
            var t2 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 2);
            Assert.That(RecipeDiscovery.IsDiscovered(s, t2), Is.False);

            void CraftT1()
            {
                s.Gold += t1.Gold;
                foreach (var ic in t1.Inputs) s.AddItem(ic.Item, ic.Count);
                Assert.That(Crafting.Craft(c, s, t1).Ok, Is.True);
                s.AddItem(t1.Result, -s.Count(t1.Result));
            }
            for (int i = 0; i < 20 && StationProgress.Level(s, StationKind.Blacksmith) < 2; i++) CraftT1();

            Assert.That(StationProgress.LevelName(s, StationKind.Blacksmith), Is.EqualTo("Journeyman"));
            Assert.That(RecipeDiscovery.IsDiscovered(s, t2), Is.True, "reaching Journeyman should discover its tier-2 recipes");
            Assert.That(s.DiscoveredRecipes, Does.Not.Contain(t2.Id)); // derived from the tier reached, not stored
        }

        [Test]
        public void First_ever_pickup_of_a_recipes_rarest_input_discovers_it_even_while_still_tier_locked()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco3");
            var recipe = c.Recipe("recipe_revival_charm"); // rarest input: mat_ghost_ectoplasm (Epic)
            var rarest = RecipeDiscovery.RarestInput(c, recipe);
            Assert.That(rarest.Id, Is.EqualTo("mat_ghost_ectoplasm"));
            Assert.That(RecipeDiscovery.IsDiscovered(s, recipe), Is.False);

            // Picking up a common input first shouldn't discover it.
            RecipeDiscovery.OnItemAcquired(c, s, "mat_arcane_dust");
            Assert.That(RecipeDiscovery.IsDiscovered(s, recipe), Is.False);

            RecipeDiscovery.OnItemAcquired(c, s, "mat_ghost_ectoplasm");
            Assert.That(RecipeDiscovery.IsDiscovered(s, recipe), Is.True);
            Assert.That(Crafting.IsUnlocked(s, recipe), Is.False); // discovered early, still locked by tier
        }

        [Test]
        public void Learn_reads_a_scroll_and_discovers_its_recipe_then_consumes_it()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco4");
            var scroll = c.Item("scroll_recipe_holy_water");
            var recipe = c.Recipe(scroll.TeachesRecipe);
            s.AddItem(scroll.Id, 1);
            Assert.That(RecipeDiscovery.IsDiscovered(s, recipe), Is.False);

            var res = RecipeDiscovery.Learn(c, s, scroll.Id);
            Assert.That(res.Ok, Is.True, res.Message);
            Assert.That(s.Count(scroll.Id), Is.EqualTo(0));
            Assert.That(RecipeDiscovery.IsDiscovered(s, recipe), Is.True);
        }

        [Test]
        public void Learn_fails_without_the_scroll_in_hand_or_for_a_non_scroll_item()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco5");
            Assert.That(RecipeDiscovery.Learn(c, s, "scroll_recipe_holy_water").Ok, Is.False); // none owned
            Assert.That(RecipeDiscovery.Learn(c, s, "mat_iron_ore").Ok, Is.False); // not a scroll
        }

        [Test]
        public void OneAwayMissing_flags_exactly_one_missing_input_and_nothing_else()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco6");
            var recipe = c.Recipe("recipe_potion_x"); // mat_slime_gel x10, mat_arcane_dust x5, mat_demon_shard x1 — tier 3
            Assert.That(RecipeDiscovery.OneAwayMissing(s, recipe), Is.Null); // owns nothing: more than one short

            s.AddItem("mat_slime_gel", 10);
            s.AddItem("mat_arcane_dust", 5);
            var missing = RecipeDiscovery.OneAwayMissing(s, recipe);
            Assert.That(missing, Is.Not.Null);
            Assert.That(missing.Item, Is.EqualTo("mat_demon_shard"));

            s.AddItem("mat_demon_shard", 1); // now fully stocked but still undiscovered
            Assert.That(RecipeDiscovery.OneAwayMissing(s, recipe), Is.Null);
            Assert.That(RecipeDiscovery.IsDiscovered(s, recipe), Is.False);
        }

        [Test]
        public void OneAwayMissing_counts_a_missing_base_item_and_catches_two_things_missing_at_once()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Disco7");
            var upgrade = Crafting.Recipes(c, CraftStation.Forge).First(r => r.BaseItem != null); // upgrade: base item + inputs
            Assert.That(RecipeDiscovery.OneAwayMissing(s, upgrade), Is.Null); // missing the base item AND every input: more than one short

            foreach (var ic in upgrade.Inputs) s.AddItem(ic.Item, ic.Count); // inputs stocked, base item still missing
            var missing = RecipeDiscovery.OneAwayMissing(s, upgrade);
            Assert.That(missing, Is.Not.Null);
            Assert.That(missing.Item, Is.EqualTo(upgrade.BaseItem));

            s.AddItem(upgrade.Inputs[0].Item, -upgrade.Inputs[0].Count); // now missing both the base item and an input
            Assert.That(RecipeDiscovery.OneAwayMissing(s, upgrade), Is.Null);
        }

        [Test]
        public void Every_recipe_scroll_item_teaches_a_real_recipe()
        {
            var c = Real;
            foreach (var i in c.Items.Values.Where(i => i.Type == ItemType.RecipeScroll))
            {
                Assert.That(i.TeachesRecipe, Is.Not.Null);
                Assert.That(c.Recipes.ContainsKey(i.TeachesRecipe), Is.True, $"{i.Id}: unknown recipe '{i.TeachesRecipe}'");
            }
        }
    }
}
