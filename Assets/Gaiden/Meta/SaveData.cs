using System;
using System.Collections.Generic;

namespace Gaiden.Meta
{
    /// <summary>
    /// One save slot (bible §15). Plain data, JSON-serialized by the app's save store.
    /// Bump <see cref="CurrentVersion"/> and add a step to <see cref="SaveMigrations"/> when the shape changes.
    /// </summary>
    /// <summary>An expedition in progress: a theme's battles fought in order, wounds and mana carried between them (§22.5).</summary>
    public sealed class ExpeditionSave
    {
        public string ThemeId;
        /// <summary>Index (into the theme's stages) of the next battle.</summary>
        public int Next;
        /// <summary>Orbs left in the bank at the end of the last battle; they start the next one.</summary>
        public int CarriedOrbs;
        /// <summary>Coins earned so far on this expedition (the completion bonus is a share of it).</summary>
        public int Coins;
    }

    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public string Name = "Lightbearer";
        public DateTime CreatedUtc = DateTime.UtcNow;
        public DateTime UpdatedUtc = DateTime.UtcNow;
        public double PlaySeconds;

        public int Gold;
        public int LifetimeCoins;
        /// <summary>Campaign index of the furthest cleared stage (-1 = none).</summary>
        public int HighestClearedStage = -1;
        public HashSet<string> ClearedStages = new HashSet<string>();
        public HashSet<string> SeenCrawls = new HashSet<string>();
        public string LastStage;

        public List<HeroSave> Roster = new List<HeroSave>();
        public List<string> Party = new List<string>();
        public Dictionary<string, int> Inventory = new Dictionary<string, int>();
        public int Recruits;
        /// <summary>Caravan upgrade id → level bought (§25.12).</summary>
        public Dictionary<string, int> Caravan = new Dictionary<string, int>();
        /// <summary>Crafting station id ("blacksmith"/"alchemist") → accumulated XP (§24.6.1, GG-T35).</summary>
        public Dictionary<string, int> StationXp = new Dictionary<string, int>();
        /// <summary>Guest allies who died in the battle they were met in — gone for good (§25.11).</summary>
        public HashSet<string> LostGuests = new HashSet<string>();
        /// <summary>Recipe ids discovered early, before the crafting station reached their tier — by reading a
        /// recipe scroll or first-ever pickup of the recipe's rarest input (§24.6.2, GG-T36). A recipe whose
        /// tier the station has already reached is always discovered regardless of this set.</summary>
        public HashSet<string> DiscoveredRecipes = new HashSet<string>();
        /// <summary>Spare equipment that has been used: item id → its worn copies (per-instance durability, US-246).</summary>
        public Dictionary<string, List<WornGear>> Worn = new Dictionary<string, List<WornGear>>();
        /// <summary>Shattered/broken gear kept as salvage fodder instead of lost outright: item id → count of
        /// Broken copies. Broken pieces can't be equipped or repaired — only scrapped at the Blacksmith (GG-T41).</summary>
        public Dictionary<string, int> Broken = new Dictionary<string, int>();
        /// <summary>Advances once per finished battle; seeds the Merchant's "Used / Bargain" shelf so it
        /// turns over on every return to camp but replays identically from the same save (GG-T38).</summary>
        public int BargainRotation;
        public bool TutorialDone;
        /// <summary>New Game+ cycle (0 = first run). Unlocked by clearing the last campaign stage (US-332).</summary>
        public int NewGamePlus;

        /// <summary>The expedition under way (null = at the caravan between expeditions) — §22.5.</summary>
        public ExpeditionSave Expedition;
        public BountySave Bounty = new BountySave();
        public Dictionary<string, BestiaryEntry> Bestiary = new Dictionary<string, BestiaryEntry>();
        public SettingsSave Settings = new SettingsSave();

        public HeroSave Hero(string classId) => Roster.Find(h => string.Equals(h.ClassId, classId, StringComparison.OrdinalIgnoreCase));

        public int Count(string itemId) => itemId != null && Inventory.TryGetValue(itemId, out var n) ? n : 0;

        public void AddItem(string itemId, int n)
        {
            if (itemId == null || n == 0) return;
            int v = Count(itemId) + n;
            if (v <= 0) Inventory.Remove(itemId); else Inventory[itemId] = v;
            // Worn copies can't outnumber owned copies; when copies leave the bag the most-worn go first.
            if (Worn.TryGetValue(itemId, out var worn))
            {
                worn.Sort((a, b) => b.Durability.CompareTo(a.Durability));
                while (worn.Count > Count(itemId)) worn.RemoveAt(worn.Count - 1);
                if (worn.Count == 0) Worn.Remove(itemId);
            }
        }

        public int BrokenCount(string itemId) => itemId != null && Broken.TryGetValue(itemId, out var n) ? n : 0;

        public void AddBroken(string itemId, int n)
        {
            if (itemId == null || n == 0) return;
            int v = BrokenCount(itemId) + n;
            if (v <= 0) Broken.Remove(itemId); else Broken[itemId] = v;
        }
    }

    public sealed class HeroSave
    {
        public string ClassId;
        public int TotalXp;
        /// <summary>Current HP; 0 = full (wounds carry between battles, §15.1).</summary>
        public int Hp;
        public List<string> Known = new List<string>();
        /// <summary>Five ability-bar entries: ability id, "item:id", or null.</summary>
        public List<string> Bar = new List<string> { null, null, null, null, null };

        public string WeaponId;
        public int WeaponDurability;
        public int WeaponRepairs;
        /// <summary>Masterwork stat multiplier (§24.6.2, GG-T46); 1 = normal gear.</summary>
        public float WeaponMastery = 1f;
        public string ArmorId;
        public int ArmorDurability;
        public int ArmorRepairs;
        public float ArmorMastery = 1f;
        public List<string> Relics = new List<string> { null, null, null };
    }

    public sealed class WornGear
    {
        public int Durability;
        public int Repairs;
        /// <summary>Masterwork stat multiplier this specific copy rolled at craft time (§24.6.2, GG-T46); 1 = normal.</summary>
        public float Mastery = 1f;
    }

    public sealed class BountySave
    {
        public string ActiveId;
        public int Progress;
        public HashSet<string> Claimed = new HashSet<string>();
    }

    public sealed class BestiaryEntry
    {
        public bool Seen;
        public bool Scanned;
        public int Defeated;
    }

    public sealed class SettingsSave
    {
        public float MusicVolume = 0.6f;
        public float SfxVolume = 0.85f;
        public bool MuteMusic;
        public bool MuteSfx;
        public bool ColorblindMode;
        public bool ReduceMotion;
        /// <summary>Terra Battle-style drag timer (§2, act-advanced amendment). Off by default.</summary>
        public bool DragTimer;
        /// <summary>Battle animation speed multiplier (1 = normal).</summary>
        public float BattleSpeed = 1f;
        /// <summary>Close-up portrait cut-in on pincers (ported from GridGame2026).</summary>
        public bool PincerCutIns = true;
        /// <summary>Pick a target, then confirm (owner direction 2026-10-11). Off = commit on the pick, for veterans.</summary>
        public bool ConfirmActions = true;
        /// <summary>Menu color theme by name (App.Art.Themes); unknown/missing names fall back to Classic.</summary>
        public string MenuTheme = "Classic";
        /// <summary>Body font by name (App.Fonts.Presets); unknown/missing names fall back to Classic.</summary>
        public string FontName = "Classic";
        /// <summary>Difficulty preset + multipliers (§22.9). Scaffolding only — every multiplier is inert
        /// at 1.0 until balance work consumes it; only Custom lets the player move them away from 1.0 today.</summary>
        public DifficultySettings Difficulty = new DifficultySettings();
    }

    /// <summary>
    /// Difficulty presets and their multipliers (owner direction 2026-10-11: build the selection and its
    /// multipliers now, before any of them do anything, so (a) later balance work has a lever to turn and
    /// (b) telemetry can already tag every battle with the active multipliers to learn what affects what.
    /// A built-in preset (Easy/Normal/Hard) resets every multiplier to 1.0 — they're placeholders until
    /// balance work gives each one a real curve; Custom is the only preset where the player's own values
    /// stick. "Higher difficulty = better rewards" is the intended direction once that work lands, not yet.
    /// </summary>
    public sealed class DifficultySettings
    {
        public string Preset = "Normal"; // "Easy" | "Normal" | "Hard" | "Custom"
        public float EnemyHp = 1f;
        public float EnemyDamage = 1f;
        public float EnemySpeed = 1f;
        public float RewardGold = 1f;
        public float RewardXp = 1f;
        public float ItemDropRate = 1f;
    }

    public static class SaveMigrations
    {
        /// <summary>Upgrades an older save in place. Unknown future versions are rejected.</summary>
        public static SaveData Migrate(SaveData s)
        {
            if (s == null) return null;
            if (s.Version > SaveData.CurrentVersion) throw new InvalidOperationException($"Save version {s.Version} is newer than this build");
            // v1 is the first shape; future steps go here: if (s.Version == 1) { ...; s.Version = 2; }
            s.Bounty ??= new BountySave();
            s.Worn ??= new Dictionary<string, List<WornGear>>();
            s.Broken ??= new Dictionary<string, int>();
            s.Settings ??= new SettingsSave();
            s.StationXp ??= new Dictionary<string, int>();
            s.DiscoveredRecipes ??= new HashSet<string>();
            foreach (var h in s.Roster)
            {
                while (h.Bar.Count < 5) h.Bar.Add(null);
                while (h.Relics.Count < 3) h.Relics.Add(null);
            }
            return s;
        }
    }
}
