using System.Collections.Generic;

namespace Gaiden.Core.Content
{
    // Plain data definitions deserialized from Assets/Gaiden/Resources/Content/*.json.
    // Property names are the JSON keys (PascalCase); enums serialize as strings.

    public enum Team { Hero, Enemy }

    /// <summary>How an enemy chooses which hero to go after (bible §14.1). Auto derives it from INT and tags.</summary>
    public enum Targeting { Auto, Closest, Weakest, Threat, Squishy, Erratic }


    public enum DamageType { Physical, Fire, Ice, Lightning, Poison, Holy, Dark, Arcane }

    public enum AbilityKind { Skill, Spell, Item, Passive }

    public enum TargetShape { Self, SingleActor, SingleTile, Square, Diamond, Cross, Plus, Row, Column, AllEnemies, AllAllies }

    public enum TargetMode { Auto, PickActor, PickTile }

    public enum TargetFilter { Any, EnemyOnly, AllyOnly, EmptyOnly }

    public enum ProjectileMotion { None, Straight, Bezier, Homing, Spiral, Twist, Strike }

    public enum BuffKind { Buff, Debuff }

    /// <summary>What happens when a buff is applied to a unit that already has it (bible §8.6).</summary>
    public enum BuffStacking { Refresh, Stack, Ignore, Upgrade }

    public enum DurationUnit { Turns, Ticks }

    public enum ItemType { Equipment, Consumable, CraftingMaterial, QuestItem }

    public enum ItemRarity { Junk, Common, Uncommon, Rare, Epic, Legendary }

    public enum EquipmentSlot { None, Weapon, Armor, Relic1, Relic2, Relic3 }

    public enum CraftStation { Forge, Alchemy, Enchant }

    public enum ChargeShape { Single, Line }

    public sealed class Stats
    {
        public float Strength;
        public float Vitality;
        public float Agility;
        public float Speed;
        public float Stamina;
        public float Intelligence;
        public float Wisdom;
        public float Luck;

        public Stats Clone() => (Stats)MemberwiseClone();

        public static Stats operator +(Stats a, Stats b) => new Stats
        {
            Strength = a.Strength + b.Strength,
            Vitality = a.Vitality + b.Vitality,
            Agility = a.Agility + b.Agility,
            Speed = a.Speed + b.Speed,
            Stamina = a.Stamina + b.Stamina,
            Intelligence = a.Intelligence + b.Intelligence,
            Wisdom = a.Wisdom + b.Wisdom,
            Luck = a.Luck + b.Luck,
        };

        public static Stats operator *(Stats a, float k) => new Stats
        {
            Strength = a.Strength * k,
            Vitality = a.Vitality * k,
            Agility = a.Agility * k,
            Speed = a.Speed * k,
            Stamina = a.Stamina * k,
            Intelligence = a.Intelligence * k,
            Wisdom = a.Wisdom * k,
            Luck = a.Luck * k,
        };
    }

    public sealed class MilestoneGrowth
    {
        public int Level;
        public Stats Growth = new Stats();
    }

    public sealed class Footprint
    {
        public int W = 1;
        public int H = 1;
    }

    /// <summary>Portrait crop hints carried over from the old ThumbnailSettings (pixel offset + zoom).</summary>
    public sealed class PortraitCrop
    {
        public int X;
        public int Y;
        public float Zoom = 1f;
    }

    public sealed class BossPhaseDef
    {
        /// <summary>Phase begins when HP fraction drops to or below this (0..1).</summary>
        public float HpThreshold;
        public bool PrefersCharge;
        /// <summary>Ability fired once on phase entry (e.g. Quicken on self = enrage). Optional.</summary>
        public string OnEnterAbility;
        public string Announce;
    }

    public sealed class ClassDef
    {
        public string Id;
        public string Name;
        public Team Team;
        public List<string> Tags = new List<string>();
        public string Description;
        public string Lore;
        public string Card;
        public string Portrait;
        public PortraitCrop Crop;
        public Footprint Footprint = new Footprint();
        public Stats BaseStats = new Stats();
        public Stats Growth = new Stats();
        public List<MilestoneGrowth> Milestones = new List<MilestoneGrowth>();
        public Dictionary<DamageType, float> Resistances = new Dictionary<DamageType, float>();
        /// <summary>Abilities this class can learn/use (hero) or uses (enemy charge / trap).</summary>
        public List<string> Abilities = new List<string>();
        /// <summary>Default ability bar preset (ability ids; items as "item:id").</summary>
        public List<string> DefaultLoadout = new List<string>();
        public string DropTable;
        public int MoveRange = 1;
        /// <summary>Trap id left on every tile this enemy steps off (slime trail, mire, quicksand — §14.4).</summary>
        public string Trail;
        /// <summary>Buff a landed enemy attack may inflict (venom, frost, blinding dirt — §14.4)…</summary>
        public string OnHitBuff;
        /// <summary>…with this chance per landed hit.</summary>
        public float OnHitChance;
        /// <summary>Class id of the remains this enemy leaves (gravestone, bone pile — §14.5). Destroy them or it rises again.</summary>
        public string Remains;
        /// <summary>Damage dealt to every adjacent hero when this enemy dies (cyber-zombie self-destruct). 0 = none.</summary>
        public int DeathBurst;
        /// <summary>Enemy targeting personality (§14.1).</summary>
        public Targeting Targeting = Targeting.Auto;
        /// <summary>Straight-line attack reach for the Ranged archetype (1 = melee only).</summary>
        public int AttackRange = 1;
        /// <summary>Buff ids this class shrugs off ("Immune!") — the Mechanical archetype.</summary>
        public List<string> ImmuneBuffs = new List<string>();
        public int XpReward = 10;
        public List<BossPhaseDef> BossPhases = new List<BossPhaseDef>();
        /// <summary>Enemy charge spell id (Caster archetype). Optional.</summary>
        public string ChargeAbility;
        public ChargeShape ChargeShape = ChargeShape.Single;
        /// <summary>Trap id laid by trap-layers. Optional.</summary>
        public string Trap;
        /// <summary>Snake body segments grown behind this head at spawn (0 = not a snake).</summary>
        public int SnakeSegments;

        public bool HasTag(string tag) => Tags != null && Tags.Contains(tag);
    }

    public sealed class AbilityDef
    {
        public string Id;
        public string Name;
        public string Description;
        public AbilityKind Kind;
        /// <summary>Mana cost in generic orbs (0 for skills/items).</summary>
        public int Cost;
        public float CastTime;
        public int CooldownTurns;
        public TargetShape Shape = TargetShape.SingleActor;
        public int Radius;
        public TargetMode Mode = TargetMode.PickActor;
        public TargetFilter Filter = TargetFilter.EnemyOnly;
        public ProjectileMotion Motion = ProjectileMotion.None;
        public DamageType DamageType = DamageType.Arcane;
        public int BaseDamage;
        public int BaseHeal;
        public string ApplyBuff;
        public float ApplyBuffChance = 1f;
        public bool RemovesDebuffs;
        public bool StealsMana;
        public float HastenU;
        public bool IsTeleport;
        /// <summary>Tiles the target is shoved straight away from the caster (shield bash) — §6.3.</summary>
        public int Knockback;
        /// <summary>The pick must be cardinally adjacent to the caster (melee skills).</summary>
        public bool AdjacentOnly;
        public bool IsScan;
        /// <summary>Random WUBRG orbs added to the bank on resolve (ethers).</summary>
        public int GrantsOrbs;
        /// <summary>Removes just this buff id (Eye Drops → blinded).</summary>
        public string RemovesBuff;
        /// <summary>Brings a fallen ally back onto a free tile beside the caster with BaseHeal HP.</summary>
        public bool Revives;
        /// <summary>Ends the battle as an escape (no rewards, no defeat penalty).</summary>
        public bool Escape;
        /// <summary>Damage ×BonusMultiplier against targets with this tag (Holy Water vs Undead).</summary>
        public string BonusVsTag;
        public float BonusMultiplier = 1f;
        public string Icon;
        public string CastVfx;
        public string ProjectileVfx;
        public string ImpactVfx;
        public string LingerVfx;
        public string Sfx;
    }

    public sealed class BuffDef
    {
        public string Id;
        public string Name;
        public BuffKind Kind = BuffKind.Debuff;
        public DurationUnit Unit = DurationUnit.Turns;
        public int Duration = 1;
        public int DamagePerTick;
        public int HealPerTick;
        public bool Immobile;
        public bool HaltsTimeline;
        public bool BreaksOnDamage;
        public bool BreaksOnMove;
        public float IncomingDamageMultiplier = 1f;
        public float TimelineSpeedMultiplier = 1f;
        public float AccuracyMultiplier = 1f;
        public bool BlocksSpells;
        public string OnExpireApply;
        public BuffStacking Stacking = BuffStacking.Refresh;
        /// <summary>Cap for Stack buffs (0 = unlimited). Burning stacks to 3.</summary>
        public int MaxStacks;
        public string Letter;
        public string Color;
    }

    public sealed class ItemDef
    {
        public string Id;
        public string Name;
        public string Description;
        public ItemType Type;
        public ItemRarity Rarity = ItemRarity.Common;
        public EquipmentSlot Slot = EquipmentSlot.None;
        public string WeaponType;
        public int BaseCost;
        /// <summary>-1 = BaseCost/2.</summary>
        public int SellValue = -1;
        public int MaxStack = 99;
        public int Durability;
        public int BaseHealing;
        public int BaseDamage;
        public Stats Stats = new Stats();
        public int BattleStartManaOrbs;
        /// <summary>Consumable that casts this ability on use (e.g. Sleep Dart → "sleep").</summary>
        public string OnUseAbility;
        public Dictionary<DamageType, float> Resistances = new Dictionary<DamageType, float>();
        public bool SoldByVendor;
        public string Icon;
        public DamageType DamageType = DamageType.Physical;
        public int GrantsOrbs;
        public string CuresBuff;
        public bool CuresAll;
        public bool Revives;
        public bool Escape;
        /// <summary>Usable only from the camp Items menu (Tent).</summary>
        public bool OutOfBattleOnly;
        public string BonusVsTag;
        public float BonusMultiplier = 1f;
        /// <summary>Extra ManaBank slots while equipped (bible §3.1.4 knob), summed over the party, max +4.</summary>
        public int BankCapacityBonus;
        /// <summary>Relic passive: this hero's pincer strikes deal +X (fraction).</summary>
        public float PincerDamageBonus;
        /// <summary>Relic passive: HP restored each time control returns to the player.</summary>
        public int RegenPerTurn;
        /// <summary>Relic passive: start every battle with Protection.</summary>
        public bool StartsProtected;

        public int EffectiveSellValue => SellValue >= 0 ? SellValue : System.Math.Max(1, BaseCost / 2);
        public bool IsRelic => Slot == EquipmentSlot.Relic1 || Slot == EquipmentSlot.Relic2 || Slot == EquipmentSlot.Relic3;
    }

    public sealed class ItemCount
    {
        public string Item;
        public int Count = 1;
    }

    public sealed class RecipeDef
    {
        public string Id;
        public CraftStation Station;
        public List<ItemCount> Inputs = new List<ItemCount>();
        public int Gold;
        /// <summary>Optional base item consumed (upgrade/enchant).</summary>
        public string BaseItem;
        public string Result;
        public int ResultCount = 1;
    }

    public sealed class DropEntry
    {
        public string Item;
        public float Chance;
        public int Min = 1;
        public int Max = 1;
    }

    public sealed class DropTableDef
    {
        public string Id;
        public int CoinsMin;
        public int CoinsMax;
        public List<DropEntry> Entries = new List<DropEntry>();
    }

    public sealed class SpawnDef
    {
        public string Class;
        public int Level = 1;
        /// <summary>Optional fixed tile (0-based col); -1 = random free tile.</summary>
        public int X = -1;
        /// <summary>Optional fixed tile (0-based row, 0 = top); -1 = random free tile.</summary>
        public int Y = -1;
    }

    public sealed class WaveDef
    {
        public List<SpawnDef> Enemies = new List<SpawnDef>();
    }

    public sealed class StageDef
    {
        public string Id;
        public string Name;
        public string Theme;
        public string Background;
        public string Music;
        public bool IsTest;
        /// <summary>Physical blocks (rocks, pillars, wreckage): tiles no unit may enter, land on, be pushed into or spawn on (§1.1.1).</summary>
        public List<Coord> Blocks = new List<Coord>();
        public List<WaveDef> Waves = new List<WaveDef>();
    }

    public sealed class ThemeDef
    {
        public string Id;
        public string Name;
        public string Crawl;
        public List<string> Stages = new List<string>();
    }

    public sealed class CampaignDef
    {
        public List<ThemeDef> Themes = new List<ThemeDef>();
    }

    public sealed class TrapDef
    {
        public string Id;
        public string Name;
        public int Damage;
        public string ApplyBuff;
        public float LayChance = 0.45f;
        /// <summary>Element of the trap's damage (default Poison).</summary>
        public DamageType DamageType = DamageType.Poison;
    }

    public sealed class BountyDef
    {
        public string Id;
        public string Name;
        public string TargetClass;
        public int Kills;
        public int RewardGold;
        public string RewardItem;
    }

    public sealed class SummonDef
    {
        public List<string> Pool = new List<string>();
        public int BasePrice = 250;
        public int PriceStep = 250;
    }

    public sealed class EnchantDef
    {
        public string Id;
        public string Name;
        public string Element;
        public string Essence;
        public int ArcaneDust = 2;
        public int Gold = 150;
        public Stats Bonus = new Stats();
    }
}
