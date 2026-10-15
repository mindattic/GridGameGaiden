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
        /// <summary>
        /// The starting party (owner 2026-10-11: "add a fourth and fifth hero … a 5 player team seems about right for max"):
        /// Tank, Striker, Healer, Mage, Support — one of each role.
        /// </summary>
        public static readonly string[] StartingRoster = { "paladin", "barbarian", "cleric", "redmage", "engineer" };
        public const int StartingGold = 200;
        public const int MaxPartySize = 5;
        public const int MaxBarSlots = 5;
        /// <summary>
        /// Slot unlocks (§4.7). Amended 2026-10-11 (owner: "give each character 2 stacks of full life potions and unlock
        /// the slots to put them in"): every slot is open from the start, so the thresholds no longer gate anything.
        /// </summary>
        public static readonly int[] UnlockThresholds = { };
        public const int StartingSlots = MaxBarSlots;

        public static SaveData NewGame(ContentDb c, string name) => NewGame(c, name, StartingRoster);

        /// <summary>A custom party (e.g. from the character-select screen) in place of the fixed starting five.</summary>
        public static SaveData NewGame(ContentDb c, string name, IReadOnlyList<string> party)
        {
            var s = new SaveData { Name = string.IsNullOrWhiteSpace(name) ? "Lightbearer" : name.Trim(), Gold = StartingGold };
            foreach (var id in party)
            {
                if (c.TryClass(id) == null) continue;
                var h = NewHero(c, id);
                s.Roster.Add(h);
                s.Party.Add(id);
            }
            // Every item slot on a starting bar is a full stack (two X-Potion stacks of 5 per hero).
            foreach (var h in s.Roster)
                foreach (var e in h.Bar)
                    if (e != null && e.StartsWith("item:")) s.AddItem(e.Substring(5), Math.Max(1, c.TryItem(e.Substring(5))?.MaxStack ?? 3));
            return s;
        }

        public static HeroSave NewHero(ContentDb c, string classId)
        {
            var def = c.Class(classId);
            var h = new HeroSave { ClassId = def.Id };
            int i = 0;
            foreach (var entry in def.DefaultLoadout)
            {
                // Abilities past the bar are still known (they can be slotted later); items may repeat (two stacks).
                if (!entry.StartsWith("item:") && !h.Known.Contains(entry)) h.Known.Add(entry);
                if (i < MaxBarSlots) h.Bar[i++] = entry;
            }
            return h;
        }

        public static int Level(HeroSave h) => Formulas.LevelFromTotalXp(h.TotalXp).level;

        public static int UnlockedSlots(SaveData s)
        {
            int n = StartingSlots;
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
        /// <summary>A voluntary pause-menu Retreat (FFIV-style partial loss, §22.2) — distinct from Defeat.</summary>
        public bool Retreated;
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
        // Expedition (§22.5)
        public string ExpeditionName;
        public int ExpeditionStep;      // battles won so far on this expedition
        public int ExpeditionLength;
        public bool ExpeditionComplete;
        public bool ExpeditionFailed;
        public int ExpeditionBonus;
        public int CarriedOrbs;
        public string NextStageId;
        // Guests (§25.11)
        public List<string> GuestsJoined = new List<string>();
        public List<string> GuestsLost = new List<string>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Expeditions (§22.5): a theme's battles in order; wounds and mana carry; no inn until it ends
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Expeditions
    {
        /// <summary>Share of the expedition's coins paid as a completion bonus.</summary>
        public const float CompletionBonus = 0.5f;

        public static ThemeDef Theme(ContentDb c, string themeId) => c.Campaign.Themes.Find(t => t.Id == themeId);

        /// <summary>The inn is open between expeditions — or on the road once the caravan has a Medic Wagon.</summary>
        public static bool InnOpen(SaveData s) => s.Expedition == null || CaravanUpgrades.Level(s, "medic_wagon") > 0;

        /// <summary>A battle can start if it is the next one of the active expedition, or the first battle of a theme when none is active.</summary>
        public static bool CanLaunch(ContentDb c, SaveData s, string stageId, out string why)
        {
            why = null;
            var theme = c.ThemeOf(stageId);
            if (theme == null) return true; // test/standalone stages
            int idx = theme.Stages.IndexOf(stageId);
            if (s.Expedition != null)
            {
                if (s.Expedition.ThemeId != theme.Id) { why = $"Finish (or abandon) the {Theme(c, s.Expedition.ThemeId)?.Name} expedition first"; return false; }
                if (idx != s.Expedition.Next) { why = $"Next up: battle {s.Expedition.Next + 1} of {theme.Stages.Count}"; return false; }
                return true;
            }
            if (idx != 0) { why = "Expeditions start at battle 1"; return false; }
            return true;
        }

        /// <summary>Called when a battle is launched: starts an expedition on its theme's first battle.</summary>
        public static void Begin(ContentDb c, SaveData s, string stageId)
        {
            var theme = c.ThemeOf(stageId);
            if (theme == null || s.Expedition != null) return;
            if (theme.Stages.IndexOf(stageId) == 0) s.Expedition = new ExpeditionSave { ThemeId = theme.Id };
        }

        public static int StartOrbs(ContentDb c, SaveData s, string stageId)
        {
            var theme = c.ThemeOf(stageId);
            return s.Expedition != null && theme != null && s.Expedition.ThemeId == theme.Id ? s.Expedition.CarriedOrbs : 0;
        }

        public static Result Abandon(SaveData s)
        {
            if (s.Expedition == null) return Result.Fail("No expedition under way");
            s.Expedition = null;
            return Result.Success("Expedition abandoned — the caravan turns back");
        }

        public static void AfterBattle(ContentDb c, SaveData s, BattleReport rep, BattleState battle)
        {
            var theme = c.ThemeOf(battle.Stage.Id);
            if (theme == null || s.Expedition == null || s.Expedition.ThemeId != theme.Id) return;
            rep.ExpeditionName = theme.Name;
            rep.ExpeditionLength = theme.Stages.Count;
            if (!rep.Victory)
            {
                rep.ExpeditionFailed = true;
                rep.ExpeditionStep = s.Expedition.Next;
                s.Expedition = null;
                return;
            }
            s.Expedition.Next++;
            s.Expedition.Coins += rep.Gold;
            s.Expedition.CarriedOrbs = battle.Bank.Count;
            rep.ExpeditionStep = s.Expedition.Next;
            rep.CarriedOrbs = battle.Bank.Count;
            if (s.Expedition.Next >= theme.Stages.Count)
            {
                rep.ExpeditionComplete = true;
                rep.ExpeditionBonus = (int)Math.Round(s.Expedition.Coins * (CompletionBonus + CaravanUpgrades.Value(c, s, "strongbox")));
                s.Gold += rep.ExpeditionBonus;
                rep.Gold += rep.ExpeditionBonus;
                foreach (var id in s.Party) { var h = s.Hero(id); if (h != null) h.Hp = 0; } // the caravan makes camp: everyone rests
                s.Expedition = null;
                rep.CarriedOrbs = 0;
            }
            else rep.NextStageId = theme.Stages[s.Expedition.Next];
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Caravan upgrades (§25.12): the long-run gold sink that changes how expeditions play
    // ─────────────────────────────────────────────────────────────────────────────

    public static class CaravanUpgrades
    {
        public static int Level(SaveData s, string id) => s.Caravan.TryGetValue(id, out var n) ? n : 0;

        /// <summary>The current level's value, or <paramref name="none"/> when not bought.</summary>
        public static float Value(ContentDb c, SaveData s, string id, float none = 0f)
        {
            int lv = Level(s, id);
            if (lv <= 0 || !c.Caravan.TryGetValue(id, out var def) || def.Levels.Count == 0) return none;
            return def.Levels[Math.Min(lv, def.Levels.Count) - 1].Value;
        }

        public static CaravanLevel NextLevel(ContentDb c, SaveData s, string id) =>
            c.Caravan.TryGetValue(id, out var def) && Level(s, id) < def.Levels.Count ? def.Levels[Level(s, id)] : null;

        public static Result Buy(ContentDb c, SaveData s, string id)
        {
            if (!c.Caravan.TryGetValue(id, out var def)) return Result.Fail("Unknown upgrade");
            var next = NextLevel(c, s, id);
            if (next == null) return Result.Fail("Fully upgraded");
            if (s.Gold < next.Cost) return Result.Fail("Not enough gold");
            s.Gold -= next.Cost;
            s.Caravan[id] = Level(s, id) + 1;
            return Result.Success($"{def.Name}: {next.Text}");
        }
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
                RecommendedLevel = stage.LevelOverride ?? (index >= 0 ? Progression.RecommendedLevel(index) : 1),
                NewGamePlus = s.NewGamePlus,
                StartOrbs = Expeditions.StartOrbs(c, s, stageId) + (int)CaravanUpgrades.Value(c, s, "mana_casks"),
            };
            foreach (var h in s.Roster) setup.ExcludedGuests.Add(h.ClassId);
            foreach (var g in s.LostGuests) setup.ExcludedGuests.Add(g);
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
            var rep = new BattleReport
            {
                Victory = battle.Phase == BattlePhase.Victory,
                Escaped = battle.Phase == BattlePhase.Escaped,
                Retreated = battle.Phase == BattlePhase.Retreated,
                StageId = battle.Stage.Id,
            };
            int slotsBefore = Progression.UnlockedSlots(s);
            Bargains.Rotate(s);

            foreach (var kv in r.ItemsConsumed) s.AddItem(kv.Key, -kv.Value);
            foreach (var id in r.Seen) Entry(s, id).Seen = true;
            foreach (var id in r.Scanned) Entry(s, id).Scanned = true;

            if (rep.Victory)
            {
                rep.Gold = r.Coins;
                s.Gold += r.Coins;
                s.LifetimeCoins += r.Coins;
                GrantItems(c, s, rep, r.Items);
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
                    r.BrokenWeapons.TryGetValue(u.Def.Id, out var shatteredWeapons);
                    if (r.FinalWeapon.TryGetValue(u.Def.Id, out var finalWeapon) && finalWeapon != h.WeaponId && finalWeapon != null)
                    {
                        // A bar swap: the old weapon goes back to the bag, the new one leaves it — unless the
                        // old one shattered first, in which case it already became a Broken fragment below (GG-T41).
                        if (h.WeaponId != null && (shatteredWeapons == null || !shatteredWeapons.Contains(h.WeaponId))) s.AddItem(h.WeaponId, 1);
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
                        else if (dur >= 0 && h.WeaponId != null) h.WeaponDurability = dur;
                    }
                    if (r.ArmorDurability.TryGetValue(u.Def.Id, out var adur))
                    {
                        if (adur < 0 && h.ArmorId != null && u.ArmorId == null) { h.ArmorId = null; h.ArmorDurability = 0; h.ArmorRepairs = 0; }
                        else if (adur >= 0 && h.ArmorId != null) h.ArmorDurability = adur;
                    }
                }
                // Shattered weapons and broken armor become salvage-only Broken fragments instead of
                // vanishing outright (GG-T41) — can't be equipped or repaired, only scrapped for materials.
                foreach (var list in r.BrokenWeapons.Values) foreach (var id in list) s.AddBroken(id, 1);
                foreach (var list in r.BrokenArmors.Values) foreach (var id in list) s.AddBroken(id, 1);

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
            else if (rep.Retreated)
            {
                // FFIV-style partial loss (owner direction 2026-10-11): a voluntary retreat keeps a
                // fraction of this fight's uncommitted gold/loot plus hard-won XP — better than a wipe,
                // but not free. Still resets to full HP like a Defeat (you fall back and recover).
                foreach (var id in s.Party) { var h = s.Hero(id); if (h != null) h.Hp = 0; }
                int keepCoins = (int)Math.Floor(r.Coins * BattleConfig.RetreatKeepFraction);
                rep.Gold = keepCoins;
                s.Gold += keepCoins;
                s.LifetimeCoins += keepCoins;
                var kept = new Dictionary<string, int>();
                foreach (var kv in r.Items)
                {
                    int n = (int)Math.Floor(kv.Value * BattleConfig.RetreatKeepFraction);
                    if (n > 0) kept[kv.Key] = n;
                }
                GrantItems(c, s, rep, kept);
                GrantHardWonXp(c, s, battle, r, rep);
            }
            else
            {
                foreach (var id in s.Party) { var h = s.Hero(id); if (h != null) h.Hp = 0; } // defeat = retry at full (§22.2)
                GrantHardWonXp(c, s, battle, r, rep);
            }
            // Guests: alive at a victory → they join the caravan; fallen at any point → lost forever.
            foreach (var g in battle.Units.Where(u => u.IsGuest))
            {
                if (!g.Alive) { s.LostGuests.Add(g.Def.Id); rep.GuestsLost.Add(g.Def.Id); continue; }
                if (!rep.Victory || s.Hero(g.Def.Id) != null) continue;
                var h = Progression.NewHero(c, g.Def.Id);
                h.TotalXp = Formulas.TotalXpForLevel(g.Level);
                h.Hp = g.Hp >= g.MaxHp ? 0 : g.Hp;
                s.Roster.Add(h);
                if (s.Party.Count < Progression.MaxPartySize) s.Party.Add(h.ClassId);
                rep.GuestsJoined.Add(g.Def.Id);
            }
            Expeditions.AfterBattle(c, s, rep, battle);
            rep.NewSlots = Progression.UnlockedSlots(s) - slotsBefore;
            s.LastStage = battle.Stage.Id;
            s.UpdatedUtc = DateTime.UtcNow;
            return rep;
        }

        /// <summary>Grants items into the save, respecting stack caps (overflow auto-sells, US-245). Shared by
        /// Victory's full haul and a voluntary Retreat's partial one.</summary>
        private static void GrantItems(ContentDb c, SaveData s, BattleReport rep, Dictionary<string, int> items)
        {
            foreach (var kv in items)
            {
                var def = c.TryItem(kv.Key);
                bool firstEver = s.Count(kv.Key) == 0;
                int room = def == null ? kv.Value : Math.Max(0, def.MaxStack - s.Count(kv.Key));
                int keep = Math.Min(room, kv.Value), extra = kv.Value - keep;
                if (keep > 0)
                {
                    s.AddItem(kv.Key, keep);
                    rep.Items[kv.Key] = keep;
                    if (firstEver) RecipeDiscovery.OnItemAcquired(c, s, kv.Key);
                }
                if (extra > 0 && def != null) { int g = extra * Shop.SellPrice(def); s.Gold += g; rep.Gold += g; rep.AutoSold += extra; }
            }
        }

        /// <summary>Hard-won experience (amended 2026-10-11): the XP of every foe felled is kept on a non-victory
        /// end, so each retry of a long gauntlet leaves the party stronger. Shared by Defeat and Retreat.</summary>
        private static void GrantHardWonXp(ContentDb c, SaveData s, BattleState battle, BattleResults r, BattleReport rep)
        {
            var heroes = battle.Units.Where(u => u.IsHero && !u.IsGuest).ToList();
            rep.XpEach = heroes.Count > 0 ? r.Xp : 0;
            foreach (var u in heroes)
            {
                var h = s.Hero(u.Def.Id);
                if (h == null || rep.XpEach <= 0) continue;
                int before = Progression.Level(h);
                h.TotalXp += rep.XpEach;
                int after = Progression.Level(h);
                if (after > before) rep.LevelUps.Add((h.ClassId, before, after));
            }
            foreach (var kv in r.Kills) Entry(s, kv.Key).Defeated += kv.Value;
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
            string heroName = c.TryClass(hero.ClassId)?.Name ?? hero.ClassId;
            int max = Progression.Preview(c, hero).MaxHp;
            if (hero.Hp <= 0 || hero.Hp >= max) return Result.Fail($"{heroName} is already healthy");
            int healed = Math.Min(max - hero.Hp, item.BaseHealing);
            hero.Hp += healed;
            if (hero.Hp >= max) hero.Hp = 0;
            s.AddItem(itemId, -1);
            return Result.Success($"{heroName} recovers {healed} HP");
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

    /// <summary>One rolled offer on the Merchant's "Used / Bargain" shelf (§25.1, US-246).</summary>
    public sealed class BargainOffer
    {
        public ItemDef Item;
        /// <summary>Discounted price (35-60% of <see cref="ItemDef.BaseCost"/>).</summary>
        public int Price;
        /// <summary>Starting durability the piece carries if bought (10-40% of <see cref="ItemDef.Durability"/>).</summary>
        public int Durability;
    }

    /// <summary>
    /// The Merchant's rotating "Used / Bargain" shelf (bible §25.1, US-246): cheap weapons and armor
    /// sold well below new price, but already worn down — a deliberate early-game gamble between
    /// cheap power now and a real chance of shattering mid-combat before it can be repaired.
    /// </summary>
    public static class Bargains
    {
        public const int ShelfSize = 4;
        public const float MinPriceFraction = 0.35f, MaxPriceFraction = 0.60f;
        public const float MinDurabilityFraction = 0.10f, MaxDurabilityFraction = 0.40f;

        private static bool Eligible(ItemDef i) => i.Type == ItemType.Equipment && i.Durability > 0 &&
            (i.Slot == EquipmentSlot.Weapon || i.Slot == EquipmentSlot.Armor);

        /// <summary>
        /// This rotation's stock. Deterministic from <see cref="SaveData.BargainRotation"/> alone, so it
        /// stays put across repeated calls (reopening the Merchant doesn't reroll it) and replays
        /// identically from the same save, but turns over on <see cref="Rotate"/>.
        /// </summary>
        public static List<BargainOffer> Shelf(ContentDb c, SaveData s)
        {
            var pool = c.Items.Values.Where(Eligible).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
            var offers = new List<BargainOffer>();
            if (pool.Count == 0) return offers;
            var rng = new Random(unchecked((int)((uint)s.BargainRotation * 747796405u + 2891336453u)));
            var remaining = Enumerable.Range(0, pool.Count).ToList();
            int take = Math.Min(ShelfSize, pool.Count);
            for (int i = 0; i < take; i++)
            {
                int pick = rng.Next(remaining.Count);
                var item = pool[remaining[pick]];
                remaining.RemoveAt(pick);
                float priceFrac = MinPriceFraction + (float)rng.NextDouble() * (MaxPriceFraction - MinPriceFraction);
                float durFrac = MinDurabilityFraction + (float)rng.NextDouble() * (MaxDurabilityFraction - MinDurabilityFraction);
                offers.Add(new BargainOffer
                {
                    Item = item,
                    Price = Math.Max(1, (int)Math.Round(item.BaseCost * priceFrac)),
                    Durability = Math.Max(1, (int)Math.Round(item.Durability * durFrac)),
                });
            }
            return offers;
        }

        public static Result Buy(ContentDb c, SaveData s, string itemId)
        {
            var offer = Shelf(c, s).FirstOrDefault(o => o.Item.Id == itemId);
            if (offer == null) return Result.Fail("Not on the shelf");
            if (s.Count(itemId) >= offer.Item.MaxStack) return Result.Fail("Can't carry more");
            if (s.Gold < offer.Price) return Result.Fail("Not enough gold");
            s.Gold -= offer.Price;
            s.AddItem(itemId, 1);
            // Marks the copy as worn on arrival so equipping it pulls this rolled durability, not factory-fresh.
            if (!s.Worn.TryGetValue(itemId, out var list)) s.Worn[itemId] = list = new List<WornGear>();
            list.Add(new WornGear { Durability = offer.Durability, Repairs = 0 });
            return Result.Success($"Bought worn {offer.Item.Name} for {offer.Price}g");
        }

        /// <summary>Turns the shelf over. Called once per finished battle, win or lose (<see cref="BattleFactory.Commit"/>).</summary>
        public static void Rotate(SaveData s) => s.BargainRotation++;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Station progression (§24.6.1, GG-T35): craft/brew/repair/salvage levels up the
    // station that did the work, unlocking the next tier of its own recipe ladder.
    // Deliberately independent of §25.12 caravan upgrades (gold-bought stat bonuses,
    // not recipe gates) so the two never double-gate the same thing.
    // ─────────────────────────────────────────────────────────────────────────────

    public enum StationKind { Blacksmith, Alchemist }

    public static class StationProgress
    {
        public static readonly string[] LevelNames = { "Apprentice", "Journeyman", "Master" };
        /// <summary>Total XP needed to *reach* each level (index 0 = Apprentice, free).</summary>
        private static readonly int[] Thresholds = { 0, 150, 450 };

        public static string Id(StationKind k) => k == StationKind.Blacksmith ? "blacksmith" : "alchemist";

        public static int Xp(SaveData s, StationKind k) => s.StationXp.TryGetValue(Id(k), out var n) ? n : 0;

        public static int Level(SaveData s, StationKind k)
        {
            int xp = Xp(s, k);
            int lvl = 1;
            for (int i = 1; i < Thresholds.Length; i++) if (xp >= Thresholds[i]) lvl = i + 1;
            return lvl;
        }

        public static string LevelName(SaveData s, StationKind k) => LevelNames[Level(s, k) - 1];
        public static string TierName(int tier) => LevelNames[Math.Max(1, Math.Min(tier, LevelNames.Length)) - 1];

        public static void Gain(SaveData s, StationKind k, int amount)
        {
            if (amount <= 0) return;
            s.StationXp[Id(k)] = Xp(s, k) + amount;
        }

        public static bool IsUnlocked(SaveData s, StationKind k, int tier) => Level(s, k) >= tier;

        /// <summary>"Journeyman — 40/450 XP to Master", or "Master — every tier unlocked" when maxed.</summary>
        public static string HeaderText(SaveData s, StationKind k)
        {
            int lvl = Level(s, k);
            if (lvl >= LevelNames.Length) return $"{LevelNames[lvl - 1]} — every recipe tier unlocked";
            return $"{LevelNames[lvl - 1]} — {Xp(s, k)}/{Thresholds[lvl]} XP to {LevelNames[lvl]}";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Blacksmith & Alchemist crafting (§24.6, §25.2–25.3)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Crafting
    {
        public const int CraftXp = 15;
        public const int SalvageXp = 8;

        public static StationKind StationFor(CraftStation station) => station == CraftStation.Forge ? StationKind.Blacksmith : StationKind.Alchemist;

        public static List<RecipeDef> Recipes(ContentDb c, CraftStation station) =>
            c.Recipes.Values.Where(r => r.Station == station && c.TryItem(r.Result) != null)
                .OrderBy(r => r.BaseItem != null).ThenBy(r => r.Gold).ThenBy(r => c.Item(r.Result).Name).ToList();

        public static bool HasInputs(SaveData s, RecipeDef r)
        {
            foreach (var ic in r.Inputs) if (s.Count(ic.Item) < ic.Count) return false;
            if (r.BaseItem != null && s.Count(r.BaseItem) < 1) return false;
            return true;
        }

        public static bool IsUnlocked(SaveData s, RecipeDef r) => StationProgress.IsUnlocked(s, StationFor(r.Station), r.Tier);

        public static Result Craft(ContentDb c, SaveData s, RecipeDef r)
        {
            if (!IsUnlocked(s, r)) return Result.Fail($"Requires {StationProgress.TierName(r.Tier)}");
            if (s.Gold < r.Gold) return Result.Fail("Not enough gold");
            if (!HasInputs(s, r)) return Result.Fail("Missing materials");
            var result = c.Item(r.Result);
            if (s.Count(result.Id) + r.ResultCount > result.MaxStack) return Result.Fail("Can't carry more");
            s.Gold -= r.Gold;
            foreach (var ic in r.Inputs) s.AddItem(ic.Item, -ic.Count);
            if (r.BaseItem != null) s.AddItem(r.BaseItem, -1);
            s.AddItem(result.Id, r.ResultCount);
            StationProgress.Gain(s, StationFor(r.Station), CraftXp);
            return Result.Success($"Made {result.Name}");
        }

        /// <summary>Pieces without a base Forge recipe (upgrades, enchants, relics) fall back to a flat
        /// materials-by-type-and-rarity table, so every equipment piece is salvageable (GG-T42).</summary>
        private static readonly Dictionary<(EquipmentSlot Slot, ItemRarity Rarity), (string Item, int Count)[]> FallbackSalvage =
            new Dictionary<(EquipmentSlot, ItemRarity), (string, int)[]>
            {
                [(EquipmentSlot.Weapon, ItemRarity.Junk)] = new[] { ("mat_broken_blade", 1) },
                [(EquipmentSlot.Weapon, ItemRarity.Common)] = new[] { ("mat_iron_ore", 2), ("mat_wood_plank", 1) },
                [(EquipmentSlot.Weapon, ItemRarity.Uncommon)] = new[] { ("mat_iron_ore", 3), ("mat_arcane_dust", 1) },
                [(EquipmentSlot.Weapon, ItemRarity.Rare)] = new[] { ("mat_iron_ore", 4), ("mat_arcane_dust", 2) },
                [(EquipmentSlot.Weapon, ItemRarity.Epic)] = new[] { ("mat_demon_shard", 2), ("mat_arcane_dust", 2) },
                [(EquipmentSlot.Weapon, ItemRarity.Legendary)] = new[] { ("mat_dragon_scale", 1), ("mat_demon_shard", 2) },

                [(EquipmentSlot.Armor, ItemRarity.Junk)] = new[] { ("mat_tattered_cloth", 1) },
                [(EquipmentSlot.Armor, ItemRarity.Common)] = new[] { ("mat_leather", 2), ("mat_cloth", 1) },
                [(EquipmentSlot.Armor, ItemRarity.Uncommon)] = new[] { ("mat_leather", 3), ("mat_arcane_dust", 1) },
                [(EquipmentSlot.Armor, ItemRarity.Rare)] = new[] { ("mat_troll_hide", 2), ("mat_leather", 2) },
                [(EquipmentSlot.Armor, ItemRarity.Epic)] = new[] { ("mat_demon_shard", 2), ("mat_troll_hide", 2) },
                [(EquipmentSlot.Armor, ItemRarity.Legendary)] = new[] { ("mat_dragon_scale", 1), ("mat_demon_shard", 1) },

                [(EquipmentSlot.Relic1, ItemRarity.Junk)] = new[] { ("mat_cracked_fang", 1) },
                [(EquipmentSlot.Relic1, ItemRarity.Common)] = new[] { ("mat_iron_ore", 1), ("mat_undead_bone", 1) },
                [(EquipmentSlot.Relic1, ItemRarity.Uncommon)] = new[] { ("mat_arcane_dust", 2) },
                [(EquipmentSlot.Relic1, ItemRarity.Rare)] = new[] { ("mat_naga_scale", 1), ("mat_arcane_dust", 1) },
                [(EquipmentSlot.Relic1, ItemRarity.Epic)] = new[] { ("mat_ghost_ectoplasm", 2) },
                [(EquipmentSlot.Relic1, ItemRarity.Legendary)] = new[] { ("mat_dragon_scale", 1) },
            };

        private static List<ItemCount> FullSalvageYield(ContentDb c, ItemDef item)
        {
            var recipe = c.Recipes.Values.FirstOrDefault(r => r.Result == item.Id && r.Station == CraftStation.Forge && r.BaseItem == null);
            if (recipe != null)
                return recipe.Inputs.Select(ic => new ItemCount { Item = ic.Item, Count = Math.Max(1, ic.Count / 2) }).ToList();
            var bucket = item.Slot == EquipmentSlot.Relic2 || item.Slot == EquipmentSlot.Relic3 ? EquipmentSlot.Relic1 : item.Slot;
            if (!FallbackSalvage.TryGetValue((bucket, item.Rarity), out var fallback)) return new List<ItemCount>();
            return fallback.Select(f => new ItemCount { Item = f.Item, Count = f.Count }).ToList();
        }

        /// <summary>Salvage yield for a piece at <paramref name="durability"/> out of its max: a near-dead or
        /// Broken item returns proportionally less (floor), but never less than 1 of the cheapest input (GG-T42).</summary>
        public static List<ItemCount> SalvageYield(ContentDb c, ItemDef item, int durability)
        {
            var full = FullSalvageYield(c, item);
            if (full.Count == 0 || item.Durability <= 0) return full;
            float frac = Math.Max(0f, Math.Min(1f, (float)durability / item.Durability));
            var scaled = full.Select(ic => new ItemCount { Item = ic.Item, Count = (int)Math.Floor(ic.Count * frac) })
                .Where(ic => ic.Count > 0).ToList();
            if (scaled.Count == 0)
            {
                var cheapest = full.OrderBy(ic => c.TryItem(ic.Item)?.BaseCost ?? int.MaxValue).First();
                scaled.Add(new ItemCount { Item = cheapest.Item, Count = 1 });
            }
            return scaled;
        }

        /// <summary>Preview of what salvaging the bag's worst (most-worn) copy of this item would yield, without consuming it.</summary>
        public static List<ItemCount> SalvagePreview(ContentDb c, SaveData s, string itemId)
        {
            var item = c.TryItem(itemId);
            return item == null ? new List<ItemCount>() : SalvageYield(c, item, PeekWorstDurability(s, item));
        }

        private static int PeekWorstDurability(SaveData s, ItemDef item)
        {
            if (item.Durability <= 0) return item.Durability;
            return s.Worn.TryGetValue(item.Id, out var worn) && worn.Count > 0 ? worn.Min(w => w.Durability) : item.Durability;
        }

        /// <summary>Removes and returns the durability of the bag's single worst tracked copy, or the full
        /// durability if every copy is factory-fresh (so salvaging always eats the junkiest piece first).</summary>
        private static int TakeWorstDurability(SaveData s, ItemDef item)
        {
            if (item.Durability <= 0) return item.Durability;
            if (!s.Worn.TryGetValue(item.Id, out var worn) || worn.Count == 0) return item.Durability;
            int idx = 0;
            for (int i = 1; i < worn.Count; i++) if (worn[i].Durability < worn[idx].Durability) idx = i;
            int dur = worn[idx].Durability;
            worn.RemoveAt(idx);
            if (worn.Count == 0) s.Worn.Remove(item.Id);
            return dur;
        }

        /// <summary>Salvages one bag copy of <paramref name="itemId"/> — the worst (most-worn) tracked copy first.</summary>
        public static Result Salvage(ContentDb c, SaveData s, string itemId)
        {
            var item = c.TryItem(itemId);
            if (item == null || s.Count(itemId) < 1) return Result.Fail("Nothing to salvage");
            int durability = TakeWorstDurability(s, item);
            var yield = SalvageYield(c, item, durability);
            if (yield.Count == 0) return Result.Fail("Can't be salvaged");
            s.AddItem(itemId, -1);
            foreach (var ic in yield) s.AddItem(ic.Item, ic.Count);
            StationProgress.Gain(s, StationKind.Blacksmith, SalvageXp);
            return Result.Success($"Salvaged {item.Name}");
        }

        /// <summary>Salvages a hero's currently equipped weapon/armor directly (the "Salvage instead" offer on
        /// an uneconomical repair, §25.2) — the piece is destroyed, not returned to the bag (GG-T42).</summary>
        public static Result SalvageEquipped(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot)
        {
            var itemId = Equipment.Equipped(h, slot);
            var item = itemId != null ? c.TryItem(itemId) : null;
            if (item == null) return Result.Fail("Nothing equipped");
            int durability = slot == EquipmentSlot.Weapon ? h.WeaponDurability : slot == EquipmentSlot.Armor ? h.ArmorDurability : 0;
            var yield = SalvageYield(c, item, durability);
            if (yield.Count == 0) return Result.Fail("Can't be salvaged");
            switch (slot)
            {
                case EquipmentSlot.Weapon: h.WeaponId = null; h.WeaponDurability = 0; h.WeaponRepairs = 0; break;
                case EquipmentSlot.Armor: h.ArmorId = null; h.ArmorDurability = 0; h.ArmorRepairs = 0; break;
                default: return Result.Fail("Can't be salvaged");
            }
            foreach (var ic in yield) s.AddItem(ic.Item, ic.Count);
            StationProgress.Gain(s, StationKind.Blacksmith, SalvageXp);
            return Result.Success($"Salvaged {item.Name}");
        }

        /// <summary>Preview of what salvaging one Broken fragment of <paramref name="itemId"/> would yield
        /// (durability 0 — the floor: 1 of the cheapest input) (GG-T41).</summary>
        public static List<ItemCount> SalvageBrokenPreview(ContentDb c, string itemId)
        {
            var item = c.TryItem(itemId);
            return item == null ? new List<ItemCount>() : SalvageYield(c, item, 0);
        }

        /// <summary>Scraps one Broken fragment of <paramref name="itemId"/> for materials — Broken pieces
        /// can't be equipped or repaired, so salvage is their only use (GG-T41).</summary>
        public static Result SalvageBroken(ContentDb c, SaveData s, string itemId)
        {
            var item = c.TryItem(itemId);
            if (item == null || s.BrokenCount(itemId) < 1) return Result.Fail("Nothing to salvage");
            var yield = SalvageYield(c, item, 0);
            if (yield.Count == 0) return Result.Fail("Can't be salvaged");
            s.AddBroken(itemId, -1);
            foreach (var ic in yield) s.AddItem(ic.Item, ic.Count);
            StationProgress.Gain(s, StationKind.Blacksmith, SalvageXp);
            return Result.Success($"Salvaged Broken {item.Name}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Recipe discovery (§24.6.2, GG-T36): recipes start hidden; a recipe is discovered once its
    // station reaches its tier (§24.6.1), or earlier by reading a recipe scroll or first-ever
    // pickup of the recipe's rarest input. Discovery only gates visibility — Crafting.Craft's own
    // gold/inputs/tier checks are unchanged, since a discovered-but-locked recipe already fails
    // IsUnlocked, and a tier-reached recipe is discovered by definition.
    // ─────────────────────────────────────────────────────────────────────────────

    public static class RecipeDiscovery
    {
        public static bool IsDiscovered(SaveData s, RecipeDef r) =>
            r != null && (Crafting.IsUnlocked(s, r) || s.DiscoveredRecipes.Contains(r.Id));

        public static void Discover(SaveData s, RecipeDef r) { if (r != null) s.DiscoveredRecipes.Add(r.Id); }

        /// <summary>The input material with the highest rarity (ties broken by cost, then id) — first-ever
        /// pickup of this item discovers the recipe.</summary>
        public static ItemDef RarestInput(ContentDb c, RecipeDef r)
        {
            ItemDef rarest = null;
            foreach (var ic in r.Inputs)
            {
                var item = c.TryItem(ic.Item);
                if (item == null) continue;
                if (rarest == null || item.Rarity > rarest.Rarity ||
                    (item.Rarity == rarest.Rarity && (item.BaseCost > rarest.BaseCost ||
                        (item.BaseCost == rarest.BaseCost && string.CompareOrdinal(item.Id, rarest.Id) < 0))))
                    rarest = item;
            }
            return rarest;
        }

        /// <summary>Call when the player's first-ever copy of an item lands in the bag (battle loot) —
        /// discovers every still-hidden recipe whose rarest input is that item.</summary>
        public static void OnItemAcquired(ContentDb c, SaveData s, string itemId)
        {
            foreach (var r in c.Recipes.Values)
            {
                if (IsDiscovered(s, r)) continue;
                if (RarestInput(c, r)?.Id == itemId) Discover(s, r);
            }
        }

        /// <summary>Reads a recipe scroll: learns its recipe outright (even if still tier-locked) and consumes it.</summary>
        public static Result Learn(ContentDb c, SaveData s, string scrollItemId)
        {
            var item = c.TryItem(scrollItemId);
            if (item == null || item.Type != ItemType.RecipeScroll) return Result.Fail("Can't be read");
            if (s.Count(scrollItemId) < 1) return Result.Fail("None left");
            var recipe = c.TryRecipe(item.TeachesRecipe);
            if (recipe == null) return Result.Fail("This page leads nowhere");
            bool already = IsDiscovered(s, recipe);
            s.AddItem(scrollItemId, -1);
            Discover(s, recipe);
            var result = c.TryItem(recipe.Result);
            return Result.Success(already ? $"You already knew how to make {result?.Name ?? recipe.Result}" : $"Learned: {result?.Name ?? recipe.Result}");
        }

        /// <summary>Not discovered, but missing exactly one item (an input, or the base item an
        /// upgrade/enchant consumes) — the Forge/Brew list's "???" tease. Null when discovered, already
        /// craftable, or still missing more than one thing.</summary>
        public static ItemCount OneAwayMissing(SaveData s, RecipeDef r)
        {
            if (IsDiscovered(s, r)) return null;
            ItemCount missing = null;
            if (r.BaseItem != null && s.Count(r.BaseItem) < 1) missing = new ItemCount { Item = r.BaseItem, Count = 1 };
            foreach (var ic in r.Inputs)
            {
                if (s.Count(ic.Item) < ic.Count)
                {
                    if (missing != null) return null;
                    missing = ic;
                }
            }
            return missing;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Repair (§24.5, §25.2)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Repair
    {
        public const int RepairXp = 8;

        public static int EffectiveMax(ItemDef item, int repairs) => Math.Max(1, item.Durability - repairs);

        /// <summary>Repair price after the caravan's Armory Wagon discount.</summary>
        public static int Cost(ContentDb c, SaveData s, ItemDef item, int current, int repairs)
        {
            int baseCost = Cost(item, current, repairs);
            if (baseCost <= 0) return 0;
            return Math.Max(1, (int)Math.Ceiling(baseCost * (1f - CaravanUpgrades.Value(c, s, "armory"))));
        }

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
            int cost = Cost(c, s, item, h.WeaponDurability, h.WeaponRepairs);
            if (cost == 0) return Result.Fail("Already in top shape");
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            s.Gold -= cost;
            h.WeaponDurability = EffectiveMax(item, h.WeaponRepairs);
            h.WeaponRepairs++;
            StationProgress.Gain(s, StationKind.Blacksmith, RepairXp);
            return Result.Success($"Repaired {item.Name}");
        }

        public static Result RepairArmor(ContentDb c, SaveData s, HeroSave h)
        {
            var item = c.TryItem(h.ArmorId);
            if (item == null || item.Durability <= 0) return Result.Fail("Nothing to repair");
            int cost = Cost(c, s, item, h.ArmorDurability, h.ArmorRepairs);
            if (cost == 0) return Result.Fail("Already in top shape");
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            s.Gold -= cost;
            h.ArmorDurability = EffectiveMax(item, h.ArmorRepairs);
            h.ArmorRepairs++;
            StationProgress.Gain(s, StationKind.Blacksmith, RepairXp);
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
            float premium = s.Expedition != null ? CaravanUpgrades.Value(c, s, "medic_wagon", 1f) : 1f; // on the road: the Medic Wagon's price
            return (int)Math.Ceiling(missing * GoldPerHp * premium);
        }

        public static Result Rest(ContentDb c, SaveData s)
        {
            if (!Expeditions.InnOpen(s)) return Result.Fail("On the road — the caravan's inn opens when the expedition ends");
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
            var def = c.TryClass(h.ClassId);
            var set = new List<AbilityDef>();
            if (def != null)
                foreach (var id in def.Abilities)
                {
                    var a = c.TryAbility(id);
                    if (a != null && IsHeroUsable(a) && !h.Known.Contains(a.Id) && !set.Contains(a)) set.Add(a);
                }
            foreach (var a in c.Abilities.Values)
                if (IsHeroUsable(a) && !h.Known.Contains(a.Id) && !set.Contains(a) &&
                    (a.Kind == AbilityKind.Spell || a.Universal))
                    set.Add(a);
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

        /// <summary>Reorder the bar (owner direction 2026-10-11: organise your abilities): swap two unlocked slots.</summary>
        public static Result Swap(SaveData s, HeroSave h, int a, int b)
        {
            int unlocked = Progression.UnlockedSlots(s);
            if (a < 0 || b < 0 || a >= unlocked || b >= unlocked) return Result.Fail("Slot locked");
            if (a == b) return Result.Fail("Same slot");
            while (h.Bar.Count < Progression.MaxBarSlots) h.Bar.Add(null);
            var t = h.Bar[a]; h.Bar[a] = h.Bar[b]; h.Bar[b] = t;
            return Result.Success("Moved");
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
            if (s.Party.Count >= Progression.MaxPartySize) return Result.Fail($"Party is full ({Progression.MaxPartySize})");
            s.Party.Add(classId);
            return Result.Success("Joined the party");
        }
    }

    public static class Summon
    {
        public static int Price(ContentDb c, SaveData s) => c.Summon.BasePrice + c.Summon.PriceStep * s.Recruits;

        public static List<ClassDef> Pool(ContentDb c, SaveData s) =>
            c.Summon.Pool.Where(id => !s.LostGuests.Contains(id)).Select(c.TryClass).Where(d => d != null).ToList();

        public static Result Recruit(ContentDb c, SaveData s, string classId)
        {
            if (!c.Summon.Pool.Contains(classId)) return Result.Fail("Not summonable");
            if (s.LostGuests.Contains(classId)) return Result.Fail("Lost forever");
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
