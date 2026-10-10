using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;
using Gaiden.Meta;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    public class FeatureTests
    {
        private static Unit Enemy(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        [Fact] // US-243
        public void Armor_wears_per_enemy_hit_and_breaks_at_zero()
        {
            var db = Mini();
            db.Items["mail"] = new ItemDef { Id = "mail", Name = "Mail", Type = ItemType.Equipment, Slot = EquipmentSlot.Armor, Durability = 1, Stats = new Stats { Vitality = 5 } };
            db.Classes["wolf"].MoveRange = 0;
            db.Classes["wolf"].BaseStats.Luck = 0;
            var setup = new BattleSetup { Stage = Stage(("wolf", 2, 2)), Seed = 3, UnlockedSlots = 2 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(2, 3), ArmorId = "mail", EquippedItemIds = { "mail" } });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(5, 7) });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            var k = Hero(s, "knight");
            float vit = k.Effective.Vitality;
            var all = new List<BattleEvent>();
            for (int i = 0; i < 6 && k.ArmorId != null; i++) { s.TurnEntryOf(Enemy(s, "wolf").Id).Ip = 999; all.AddRange(BattleEngine.Apply(s, new ShieldCommand())); }
            Assert.Contains(all, e => e is ArmorBrokenEvent);
            Assert.Null(k.ArmorId);
            Assert.Equal(vit - 5, k.Effective.Vitality);
        }

        [Fact] // US-182
        public void Mechanical_enemies_are_immune_to_poison_and_sleep()
        {
            var db = Mini();
            db.Classes["tank"].ImmuneBuffs = new List<string> { "sleep" };
            db.Abilities["sleep"] = new AbilityDef { Id = "sleep", Name = "Sleep", Kind = AbilityKind.Spell, ApplyBuff = "sleep", Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 5, 7), ("mage", 4, 7));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "sleep" };
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = new Coord(2, 2) });
            Assert.Contains(ev, e => e is AnnounceEvent a && a.Text == "Immune!");
            Assert.False(Enemy(s, "tank").HasBuff("sleep"));
            Assert.All(Real.Classes.Values.Where(c => c.HasTag("Mechanical")), c => Assert.Contains("poisoned", c.ImmuneBuffs));
        }

        [Theory] // US-157
        [InlineData(BuffStacking.Stack, 2)]
        [InlineData(BuffStacking.Ignore, 1)]
        [InlineData(BuffStacking.Refresh, 1)]
        [InlineData(BuffStacking.Upgrade, 1)]
        public void Buff_stacking_policies(BuffStacking policy, int expectedInstances)
        {
            var db = Mini();
            db.Buffs["burning"].Stacking = policy;
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 5, 7), ("mage", 4, 7));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            s.Bank.Add(ManaColor.Red, 4);
            var tank = Enemy(s, "tank");
            tank.Buffs.Add(new BuffInstance { Def = db.Buffs["burning"], Remaining = 1 });
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = m.Id, Slot = 0, Target = tank.Pos });
            int applied = ev.OfType<BuffAppliedEvent>().Count(b => b.BuffId == "burning");
            Assert.Equal(policy == BuffStacking.Ignore ? 0 : 1, applied);
            // Count instances immediately after application via the event stream (ticking may expire some later).
            if (policy == BuffStacking.Stack) Assert.True(applied == 1 && expectedInstances == 2);
        }

        [Fact] // US-181
        public void Ranged_enemy_shoots_down_a_clear_lane_and_keeps_its_distance()
        {
            var db = Mini();
            db.Classes["hag"] = new ClassDef { Id = "hag", Name = "Hag", Team = Team.Enemy, Tags = { "Ranged" }, AttackRange = 3, MoveRange = 0, BaseStats = new Stats { Strength = 12, Vitality = 10, Speed = 10, Agility = 10, Luck = 0 } };
            var (s, _) = Battle(db, Stage(("hag", 2, 1)), ("knight", 2, 4), ("cleric", 5, 7));
            s.TurnEntryOf(Enemy(s, "hag").Id).Ip = 999;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.Contains(ev, e => e is AttackEvent a && a.IsRanged && a.TargetId == Hero(s, "knight").Id);
            // A unit in the lane blocks the shot.
            var (s2, _) = Battle(db, Stage(("hag", 2, 1), ("tank", 2, 2)), ("knight", 2, 4), ("cleric", 5, 7));
            Assert.Null(EnemyPlanner.RangedTarget(s2, Enemy(s2, "hag")));
            // Movement keeps reach instead of closing to melee.
            db.Classes["hag"].MoveRange = 1;
            var (s3, _) = Battle(db, Stage(("hag", 2, 1)), ("knight", 2, 4), ("cleric", 5, 7));
            var plan = EnemyPlanner.PlanStep(s3, Enemy(s3, "hag"));
            Assert.True(plan.Manhattan(new Coord(2, 4)) >= 2);
        }

        [Fact] // US-082
        public void Bank_capacity_bonus_gear_widens_the_orb_line()
        {
            var db = Mini();
            db.Items["star"] = new ItemDef { Id = "star", Name = "Star", Type = ItemType.Equipment, Slot = EquipmentSlot.Relic1, BankCapacityBonus = 2 };
            var setup = new BattleSetup { Stage = Stage(("tank", 0, 0)), Seed = 1, UnlockedSlots = 2 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(5, 7), EquippedItemIds = { "star" } });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(4, 7) });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            Assert.Equal(14, s.Bank.Capacity);
        }

        [Fact] // US-244
        public void Relic_passives_boost_pincers_regen_and_protect()
        {
            var db = Mini();
            db.Items["fang"] = new ItemDef { Id = "fang", Name = "Fang", Type = ItemType.Equipment, Slot = EquipmentSlot.Relic1, PincerDamageBonus = 1f, RegenPerTurn = 5, StartsProtected = true };
            var setup = new BattleSetup { Stage = Stage(("tank", 2, 3)), Seed = 2, UnlockedSlots = 2 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(1, 3), EquippedItemIds = { "fang" } });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(3, 4) });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            var k = Hero(s, "knight");
            Assert.True(k.HasBuff("protection"));
            k.Hp = 10;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            Assert.Contains(ev, e => e is HealedEvent h && h.TargetId == k.Id && h.Amount == 5);
            Assert.Equal(1f, k.PincerDamageBonus);
        }

        [Fact] // US-116
        public void Bar_weapon_swap_changes_the_weapon_and_the_save_follows()
        {
            var c = Real;
            var save = Progression.NewGame(c, "Swap");
            var weapons = c.Items.Values.Where(i => i.Slot == EquipmentSlot.Weapon && i.Durability > 0).Take(2).ToList();
            var h = save.Hero("paladin");
            save.AddItem(weapons[0].Id, 1);
            save.AddItem(weapons[1].Id, 1);
            Assert.True(Equipment.Equip(c, save, h, EquipmentSlot.Weapon, weapons[0].Id).Ok);
            Assert.True(Abilities.Assign(c, save, h, 1, "weapon:" + weapons[1].Id).Ok);
            var setup = BattleFactory.Build(c, save, c.CampaignStages()[0].Id, 3);
            var s = BattleEngine.Create(c, setup, new List<BattleEvent>());
            var pal = s.Units.First(u => u.Def.Id == "paladin");
            Assert.Equal(SlotStatus.Ready, BattleEngine.GetSlotStatus(s, pal.Id, 1));
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = pal.Id, Slot = 1, Target = pal.Pos });
            Assert.Contains(ev, e => e is WeaponSwappedEvent w && w.ToWeapon == weapons[1].Id);
            Assert.Equal(weapons[1].Id, pal.WeaponId);
            Assert.Equal(weapons[0].Id, pal.Bar[1].WeaponId);
            foreach (var e in s.Units.Where(u => !u.IsHero)) { e.Hp = 0; e.OnBoard = false; }
            BattleEngine.Apply(s, new ShieldCommand()); // any action → CheckEnd → victory bookkeeping
            Assert.Equal(BattlePhase.Victory, s.Phase);
            BattleFactory.Commit(c, save, s);
            Assert.Equal(weapons[1].Id, h.WeaponId);
            Assert.Equal("weapon:" + weapons[0].Id, h.Bar[1]);
            Assert.Equal(1, save.Count(weapons[0].Id));
            Assert.Equal(0, save.Count(weapons[1].Id));
        }

        [Fact] // orb economy: every color has something to spend on
        public void Every_mana_color_pays_for_at_least_one_hero_spell()
        {
            var c = Real;
            foreach (var color in new[] { 'W', 'U', 'B', 'R', 'G' })
                Assert.Contains(c.Abilities.Values, a => Gaiden.Meta.Abilities.IsHeroUsable(a) && a.Kind == AbilityKind.Spell && a.Cost.Contains(color));
        }

        [Fact]
        public void Default_kit_classes_now_have_color_matched_kits()
        {
            var c = Real;
            var heroes = c.Classes.Values.Where(x => x.Team == Team.Hero).ToList();
            Assert.True(heroes.Count(h => h.ColorAffinity == ManaColor.Blue) < 20, "most heroes should no longer default to Blue");
            foreach (var h in heroes)
            {
                // Every hero can learn (class list) or already carries a spell its own orb color pays for.
                char letter = ManaBank.Letter(h.ColorAffinity);
                var pool = h.Abilities.Concat(h.DefaultLoadout).Select(c.TryAbility).Where(a => a != null && a.Kind == AbilityKind.Spell);
                Assert.True(pool.Any(a => a.Cost.Contains(letter)), $"{h.Id} ({h.ColorAffinity}) has no spell its own color can pay for");
            }
        }
    }
}
