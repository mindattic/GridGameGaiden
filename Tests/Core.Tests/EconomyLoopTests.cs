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

        [Test, Ignore("GG-T176 turns the 36 unused monster trophies into alchemy potion inputs; remove this then")]
        public void Every_dropped_crafting_material_feeds_at_least_one_recipe()
        {
            var inputs = RecipeInputs();
            var orphans = Dropped().Select(C.TryItem)
                .Where(i => i != null && i.Type == ItemType.CraftingMaterial && !inputs.Contains(i.Id))
                .Select(i => i.Id).OrderBy(x => x).ToList();
            Assert.That(orphans, Is.Empty, "dropped materials no recipe uses: " + string.Join(", ", orphans));
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
