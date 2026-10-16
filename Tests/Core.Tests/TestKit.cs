using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using NUnit.Framework;

namespace Gaiden.Tests
{
    public static class TestKit
    {
        public static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "Gaiden"))) dir = dir.Parent;
                if (dir == null) throw new DirectoryNotFoundException("Repo root not found");
                return dir.FullName;
            }
        }

        public static string ContentDir => Path.Combine(RepoRoot, "Assets", "Gaiden", "Resources", "Content");

        private static ContentDb real;
        public static ContentDb Real => real ??= ContentLoader.Load(name =>
        {
            var p = Path.Combine(ContentDir, name + ".json");
            return File.Exists(p) ? File.ReadAllText(p) : null;
        });

        /// <summary>A tiny, fully controlled content set for rules tests (stats chosen so outcomes are predictable).</summary>
        public static ContentDb Mini()
        {
            var db = new ContentDb();
            Stats S(float str, float vit, float spd, float lck = 0, float intl = 10, float wis = 0) =>
                new Stats { Strength = str, Vitality = vit, Agility = 10, Speed = spd, Luck = lck, Intelligence = intl, Wisdom = wis };
            db.Classes["knight"] = new ClassDef { Id = "knight", Name = "Knight", Team = Team.Hero, Tags = { "Humanoid" }, BaseStats = S(20, 10, 10) };
            db.Classes["cleric"] = new ClassDef { Id = "cleric", Name = "Cleric", Team = Team.Hero, Tags = { "Humanoid" }, BaseStats = S(20, 10, 10) };
            db.Classes["mage"] = new ClassDef { Id = "mage", Name = "Mage", Team = Team.Hero, Tags = { "Humanoid" }, BaseStats = S(10, 10, 10) };
            db.Classes["goblin"] = new ClassDef { Id = "goblin", Name = "Goblin", Team = Team.Enemy, Tags = { "Humanoid" }, BaseStats = S(5, 2, 10), MoveRange = 0, DropTable = "goblin" };
            db.Classes["wolf"] = new ClassDef { Id = "wolf", Name = "Wolf", Team = Team.Enemy, Tags = { "Beast" }, BaseStats = S(5, 2, 10), MoveRange = 1 };
            db.Classes["tank"] = new ClassDef { Id = "tank", Name = "Tank", Team = Team.Enemy, Tags = { "Beast" }, BaseStats = S(1, 200, 1), MoveRange = 0 };
            db.Classes["boss"] = new ClassDef { Id = "boss", Name = "Boss", Team = Team.Enemy, Tags = { "Boss" }, BaseStats = S(5, 50, 5), Footprint = new Footprint { W = 2, H = 2 }, MoveRange = 0 };
            db.Classes["witch"] = new ClassDef { Id = "witch", Name = "Witch", Team = Team.Enemy, Tags = { "Magic" }, BaseStats = S(5, 50, 30, intl: 20), MoveRange = 0, ChargeAbility = "charge_bolt" };
            db.DropTables["goblin"] = new DropTableDef { Id = "goblin", CoinsMin = 3, CoinsMax = 3, Entries = { new DropEntry { Item = "fang", Chance = 1f } } };
            db.Items["fang"] = new ItemDef { Id = "fang", Name = "Fang", Type = ItemType.CraftingMaterial, BaseCost = 4 };
            db.Items["potion"] = new ItemDef { Id = "potion", Name = "Potion", Type = ItemType.Consumable, BaseHealing = 30, MaxStack = 9, BaseCost = 25 };
            db.Items["sword"] = new ItemDef { Id = "sword", Name = "Sword", Type = ItemType.Equipment, Slot = EquipmentSlot.Weapon, Durability = 2, BaseDamage = 3, Stats = new Stats { Strength = 3 } };
            db.Abilities["fireball"] = new AbilityDef { Id = "fireball", Name = "Fireball", Kind = AbilityKind.Spell, Cost = 2, BaseDamage = 18, DamageType = DamageType.Fire, ApplyBuff = "burning", Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            db.Abilities["meteor"] = new AbilityDef { Id = "meteor", Name = "Meteor", Kind = AbilityKind.Spell, Cost = 2, CastTime = 3f, BaseDamage = 22, DamageType = DamageType.Fire, Shape = TargetShape.Diamond, Radius = 2, Mode = TargetMode.PickTile, Filter = TargetFilter.EnemyOnly };
            db.Abilities["heal"] = new AbilityDef { Id = "heal", Name = "Heal", Kind = AbilityKind.Spell, Cost = 1, BaseHeal = 25, Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.AllyOnly };
            db.Abilities["frost"] = new AbilityDef { Id = "frost", Name = "Frost", Kind = AbilityKind.Spell, Cost = 2, BaseDamage = 10, DamageType = DamageType.Ice, ApplyBuff = "frozen", Shape = TargetShape.Square, Radius = 1, Mode = TargetMode.PickTile, Filter = TargetFilter.EnemyOnly };
            db.Abilities["teleport"] = new AbilityDef { Id = "teleport", Name = "Teleport", Kind = AbilityKind.Skill, CooldownTurns = 3, IsTeleport = true, Shape = TargetShape.SingleTile, Mode = TargetMode.PickTile, Filter = TargetFilter.EmptyOnly };
            db.Abilities["steal"] = new AbilityDef { Id = "steal", Name = "Steal", Kind = AbilityKind.Skill, CooldownTurns = 3, StealsMana = true, Shape = TargetShape.Cross, Radius = 1, Mode = TargetMode.Auto, Filter = TargetFilter.EnemyOnly };
            db.Abilities["quicken"] = new AbilityDef { Id = "quicken", Name = "Quicken", Kind = AbilityKind.Spell, Cost = 1, HastenU = 0.3f, Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.Any };
            db.Abilities["charge_bolt"] = new AbilityDef { Id = "charge_bolt", Name = "Dark Bolt", Kind = AbilityKind.Spell, CastTime = 2.5f, DamageType = DamageType.Dark, Shape = TargetShape.SingleActor };
            db.Abilities["shield_bash"] = new AbilityDef { Id = "shield_bash", Name = "Shield Bash", Kind = AbilityKind.Skill, CooldownTurns = 2, BaseDamage = 12, Knockback = 2, AdjacentOnly = true, DamageType = DamageType.Physical, Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            db.Buffs["burning"] = new BuffDef { Id = "burning", Name = "Burning", Unit = DurationUnit.Ticks, Duration = 5, DamagePerTick = 4, OnExpireApply = "warm" };
            db.Buffs["warm"] = new BuffDef { Id = "warm", Name = "Warm", Unit = DurationUnit.Ticks, Duration = 3 };
            db.Buffs["frozen"] = new BuffDef { Id = "frozen", Name = "Frozen", Unit = DurationUnit.Turns, Duration = 1, Immobile = true, HaltsTimeline = true, OnExpireApply = "wet" };
            db.Buffs["wet"] = new BuffDef { Id = "wet", Name = "Wet", Unit = DurationUnit.Ticks, Duration = 6 };
            db.Buffs["protection"] = new BuffDef { Id = "protection", Name = "Protection", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 1, IncomingDamageMultiplier = 0.85f };
            db.Buffs["sleep"] = new BuffDef { Id = "sleep", Name = "Sleep", Unit = DurationUnit.Turns, Duration = 3, Immobile = true, BreaksOnDamage = true, BreaksOnMove = true };
            return db;
        }

        public static StageDef Stage(params (string cls, int x, int y)[] enemies) => StageWaves(enemies);

        public static StageDef StageWaves(params (string cls, int x, int y)[][] waves)
        {
            var st = new StageDef { Id = "t", Name = "T", IsTest = true };
            foreach (var w in waves)
            {
                var wave = new WaveDef();
                foreach (var e in w) wave.Enemies.Add(new SpawnDef { Class = e.cls, Level = 1, X = e.x, Y = e.y });
                st.Waves.Add(wave);
            }
            return st;
        }

        public static (BattleState state, List<BattleEvent> events) Battle(ContentDb db, StageDef stage, params (string cls, int x, int y)[] heroes)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5 };
            foreach (var h in heroes) setup.Party.Add(new HeroSpec { ClassId = h.cls, Start = new Coord(h.x, h.y) });
            var ev = new List<BattleEvent>();
            var s = BattleEngine.Create(db, setup, ev);
            return (s, ev);
        }

        public static Unit At(BattleState s, int x, int y) => s.UnitAt(new Coord(x, y));

        public static Unit Hero(BattleState s, string cls) => s.Units.First(u => u.IsHero && u.Def.Id == cls);

        public static List<Coord> P(params (int x, int y)[] steps) => steps.Select(t => new Coord(t.x, t.y)).ToList();

        /// <summary>Structural invariants that must hold after every command.</summary>
        public static void AssertInvariants(BattleState s)
        {
            var seen = new Dictionary<Coord, int>();
            foreach (var u in s.Units)
            {
                Assert.That(u.Hp, Is.InRange(0, u.MaxHp));
                if (!u.Alive) continue;
                foreach (var t in u.Tiles())
                {
                    Assert.That(BattleState.InBounds(t), Is.True, $"{u} out of bounds at {t}");
                    Assert.That(seen.ContainsKey(t), Is.False, $"Two units on {t}: {u.Id} and {(seen.ContainsKey(t) ? seen[t] : -1)}");
                    seen[t] = u.Id;
                }
            }
            Assert.That(s.Bank.Count, Is.InRange(0, s.Bank.Capacity));
            foreach (var e in s.Timeline)
            {
                var owner = s.UnitById(e.OwnerId);
                Assert.That(owner != null && owner.Alive, Is.True, $"Timeline entry {e.Id} owned by dead/missing unit {e.OwnerId}");
            }
            if (!s.IsOver)
            {
                Assert.That(s.Units.Any(u => u.Alive && u.IsHero), Is.True, "No heroes but battle not over");
                // A reinforcement queued for the current wave (§14.8) legitimately holds the battle open with
                // the board clear — mirrors the same check BattleRunner.CheckEnd makes before ending a wave.
                bool reinforcementPending = s.PendingSpawns.Any(p => !p.Consumed && p.Wave == s.Wave);
                Assert.That(s.Units.Any(u => u.Alive && !u.IsHero) || reinforcementPending, Is.True, "No enemies but battle not over");
            }
        }
    }
}
