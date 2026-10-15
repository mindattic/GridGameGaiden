using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;

namespace Gaiden.Meta
{
    /// <summary>
    /// "What a sensible player does at camp": rest, repair, forge and equip better gear, learn a spell,
    /// fill empty bar slots, keep potions stocked. Used by the balance simulation and the in-game
    /// auto-manage helper; every step goes through the same services the screens call.
    /// </summary>
    public static class CampRoutine
    {
        public static List<string> Run(ContentDb c, SaveData s)
        {
            var log = new List<string>();
            void Note(Result r) { if (r.Ok && r.Message != null) log.Add(r.Message); }

            foreach (var h in s.Roster)
            {
                if (h.WeaponId != null) Note(Repair.RepairWeapon(c, s, h));
                if (h.ArmorId != null) Note(Repair.RepairArmor(c, s, h));
            }
            if (Inn.Cost(c, s) > 0 && Inn.Cost(c, s) <= s.Gold) Note(Inn.Rest(c, s));

            // Forge the best affordable upgrade for each party member's weapon and armor.
            foreach (var id in s.Party)
            {
                var h = s.Hero(id);
                foreach (var slot in new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor })
                {
                    float current = MetaHeuristics.Score(c.TryItem(Equipment.Equipped(h, slot)));
                    var owned = MetaHeuristics.BestFirst(Equipment.Candidates(c, s, slot)).FirstOrDefault();
                    if (owned == null || MetaHeuristics.Score(owned) <= current)
                    {
                        var recipe = Crafting.Recipes(c, CraftStation.Forge)
                            .Where(r => r.BaseItem == null && Equipment.Fits(c.Item(r.Result), slot) && Crafting.HasInputs(s, r) && r.Gold <= s.Gold / 2)
                            .OrderByDescending(r => MetaHeuristics.Score(c.Item(r.Result))).FirstOrDefault();
                        if (recipe != null && MetaHeuristics.Score(c.Item(recipe.Result)) > current) Note(Crafting.Craft(c, s, recipe));
                        owned = MetaHeuristics.BestFirst(Equipment.Candidates(c, s, slot)).FirstOrDefault();
                    }
                    if (owned != null && MetaHeuristics.Score(owned) > current) Note(Equipment.Equip(c, s, h, slot, owned.Id));
                }
            }

            // Learn one affordable spell per hero when gold allows, and slot it.
            int unlocked = Progression.UnlockedSlots(s);
            foreach (var id in s.Party)
            {
                var h = s.Hero(id);
                var learn = Abilities.Learnable(c, h).FirstOrDefault(a => (a.Kind == AbilityKind.Spell || a.Universal) && Abilities.Price(a) <= s.Gold / 3);
                if (learn != null) Note(Abilities.Learn(c, s, h, learn.Id));
                for (int i = 0; i < unlocked; i++)
                {
                    if (h.Bar[i] != null) continue;
                    var next = h.Known.FirstOrDefault(k => !h.Bar.Contains(k));
                    if (next != null) Abilities.Assign(c, s, h, i, next);
                    else if (s.Count("healing_potion_basic") > 0 && !h.Bar.Contains("item:healing_potion_basic")) Abilities.Assign(c, s, h, i, "item:healing_potion_basic");
                }
            }

            // Keep a potion stock — brewing it (reagents from field drops) undercuts the shop price
            // (GG-T44), so brew first and only fall back to buying once the reagents run dry.
            var potion = c.TryItem("healing_potion_basic");
            var brew = c.Recipes.Values.FirstOrDefault(r => r.Station == CraftStation.Alchemy && r.Result == "healing_potion_basic");
            while (potion != null && s.Count(potion.Id) < 6)
            {
                if (brew != null && Crafting.IsUnlocked(s, brew) && Crafting.HasInputs(s, brew) && s.Gold >= brew.Gold)
                    Note(Crafting.Craft(c, s, brew));
                else if (s.Gold >= potion.BaseCost * 3)
                    Note(Shop.Buy(c, s, new Dictionary<string, int> { [potion.Id] = System.Math.Min(3, Shop.MaxBuy(s, potion)) }));
                else break;
            }
            return log;
        }
    }
}
