using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T136: the content half of "every system hands off to the next" — nothing a fight drops is a dead
    /// end, and nothing a station asks for is unobtainable.</summary>
    public class EconomyLoopTests
    {
        private static ContentDb C => TestKit.Real;

        private static HashSet<string> RecipeInputs() =>
            new HashSet<string>(C.Recipes.Values.SelectMany(r =>
                r.Inputs.Select(i => i.Item).Concat(r.PotencyInputs.Select(i => i.Item)).Concat(r.BaseItem != null ? new[] { r.BaseItem } : new string[0])),
                System.StringComparer.OrdinalIgnoreCase);

        private static HashSet<string> Dropped() =>
            new HashSet<string>(C.DropTables.Values.SelectMany(d => d.Entries.Select(e => e.Item))
                .Concat(C.Bounties.Values.Where(b => b.RewardItem != null).Select(b => b.RewardItem)), System.StringComparer.OrdinalIgnoreCase);

        [Test]
        public void Every_dropped_crafting_material_feeds_at_least_one_recipe()
        {
            var inputs = RecipeInputs();
            var orphans = Dropped().Select(C.TryItem)
                .Where(i => i != null && i.Type == ItemType.CraftingMaterial && !inputs.Contains(i.Id))
                .Select(i => i.Id).OrderBy(x => x).ToList();
            Assert.That(orphans, Is.Empty, "dropped materials no recipe uses: " + string.Join(", ", orphans));
        }

        /// <summary>GG-T176 (§25.0.5): the Merchant sells Empty Bottles cheaply, and every bottle recipe brews from one
        /// bottle plus looted trophies for less than the potion costs to buy — even with every input bought.</summary>
        [Test]
        public void Bottle_recipes_brew_from_loot_for_less_than_buying_the_potion()
        {
            var bottle = C.Item(ContentLoader.EmptyBottle);
            Assert.That(Shop.Stock(C), Has.Member(bottle), "the Merchant sells Empty Bottles");
            Assert.That(bottle.BaseCost, Is.InRange(1, 5), "cheaply");
            var recipes = Brewing.BottleRecipes(C);
            Assert.That(recipes.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(recipes.Where(r => r.Tier <= 1), Is.Not.Empty, "a first brew is open from the start");
            var dropped = Dropped();
            foreach (var r in recipes)
            {
                Assert.That(C.Item(r.Result).Type, Is.EqualTo(ItemType.Consumable), r.Id);
                Assert.That(r.Inputs.Where(i => i.Item == ContentLoader.EmptyBottle).Sum(i => i.Count), Is.EqualTo(1), r.Id + ": one bottle");
                foreach (var i in r.Inputs.Where(i => i.Item != ContentLoader.EmptyBottle))
                    Assert.That(dropped, Does.Contain(i.Item), $"{r.Id}: {i.Item} is looted from enemies");
                Assert.That(Brewing.ShopCost(C, r), Is.LessThan(Brewing.BuyCost(C, r)), r.Id + ": cheaper than buying");
            }
        }

        [Test]
        public void The_camp_routine_buys_a_bottle_to_brew_its_loot()
        {
            var s = Progression.NewGame(C, "T");
            CampNpcs.JoinAll(s);
            s.Gold = 50;
            s.AddItem("mat_wolf_pelt", 1); s.AddItem("mat_tattered_cloth", 1);
            CampRoutine.Run(C, s);
            Assert.That(s.Count("mat_wolf_pelt"), Is.EqualTo(0), "the pelt went into a Healing Potion");
            Assert.That(s.Count(CampRoutine.StopgapPotion), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Every_recipe_input_can_be_obtained()
        {
            var obtainable = Dropped();
            obtainable.UnionWith(Shop.Stock(C).Select(i => i.Id));
            obtainable.UnionWith(C.Recipes.Values.Select(r => r.Result));
            obtainable.UnionWith(C.Recipes.Values.Where(r => r.PotencyResult != null).Select(r => r.PotencyResult));
            var unobtainable = RecipeInputs().Where(id => !obtainable.Contains(id)).OrderBy(x => x).ToList();
            Assert.That(unobtainable, Is.Empty, "recipe inputs no drop, shop or recipe provides: " + string.Join(", ", unobtainable));
        }

        [Test]
        public void Every_dropped_item_is_sellable_or_usable()
        {
            var dead = Dropped().Select(C.TryItem)
                .Where(i => i != null && i.Type == ItemType.CraftingMaterial && i.BaseCost <= 0 && !RecipeInputs().Contains(i.Id))
                .Select(i => i.Id).ToList();
            Assert.That(dead, Is.Empty, "dropped items with no sale value and no use: " + string.Join(", ", dead));
        }
    }
}
