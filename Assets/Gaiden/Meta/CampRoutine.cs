using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;

namespace Gaiden.Meta
{
    /// <summary>
    /// "What a sensible player does at camp": scrap worn-out gear, rest, repair, forge and equip better
    /// gear (owned bag, then Forge, then the Merchant's Bargain shelf), learn a spell, fill empty bar
    /// slots, keep potions stocked, work the Bounty Board and recruit at the Summon Circle into an open
    /// party slot (GG-T87). Used by the balance simulation and the in-game auto-manage helper;
    /// every step goes through the same services the screens call.
    /// </summary>
    public static class CampRoutine
    {
        /// <summary>A Bargain offer below this fraction of max durability is skipped (GG-T54): the shelf's
        /// §25.1 gamble (10-40% durability left, a real risk of shattering before it can be repaired) is a
        /// deliberate player choice — an automated buyer can't judge that risk, so it only takes the safer
        /// rolls of the range.</summary>
        public const float MinSafeBargainDurabilityFraction = 0.25f;

        public static List<string> Run(ContentDb c, SaveData s)
        {
            var log = new List<string>();
            void Note(Result r) { if (r.Ok && r.Message != null) log.Add(r.Message); }

            // Broken fragments (GG-T41) can't be equipped or repaired — only scrapped — so there's never a
            // reason to let them sit in the bag; salvage every copy of every one.
            foreach (var itemId in s.Broken.Keys.ToList())
            {
                Result r;
                do { r = Crafting.SalvageBroken(c, s, itemId); Note(r); } while (r.Ok && s.BrokenCount(itemId) > 0);
            }

            // A repair that costs as much as the item is worth (GG-T42's "Salvage instead" offer) is a
            // losing trade — scrap it for materials instead of pouring gold into a piece about to shatter anyway.
            foreach (var h in s.Roster)
            {
                if (h.WeaponId != null)
                {
                    var w = c.TryItem(h.WeaponId);
                    if (w != null && Repair.IsUneconomical(w, h.WeaponDurability, h.WeaponRepairs, h.WeaponMastery)) Note(Crafting.SalvageEquipped(c, s, h, EquipmentSlot.Weapon));
                    else Note(Repair.RepairWeapon(c, s, h));
                }
                foreach (var slot in BattleEngine.ArmorSlots)
                {
                    if (!h.Armor.TryGetValue(slot, out var piece)) continue;
                    var a = c.TryItem(piece.ItemId);
                    if (a != null && Repair.IsUneconomical(a, piece.Durability, piece.Repairs, piece.Mastery)) Note(Crafting.SalvageEquipped(c, s, h, slot));
                    else Note(Repair.RepairArmor(c, s, h, slot));
                }
            }
            if (Inn.Cost(c, s) > 0 && Inn.Cost(c, s) <= s.Gold) Note(Inn.Rest(c, s));

            // Bounty Board (GG-T87): cash in a finished contract, drop one whose target no longer appears
            // anywhere ahead, then take the one whose target shows up soonest on the road ahead.
            ManageBounty(c, s, Note);

            // Summon Circle (GG-T87): fill an open party slot with a recruit when the gold is there — done
            // before the gear pass so the newcomer gets equipped on this same visit.
            if (s.Party.Count < Progression.MaxPartySize && Summon.Price(c, s) <= s.Gold)
            {
                var recruit = Summon.Pool(c, s).FirstOrDefault(d => s.Hero(d.Id) == null);
                if (recruit != null) Note(Summon.Recruit(c, s, recruit.Id));
            }

            // The best affordable upgrade for each party member's weapon and armor: an owned bag copy
            // first, then a Forge recipe, then the Merchant's Bargain shelf (skipping near-shatter rolls).
            foreach (var id in s.Party)
            {
                var h = s.Hero(id);
                foreach (var slot in new[] { EquipmentSlot.Weapon }.Concat(BattleEngine.ArmorSlots))
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
                    if (owned == null || MetaHeuristics.Score(owned) <= current)
                    {
                        var bargain = Bargains.Shelf(c, s)
                            .Where(o => Equipment.Fits(o.Item, slot) && o.Price <= s.Gold / 2 && o.Durability >= o.Item.Durability * MinSafeBargainDurabilityFraction)
                            .OrderByDescending(o => MetaHeuristics.Score(o.Item)).FirstOrDefault();
                        if (bargain != null && MetaHeuristics.Score(bargain.Item) > current)
                        {
                            var bought = Bargains.Buy(c, s, bargain.Item.Id);
                            Note(bought);
                            if (bought.Ok) owned = bargain.Item;
                        }
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
            while (potion != null && s.Count(potion.Id) < System.Math.Min(6, potion.MaxStack))
            {
                if (brew != null && Crafting.IsUnlocked(s, brew) && Crafting.HasInputs(s, brew) && s.Gold >= brew.Gold)
                    Note(Crafting.Craft(c, s, brew));
                else if (s.Gold >= potion.BaseCost * 3)
                    Note(Shop.Buy(c, s, new Dictionary<string, int> { [potion.Id] = System.Math.Min(3, Shop.MaxBuy(s, potion)) }));
                else break;
            }
            return log;
        }

        /// <summary>Claim a completed bounty; abandon an active one the stages still ahead can no longer finish;
        /// with none active, accept the posted contract the road ahead completes soonest (cheapest reward
        /// breaks ties). A playthrough meets each stage once, so a contract is only "achievable" if the
        /// uncleared campaign stages spawn enough of its target — e.g. Slime Culling wants 5 slime00 kills
        /// but the whole campaign spawns 3, so this player (who doesn't grind replays) never takes it.</summary>
        private static void ManageBounty(ContentDb c, SaveData s, System.Action<Result> note)
        {
            if (Bounties.IsComplete(c, s)) note(Bounties.Claim(c, s));
            var ahead = c.CampaignStages().Skip(s.HighestClearedStage + 1).ToList();
            // Index (into `ahead`) of the stage that would bring the count to `needed`, or -1 if it never gets there.
            int CompletesAt(string classId, int needed)
            {
                int seen = 0;
                for (int i = 0; i < ahead.Count; i++)
                {
                    seen += ahead[i].Waves.Sum(w => w.Enemies.Count(e => e.Class == classId));
                    if (seen >= needed) return i;
                }
                return -1;
            }
            var active = Bounties.Active(c, s);
            if (active != null && CompletesAt(active.TargetClass, active.Kills - s.Bounty.Progress) < 0) note(Bounties.Abandon(s));
            if (Bounties.Active(c, s) != null) return;
            var pick = Bounties.Posted(c, s)
                .Select(b => (b, at: CompletesAt(b.TargetClass, b.Kills)))
                .Where(x => x.at >= 0)
                .OrderBy(x => x.at).ThenBy(x => x.b.RewardGold)
                .Select(x => x.b).FirstOrDefault();
            if (pick != null) note(Bounties.Accept(c, s, pick.Id));
        }
    }
}
