using System;
using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;

namespace Gaiden.Meta
{
    public readonly struct Result
    {
        public readonly bool Ok;
        public readonly string Message;
        private Result(bool ok, string message) { Ok = ok; Message = message; }
        public static Result Success(string message = null) => new Result(true, message);
        public static Result Fail(string message) => new Result(false, message);
        public override string ToString() => (Ok ? "OK " : "FAIL ") + Message;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // New game / progression
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Progression
    {
        public static readonly string[] StartingRoster = { "paladin", "barbarian", "cleric" };
        public const int StartingGold = 200;
        public const int MaxPartySize = 4;
        public const int MaxBarSlots = 5;
        /// <summary>Slot unlocks: 2 on a fresh save, +1 when the highest cleared campaign index reaches each threshold (§4.7).</summary>
        public static readonly int[] UnlockThresholds = { 0, 2, 5 };

        public static SaveData NewGame(ContentDb c, string name)
        {
            var s = new SaveData { Name = string.IsNullOrWhiteSpace(name) ? "Lightbearer" : name.Trim(), Gold = StartingGold };
            foreach (var id in StartingRoster)
            {
                if (c.TryClass(id) == null) continue;
                var h = NewHero(c, id);
                s.Roster.Add(h);
                s.Party.Add(id);
            }
            foreach (var h in s.Roster)
                foreach (var e in h.Bar)
                    if (e != null && e.StartsWith("item:")) s.AddItem(e.Substring(5), 3);
            return s;
        }

        public static HeroSave NewHero(ContentDb c, string classId)
        {
            var def = c.Class(classId);
            var h = new HeroSave { ClassId = def.Id };
            int i = 0;
            foreach (var entry in def.DefaultLoadout)
            {
                if (i >= MaxBarSlots) break;
                if (!entry.StartsWith("item:") && !h.Known.Contains(entry)) h.Known.Add(entry);
                if (!entry.StartsWith("item:") || !h.Bar.Contains(entry)) h.Bar[i++] = entry;
            }
            return h;
        }

        public static int Level(HeroSave h) => Formulas.LevelFromTotalXp(h.TotalXp).level;

        public static int UnlockedSlots(SaveData s)
        {
            int n = 2;
            foreach (var t in UnlockThresholds) if (s.HighestClearedStage >= t) n++;
            return Math.Min(MaxBarSlots, n);
        }

        public static bool IsStageUnlocked(SaveData s, int campaignIndex) => campaignIndex <= s.HighestClearedStage + 1;

        public static int RecommendedLevel(int campaignIndex) => campaignIndex + 1;

        public static bool CanStartNewGamePlus(ContentDb c, SaveData s)
        {
            var all = c.CampaignStages();
            return all.Count > 0 && s.HighestClearedStage >= all.Count - 1;
        }

        /// <summary>New Game+ (US-332): replay the campaign from the start with everything you own; enemies +10 levels per cycle.</summary>
        public static Result StartNewGamePlus(ContentDb c, SaveData s)
        {
            if (!CanStartNewGamePlus(c, s)) return Result.Fail("Clear the whole campaign first");
            s.NewGamePlus++;
            s.HighestClearedStage = -1;
            s.ClearedStages.Clear();
            s.SeenCrawls.Clear();
            return Result.Success($"New Game+ {s.NewGamePlus} — enemies grow stronger");
        }

        public static Unit Preview(ContentDb c, HeroSave h)
        {
            var spec = BattleFactory.SpecFor(c, h);
            var setup = new BattleSetup
            {
                Stage = new StageDef { Id = "_preview", IsTest = true, Waves = { new WaveDef() } },
                Party = { spec },
            };
            var state = BattleEngine.Create(c, setup, new List<BattleEvent>());
            return state.Units.First(u => u.IsHero);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Battle bridge: save → BattleSetup, BattleResults → save (§22.1)
    // ─────────────────────────────────────────────────────────────────────────────

    public sealed class BattleReport
    {
        public bool Victory;
        public bool Escaped;
        public string StageId;
        public int Gold;
        public int XpEach;
        public Dictionary<string, int> Items = new Dictionary<string, int>();
        public List<(string classId, int fromLevel, int toLevel)> LevelUps = new List<(string, int, int)>();
        public bool FirstClear;
        public string UnlockedStage;
        public int NewSlots;
        public string BountyProgress;
        /// <summary>Loot past an item's stack limit is sold on the spot (inventory policy, US-245).</summary>
        public int AutoSold;
        public bool CampaignComplete;
    }

    public static class BattleFactory
    {
        public static HeroSpec SpecFor(ContentDb c, HeroSave h)
        {
            var spec = new HeroSpec { ClassId = h.ClassId, Level = Progression.Level(h), Hp = h.Hp, WeaponId = h.WeaponId, WeaponDurability = h.WeaponDurability, ArmorId = h.ArmorId, ArmorDurability = h.ArmorDurability };
            if (h.WeaponId != null) spec.EquippedItemIds.Add(h.WeaponId);
            if (h.ArmorId != null) spec.EquippedItemIds.Add(h.ArmorId);
            foreach (var r in h.Relics) if (r != null) spec.EquippedItemIds.Add(r);
            spec.Bar = new List<string>(h.Bar);
            return spec;
        }

        public static BattleSetup Build(ContentDb c, SaveData s, string stageId, int seed)
        {
            var stage = c.Stage(stageId);
            int index = c.CampaignStages().FindIndex(x => x.Id == stage.Id);
            var setup = new BattleSetup
            {
                Stage = stage,
                Seed = seed,
                UnlockedSlots = Progression.UnlockedSlots(s),
                RecommendedLevel = index >= 0 ? Progression.RecommendedLevel(index) : 1,
                NewGamePlus = s.NewGamePlus,
            };
            foreach (var id in s.Party)
            {
                var h = s.Hero(id);
                if (h == null) continue;
                var spec = SpecFor(c, h);
                foreach (var kv in s.Inventory) spec.ItemCounts[kv.Key] = kv.Value;
                setup.Party.Add(spec);
            }
            return setup;
        }

        /// <summary>Commits a finished battle into the save (XP, gold, loot, wounds, durability, kills, bestiary, unlocks).</summary>
        public static BattleReport Commit(ContentDb c, SaveData s, BattleState battle)
        {
            var r = battle.Results;
            var rep = new BattleReport { Victory = battle.Phase == BattlePhase.Victory, Escaped = battle.Phase == BattlePhase.Escaped, StageId = battle.Stage.Id };
            int slotsBefore = Progression.UnlockedSlots(s);

            foreach (var kv in r.ItemsConsumed) s.AddItem(kv.Key, -kv.Value);
            foreach (var id in r.Seen) Entry(s, id).Seen = true;
            foreach (var id in r.Scanned) Entry(s, id).Scanned = true;

            if (rep.Victory)
            {
                rep.Gold = r.Coins;
                s.Gold += r.Coins;
                s.LifetimeCoins += r.Coins;
                foreach (var kv in r.Items)
                {
                    var def = c.TryItem(kv.Key);
                    int room = def == null ? kv.Value : Math.Max(0, def.MaxStack - s.Count(kv.Key));
                    int keep = Math.Min(room, kv.Value), extra = kv.Value - keep;
                    if (keep > 0) { s.AddItem(kv.Key, keep); rep.Items[kv.Key] = keep; }
                    if (extra > 0 && def != null) { int g = extra * Shop.SellPrice(def); s.Gold += g; rep.Gold += g; rep.AutoSold += extra; }
                }
                foreach (var kv in r.Kills) Entry(s, kv.Key).Defeated += kv.Value;

                var heroes = battle.Units.Where(u => u.IsHero).ToList();
                rep.XpEach = heroes.Count > 0 ? r.Xp : 0;
                foreach (var u in heroes)
                {
                    var h = s.Hero(u.Def.Id);
                    if (h == null) continue;
                    int before = Progression.Level(h);
                    h.TotalXp += rep.XpEach;
                    int after = Progression.Level(h);
                    if (after > before) rep.LevelUps.Add((h.ClassId, before, after));
                    // Wounds carry; a hero who fell revives at 1 HP; a level-up refreshes to full.
                    h.Hp = after > before ? 0 : (u.Alive ? (u.Hp >= u.MaxHp ? 0 : u.Hp) : 1);
                    if (r.FinalWeapon.TryGetValue(u.Def.Id, out var finalWeapon) && finalWeapon != h.WeaponId && finalWeapon != null)
                    {
                        // A bar swap: the old weapon goes back to the bag, the new one leaves it.
                        if (h.WeaponId != null) s.AddItem(h.WeaponId, 1);
                        s.AddItem(finalWeapon, -1);
                        h.WeaponId = finalWeapon;
                        h.WeaponRepairs = 0;
                    }
                    if (r.FinalBar.TryGetValue(u.Def.Id, out var bar))
                        for (int i = 0; i < bar.Count && i < h.Bar.Count; i++)
                            if (h.Bar[i] != null && h.Bar[i].StartsWith("weapon:")) h.Bar[i] = bar[i];
                    if (r.WeaponDurability.TryGetValue(u.Def.Id, out var dur))
                    {
                        if (dur < 0 && h.WeaponId != null && u.WeaponId == null) { h.WeaponId = null; h.WeaponDurability = 0; h.WeaponRepairs = 0; }
                        else if (dur >= 0) h.WeaponDurability = dur;
                    }
                    if (r.ArmorDurability.TryGetValue(u.Def.Id, out var adur))
                    {
                        if (adur < 0 && h.ArmorId != null && u.ArmorId == null) { h.ArmorId = null; h.ArmorDurability = 0; h.ArmorRepairs = 0; }
                        else if (adur >= 0 && h.ArmorId != null) h.ArmorDurability = adur;
                    }
                }

                int index = c.CampaignStages().FindIndex(x => x.Id == battle.Stage.Id);
                rep.FirstClear = s.ClearedStages.Add(battle.Stage.Id);
                var all = c.CampaignStages();
                rep.CampaignComplete = all.Count > 0 && index == all.Count - 1;
                if (index > s.HighestClearedStage)
                {
                    s.HighestClearedStage = index;
                    var list = c.CampaignStages();
                    if (index + 1 < list.Count) rep.UnlockedStage = list[index + 1].Name;
                }
                rep.BountyProgress = Bounties.RecordKills(c, s, r.Kills);
            }
            else if (rep.Escaped)
            {
                // Escaping keeps wounds (no defeat reset) and grants nothing.
                foreach (var u in battle.Units.Where(u => u.IsHero))
                {
                    var h = s.Hero(u.Def.Id);
                    if (h != null) h.Hp = u.Alive ? (u.Hp >= u.MaxHp ? 0 : u.Hp) : 1;
                }
            }
            else
            {
                foreach (var id in s.Party) { var h = s.Hero(id); if (h != null) h.Hp = 0; } // defeat = retry at full (§22.2)
            }
            rep.NewSlots = Progression.UnlockedSlots(s) - slotsBefore;
            s.LastStage = battle.Stage.Id;
            s.UpdatedUtc = DateTime.UtcNow;
            return rep;
        }

        private static BestiaryEntry Entry(SaveData s, string id)
        {
            if (!s.Bestiary.TryGetValue(id, out var e)) { e = new BestiaryEntry(); s.Bestiary[id] = e; }
            return e;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Using items at camp (potions on a hero, Tent on everyone)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Camp
    {
        public static bool UsableAtCamp(ItemDef i) => i != null && i.Type == ItemType.Consumable && i.BaseHealing > 0 && !i.Revives;

        public static bool HealsWholeParty(ItemDef i) => i != null && i.OutOfBattleOnly;

        public static Result Use(ContentDb c, SaveData s, string itemId, string heroId)
        {
            var item = c.TryItem(itemId);
            if (!UsableAtCamp(item)) return Result.Fail("Can't use that here");
            if (s.Count(itemId) < 1) return Result.Fail("None left");
            if (HealsWholeParty(item))
            {
                if (Inn.Cost(c, s) == 0) return Result.Fail("Everyone is healthy");
                foreach (var h in s.Roster) h.Hp = 0;
                s.AddItem(itemId, -1);
                return Result.Success("The party is fully rested");
            }
            var hero = s.Hero(heroId);
            if (hero == null) return Result.Fail("Pick a hero");
            int max = Progression.Preview(c, hero).MaxHp;
            if (hero.Hp <= 0 || hero.Hp >= max) return Result.Fail($"{c.Class(hero.ClassId).Name} is already healthy");
            int healed = Math.Min(max - hero.Hp, item.BaseHealing);
            hero.Hp += healed;
            if (hero.Hp >= max) hero.Hp = 0;
            s.AddItem(itemId, -1);
            return Result.Success($"{c.Class(hero.ClassId).Name} recovers {healed} HP");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Merchant (§25.1)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Shop
    {
        public static List<ItemDef> Stock(ContentDb c) =>
            c.Items.Values.Where(i => i.SoldByVendor && i.BaseCost > 0).OrderBy(i => i.Type).ThenBy(i => i.BaseCost).ThenBy(i => i.Name).ToList();

        public static int SellPrice(ItemDef i) => Math.Max(1, (int)Math.Floor(i.BaseCost * 0.5));

        public static List<ItemDef> Sellable(ContentDb c, SaveData s) =>
            s.Inventory.Where(kv => kv.Value > 0).Select(kv => c.TryItem(kv.Key)).Where(i => i != null && i.BaseCost > 0 && i.Type != ItemType.QuestItem)
                .OrderBy(i => i.Type).ThenBy(i => i.Name).ToList();

        public static int MaxBuy(SaveData s, ItemDef i) => Math.Max(0, i.MaxStack - s.Count(i.Id));

        public static int CartTotal(ContentDb c, Dictionary<string, int> cart, bool selling) =>
            cart.Sum(kv => { var i = c.TryItem(kv.Key); return i == null ? 0 : kv.Value * (selling ? SellPrice(i) : i.BaseCost); });

        public static Result Buy(ContentDb c, SaveData s, Dictionary<string, int> cart)
        {
            int total = CartTotal(c, cart, false);
            if (total <= 0) return Result.Fail("Nothing to buy");
            if (total > s.Gold) return Result.Fail("Not enough gold");
            foreach (var kv in cart)
            {
                var i = c.TryItem(kv.Key);
                if (i == null || kv.Value < 0 || kv.Value > MaxBuy(s, i)) return Result.Fail($"Can't carry more {i?.Name}");
            }
            s.Gold -= total;
            foreach (var kv in cart) s.AddItem(kv.Key, kv.Value);
            return Result.Success($"Bought for {total}g");
        }

        public static Result Sell(ContentDb c, SaveData s, Dictionary<string, int> cart)
        {
            foreach (var kv in cart)
                if (kv.Value < 0 || kv.Value > s.Count(kv.Key)) return Result.Fail("You don't have that many");
            int total = CartTotal(c, cart, true);
            if (total <= 0) return Result.Fail("Nothing to sell");
            foreach (var kv in cart) s.AddItem(kv.Key, -kv.Value);
            s.Gold += total;
            return Result.Success($"Sold for {total}g");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Blacksmith & Alchemist crafting (§24.6, §25.2–25.3)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Crafting
    {
        public static List<RecipeDef> Recipes(ContentDb c, CraftStation station) =>
            c.Recipes.Values.Where(r => r.Station == station && c.TryItem(r.Result) != null)
                .OrderBy(r => r.BaseItem != null).ThenBy(r => r.Gold).ThenBy(r => c.Item(r.Result).Name).ToList();

        public static bool HasInputs(SaveData s, RecipeDef r)
        {
            foreach (var ic in r.Inputs) if (s.Count(ic.Item) < ic.Count) return false;
            if (r.BaseItem != null && s.Count(r.BaseItem) < 1) return false;
            return true;
        }

        public static Result Craft(ContentDb c, SaveData s, RecipeDef r)
        {
            if (s.Gold < r.Gold) return Result.Fail("Not enough gold");
            if (!HasInputs(s, r)) return Result.Fail("Missing materials");
            var result = c.Item(r.Result);
            if (s.Count(result.Id) + r.ResultCount > result.MaxStack) return Result.Fail("Can't carry more");
            s.Gold -= r.Gold;
            foreach (var ic in r.Inputs) s.AddItem(ic.Item, -ic.Count);
            if (r.BaseItem != null) s.AddItem(r.BaseItem, -1);
            s.AddItem(result.Id, r.ResultCount);
            return Result.Success($"Made {result.Name}");
        }

        /// <summary>Salvage: break equipment into 50% of its forge recipe's inputs (floor, min 1).</summary>
        public static List<ItemCount> SalvageYield(ContentDb c, ItemDef item)
        {
            var recipe = c.Recipes.Values.FirstOrDefault(r => r.Result == item.Id && r.Station == CraftStation.Forge && r.BaseItem == null);
            var list = new List<ItemCount>();
            if (recipe == null) return list;
            foreach (var ic in recipe.Inputs) list.Add(new ItemCount { Item = ic.Item, Count = Math.Max(1, ic.Count / 2) });
            return list;
        }

        public static Result Salvage(ContentDb c, SaveData s, string itemId)
        {
            var item = c.TryItem(itemId);
            if (item == null || s.Count(itemId) < 1) return Result.Fail("Nothing to salvage");
            var yield = SalvageYield(c, item);
            if (yield.Count == 0) return Result.Fail("Can't be salvaged");
            s.AddItem(itemId, -1);
            foreach (var ic in yield) s.AddItem(ic.Item, ic.Count);
            return Result.Success($"Salvaged {item.Name}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Repair (§24.5, §25.2)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Repair
    {
        public static int EffectiveMax(ItemDef item, int repairs) => Math.Max(1, item.Durability - repairs);

        public static int Cost(ItemDef item, int current, int repairs)
        {
            if (item == null || item.Durability <= 0) return 0;
            int missing = Math.Max(0, EffectiveMax(item, repairs) - current);
            if (missing == 0) return 0;
            float perPoint = (float)item.BaseCost / item.Durability;
            float mult = 0.30f * (float)Math.Pow(1.6f, Math.Max(0, repairs));
            return Math.Max(1, (int)Math.Ceiling(missing * perPoint * mult));
        }

        public static bool IsUneconomical(ItemDef item, int current, int repairs) => Cost(item, current, repairs) >= item.BaseCost;

        public static Result RepairWeapon(ContentDb c, SaveData s, HeroSave h)
        {
            var item = c.TryItem(h.WeaponId);
            if (item == null || item.Durability <= 0) return Result.Fail("Nothing to repair");
            int cost = Cost(item, h.WeaponDurability, h.WeaponRepairs);
            if (cost == 0) return Result.Fail("Already in top shape");
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            s.Gold -= cost;
            h.WeaponDurability = EffectiveMax(item, h.WeaponRepairs);
            h.WeaponRepairs++;
            return Result.Success($"Repaired {item.Name}");
        }

        public static Result RepairArmor(ContentDb c, SaveData s, HeroSave h)
        {
            var item = c.TryItem(h.ArmorId);
            if (item == null || item.Durability <= 0) return Result.Fail("Nothing to repair");
            int cost = Cost(item, h.ArmorDurability, h.ArmorRepairs);
            if (cost == 0) return Result.Fail("Already in top shape");
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            s.Gold -= cost;
            h.ArmorDurability = EffectiveMax(item, h.ArmorRepairs);
            h.ArmorRepairs++;
            return Result.Success($"Repaired {item.Name}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Inn / Heal party (§25.3)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Inn
    {
        public const float GoldPerHp = 0.5f;

        public static int MissingHp(ContentDb c, SaveData s, HeroSave h)
        {
            if (h.Hp <= 0) return 0;
            var max = Progression.Preview(c, h).MaxHp;
            return Math.Max(0, max - h.Hp);
        }

        public static int Cost(ContentDb c, SaveData s)
        {
            int missing = 0;
            foreach (var h in s.Roster) missing += MissingHp(c, s, h);
            return (int)Math.Ceiling(missing * GoldPerHp);
        }

        public static Result Rest(ContentDb c, SaveData s)
        {
            int cost = Cost(c, s);
            if (cost == 0) return Result.Fail("Everyone is healthy");
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            s.Gold -= cost;
            foreach (var h in s.Roster) h.Hp = 0;
            return Result.Success("The party is fully rested");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Equip (§24.2, §25.4)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Equipment
    {
        public static string Equipped(HeroSave h, EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon: return h.WeaponId;
                case EquipmentSlot.Armor: return h.ArmorId;
                case EquipmentSlot.Relic1: return h.Relics[0];
                case EquipmentSlot.Relic2: return h.Relics[1];
                case EquipmentSlot.Relic3: return h.Relics[2];
                default: return null;
            }
        }

        public static bool Fits(ItemDef item, EquipmentSlot slot)
        {
            if (item == null || item.Type != ItemType.Equipment) return false;
            if (slot == EquipmentSlot.Relic1 || slot == EquipmentSlot.Relic2 || slot == EquipmentSlot.Relic3) return item.IsRelic;
            return item.Slot == slot;
        }

        public static List<ItemDef> Candidates(ContentDb c, SaveData s, EquipmentSlot slot) =>
            s.Inventory.Where(kv => kv.Value > 0).Select(kv => c.TryItem(kv.Key)).Where(i => Fits(i, slot)).OrderByDescending(i => i.Rarity).ThenBy(i => i.Name).ToList();

        public static Result Equip(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot, string itemId)
        {
            var item = c.TryItem(itemId);
            if (!Fits(item, slot)) return Result.Fail("That doesn't go there");
            if (s.Count(itemId) < 1) return Result.Fail("You don't own that");
            Unequip(c, s, h, slot);
            // Fresh copies first; otherwise the best of the worn ones (per-instance durability, US-246).
            var copy = TakeCopy(s, item);
            s.AddItem(itemId, -1);
            switch (slot)
            {
                case EquipmentSlot.Weapon: h.WeaponId = itemId; h.WeaponDurability = copy.Durability; h.WeaponRepairs = copy.Repairs; break;
                case EquipmentSlot.Armor: h.ArmorId = itemId; h.ArmorDurability = copy.Durability; h.ArmorRepairs = copy.Repairs; break;
                case EquipmentSlot.Relic1: h.Relics[0] = itemId; break;
                case EquipmentSlot.Relic2: h.Relics[1] = itemId; break;
                case EquipmentSlot.Relic3: h.Relics[2] = itemId; break;
            }
            return Result.Success($"Equipped {item.Name}");
        }

        private static WornGear TakeCopy(SaveData s, ItemDef item)
        {
            s.Worn.TryGetValue(item.Id, out var worn);
            int fresh = s.Count(item.Id) - (worn?.Count ?? 0);
            if (fresh > 0 || worn == null || worn.Count == 0) return new WornGear { Durability = item.Durability, Repairs = 0 };
            worn.Sort((a, b) => b.Durability.CompareTo(a.Durability));
            var best = worn[0];
            worn.RemoveAt(0);
            if (worn.Count == 0) s.Worn.Remove(item.Id);
            return best;
        }

        /// <summary>Returns the piece to the bag, remembering its wear and repairs if it isn't factory-fresh.</summary>
        public static Result Unequip(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot)
        {
            var id = Equipped(h, slot);
            if (id == null) return Result.Fail("Nothing equipped");
            var item = c.TryItem(id);
            int dur = slot == EquipmentSlot.Weapon ? h.WeaponDurability : slot == EquipmentSlot.Armor ? h.ArmorDurability : 0;
            int reps = slot == EquipmentSlot.Weapon ? h.WeaponRepairs : slot == EquipmentSlot.Armor ? h.ArmorRepairs : 0;
            s.AddItem(id, 1);
            if (item != null && item.Durability > 0 && (dur < item.Durability || reps > 0))
            {
                if (!s.Worn.TryGetValue(id, out var list)) s.Worn[id] = list = new List<WornGear>();
                list.Add(new WornGear { Durability = dur, Repairs = reps });
            }
            switch (slot)
            {
                case EquipmentSlot.Weapon: h.WeaponId = null; h.WeaponDurability = 0; h.WeaponRepairs = 0; break;
                case EquipmentSlot.Armor: h.ArmorId = null; h.ArmorDurability = 0; h.ArmorRepairs = 0; break;
                case EquipmentSlot.Relic1: h.Relics[0] = null; break;
                case EquipmentSlot.Relic2: h.Relics[1] = null; break;
                case EquipmentSlot.Relic3: h.Relics[2] = null; break;
            }
            return Result.Success("Unequipped");
        }

        /// <summary>Stat preview: the hero's effective stats if <paramref name="itemId"/> replaced the slot.</summary>
        public static (Stats now, Stats then) Compare(ContentDb c, HeroSave h, EquipmentSlot slot, string itemId)
        {
            var now = Progression.Preview(c, h).Effective;
            var copy = new HeroSave
            {
                ClassId = h.ClassId, TotalXp = h.TotalXp, WeaponId = h.WeaponId, ArmorId = h.ArmorId, Relics = new List<string>(h.Relics),
            };
            switch (slot)
            {
                case EquipmentSlot.Weapon: copy.WeaponId = itemId; break;
                case EquipmentSlot.Armor: copy.ArmorId = itemId; break;
                case EquipmentSlot.Relic1: copy.Relics[0] = itemId; break;
                case EquipmentSlot.Relic2: copy.Relics[1] = itemId; break;
                case EquipmentSlot.Relic3: copy.Relics[2] = itemId; break;
            }
            return (now, Progression.Preview(c, copy).Effective);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Abilities: learn + slot (§25.6)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Abilities
    {
        /// <summary>What a hero could learn: its class list plus the general spell catalog (hero spells only).</summary>
        public static List<AbilityDef> Learnable(ContentDb c, HeroSave h)
        {
            var def = c.Class(h.ClassId);
            var set = new List<AbilityDef>();
            foreach (var id in def.Abilities)
            {
                var a = c.TryAbility(id);
                if (a != null && IsHeroUsable(a) && !h.Known.Contains(a.Id) && !set.Contains(a)) set.Add(a);
            }
            foreach (var a in c.Abilities.Values)
                if (a.Kind == AbilityKind.Spell && IsHeroUsable(a) && !h.Known.Contains(a.Id) && !set.Contains(a)) set.Add(a);
            return set.OrderBy(a => Price(a)).ThenBy(a => a.Name).ToList();
        }

        public static bool IsHeroUsable(AbilityDef a) => a.Kind != AbilityKind.Passive &&
            (a.Kind == AbilityKind.Spell || a.Kind == AbilityKind.Skill) && !a.Id.StartsWith("charge_") && a.Id != "enrage";

        public static int Price(AbilityDef a) => a.Kind == AbilityKind.Skill ? 300 : 120 + 80 * a.Cost;

        public static Result Learn(ContentDb c, SaveData s, HeroSave h, string abilityId)
        {
            var a = c.TryAbility(abilityId);
            if (a == null || !Learnable(c, h).Contains(a)) return Result.Fail("Can't learn that");
            int price = Price(a);
            if (s.Gold < price) return Result.Fail("Not enough gold");
            s.Gold -= price;
            h.Known.Add(a.Id);
            return Result.Success($"Learned {a.Name}");
        }

        public static Result Assign(ContentDb c, SaveData s, HeroSave h, int slot, string entry)
        {
            if (slot < 0 || slot >= Progression.MaxBarSlots) return Result.Fail("No such slot");
            if (slot >= Progression.UnlockedSlots(s)) return Result.Fail("Slot locked");
            if (entry != null)
            {
                if (entry.StartsWith("item:"))
                {
                    var item = c.TryItem(entry.Substring(5));
                    if (item == null || item.Type != ItemType.Consumable) return Result.Fail("Not usable in battle");
                }
                else if (entry.StartsWith("weapon:"))
                {
                    var w = c.TryItem(entry.Substring(7));
                    if (w == null || w.Slot != EquipmentSlot.Weapon) return Result.Fail("Not a weapon");
                    if (s.Count(w.Id) < 1) return Result.Fail("You need a spare one in your bag");
                }
                else if (!h.Known.Contains(entry)) return Result.Fail("Not learned");
                int dup = h.Bar.IndexOf(entry);
                if (dup >= 0 && dup != slot) h.Bar[dup] = null;
            }
            h.Bar[slot] = entry;
            return Result.Success();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Party & Summon Circle (§23.4, §25.5, §25.10)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Party
    {
        public static Result Toggle(SaveData s, string classId)
        {
            if (s.Hero(classId) == null) return Result.Fail("Not in roster");
            if (s.Party.Contains(classId))
            {
                if (s.Party.Count <= 1) return Result.Fail("Someone has to go");
                s.Party.Remove(classId);
                return Result.Success("Moved to reserve");
            }
            if (s.Party.Count >= Progression.MaxPartySize) return Result.Fail("Party is full (4)");
            s.Party.Add(classId);
            return Result.Success("Joined the party");
        }
    }

    public static class Summon
    {
        public static int Price(ContentDb c, SaveData s) => c.Summon.BasePrice + c.Summon.PriceStep * s.Recruits;

        public static List<ClassDef> Pool(ContentDb c, SaveData s) =>
            c.Summon.Pool.Select(c.TryClass).Where(d => d != null).ToList();

        public static Result Recruit(ContentDb c, SaveData s, string classId)
        {
            if (!c.Summon.Pool.Contains(classId)) return Result.Fail("Not summonable");
            if (s.Hero(classId) != null) return Result.Fail("Already in your roster");
            int price = Price(c, s);
            if (s.Gold < price) return Result.Fail("Not enough gold");
            s.Gold -= price;
            s.Recruits++;
            var h = Progression.NewHero(c, classId);
            int avg = s.Roster.Count == 0 ? 0 : (int)s.Roster.Average(x => x.TotalXp);
            h.TotalXp = avg; // recruits arrive at the roster's average experience
            s.Roster.Add(h);
            if (s.Party.Count < Progression.MaxPartySize) s.Party.Add(h.ClassId);
            return Result.Success($"{c.Class(classId).Name} joins you!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Bounty board (§22.4)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Bounties
    {
        public static List<BountyDef> Posted(ContentDb c, SaveData s) =>
            c.Bounties.Values.Where(b => !s.Bounty.Claimed.Contains(b.Id)).OrderBy(b => b.RewardGold).ToList();

        public static BountyDef Active(ContentDb c, SaveData s) => s.Bounty.ActiveId != null && c.Bounties.TryGetValue(s.Bounty.ActiveId, out var b) ? b : null;

        public static bool IsComplete(ContentDb c, SaveData s) { var b = Active(c, s); return b != null && s.Bounty.Progress >= b.Kills; }

        public static Result Accept(ContentDb c, SaveData s, string id)
        {
            if (!c.Bounties.ContainsKey(id) || s.Bounty.Claimed.Contains(id)) return Result.Fail("Not available");
            if (s.Bounty.ActiveId != null) return Result.Fail("Finish or abandon your current bounty first");
            s.Bounty.ActiveId = id;
            s.Bounty.Progress = 0;
            return Result.Success("Bounty accepted");
        }

        public static Result Abandon(SaveData s)
        {
            if (s.Bounty.ActiveId == null) return Result.Fail("No active bounty");
            s.Bounty.ActiveId = null;
            s.Bounty.Progress = 0;
            return Result.Success("Bounty abandoned");
        }

        public static string RecordKills(ContentDb c, SaveData s, Dictionary<string, int> kills)
        {
            var b = Active(c, s);
            if (b == null) return null;
            if (kills.TryGetValue(b.TargetClass, out var n) && n > 0)
            {
                s.Bounty.Progress = Math.Min(b.Kills, s.Bounty.Progress + n);
                return $"{b.Name}: {s.Bounty.Progress}/{b.Kills}";
            }
            return null;
        }

        public static Result Claim(ContentDb c, SaveData s)
        {
            var b = Active(c, s);
            if (b == null) return Result.Fail("No active bounty");
            if (s.Bounty.Progress < b.Kills) return Result.Fail("Not finished yet");
            s.Gold += b.RewardGold;
            if (b.RewardItem != null) s.AddItem(b.RewardItem, 1);
            s.Bounty.Claimed.Add(b.Id);
            s.Bounty.ActiveId = null;
            s.Bounty.Progress = 0;
            return Result.Success($"Claimed {b.RewardGold}g");
        }
    }
}
