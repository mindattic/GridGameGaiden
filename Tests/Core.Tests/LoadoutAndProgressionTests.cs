using System.Linq;
using Gaiden.Core.Content;
using Gaiden.Meta;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>Camp-screen edge cases (GG-T31): equip/unequip, the ability bar, learning, and new-game/progression rules.</summary>
    public class LoadoutAndProgressionTests
    {
        // ── Equipment.Fits ──

        [Fact]
        public void Fits_rejects_non_equipment_and_null()
        {
            var c = Real;
            var material = c.Items.Values.First(i => i.Type == ItemType.CraftingMaterial);
            Assert.False(Equipment.Fits(material, EquipmentSlot.Weapon));
            Assert.False(Equipment.Fits(null, EquipmentSlot.Relic1));
        }

        [Fact]
        public void Fits_allows_a_relic_only_into_any_relic_slot()
        {
            var c = Real;
            var relic = c.Items.Values.First(i => i.IsRelic);
            Assert.True(Equipment.Fits(relic, EquipmentSlot.Relic1));
            Assert.True(Equipment.Fits(relic, EquipmentSlot.Relic2));
            Assert.True(Equipment.Fits(relic, EquipmentSlot.Relic3));
            Assert.False(Equipment.Fits(relic, EquipmentSlot.Weapon));
            Assert.False(Equipment.Fits(relic, EquipmentSlot.Armor));
        }

        [Fact]
        public void Fits_matches_weapons_and_armor_to_their_own_slot_only()
        {
            var c = Real;
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon);
            var armor = c.Items.Values.First(i => i.Slot == EquipmentSlot.Armor);
            Assert.True(Equipment.Fits(weapon, EquipmentSlot.Weapon));
            Assert.False(Equipment.Fits(weapon, EquipmentSlot.Armor));
            Assert.False(Equipment.Fits(weapon, EquipmentSlot.Relic1));
            Assert.True(Equipment.Fits(armor, EquipmentSlot.Armor));
            Assert.False(Equipment.Fits(armor, EquipmentSlot.Weapon));
        }

        // ── Equipment.Equip / Unequip ──

        [Fact]
        public void Equip_rejects_an_item_that_does_not_fit_the_slot()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Eq1");
            var hero = s.Hero(s.Party[0]);
            var armor = c.Items.Values.First(i => i.Slot == EquipmentSlot.Armor);
            s.AddItem(armor.Id, 1);
            var result = Equipment.Equip(c, s, hero, EquipmentSlot.Weapon, armor.Id);
            Assert.False(result.Ok);
            Assert.Null(hero.WeaponId);
            Assert.Equal(1, s.Count(armor.Id)); // nothing consumed on a rejected equip
        }

        [Fact]
        public void Equip_rejects_an_item_the_player_does_not_own()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Eq2");
            var hero = s.Hero(s.Party[0]);
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon);
            Assert.Equal(0, s.Count(weapon.Id));
            var result = Equipment.Equip(c, s, hero, EquipmentSlot.Weapon, weapon.Id);
            Assert.False(result.Ok);
            Assert.Null(hero.WeaponId);
        }

        [Fact]
        public void Unequip_an_empty_slot_fails()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Eq3");
            var hero = s.Hero(s.Party[0]);
            Assert.Null(Equipment.Equipped(hero, EquipmentSlot.Relic1));
            Assert.False(Equipment.Unequip(c, s, hero, EquipmentSlot.Relic1).Ok);
        }

        [Fact]
        public void Compare_previews_stats_before_and_after_equipping_a_candidate()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Eq4");
            var hero = s.Hero(s.Party[0]);
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon && i.Stats.Strength > 0);
            var (now, then) = Equipment.Compare(c, hero, EquipmentSlot.Weapon, weapon.Id);
            Assert.Null(hero.WeaponId); // Compare previews only; it doesn't mutate the hero
            Assert.NotEqual(now.Strength, then.Strength);
        }

        // ── Abilities.Assign ──

        [Fact]
        public void Assign_rejects_an_out_of_range_slot()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As1");
            var hero = s.Hero(s.Party[0]);
            Assert.False(Abilities.Assign(c, s, hero, -1, null).Ok);
            Assert.False(Abilities.Assign(c, s, hero, Progression.MaxBarSlots, null).Ok);
        }

        [Fact]
        public void Assign_rejects_a_slot_beyond_what_is_unlocked()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As1b");
            var hero = s.Hero(s.Party[0]);
            // Every slot ships unlocked today (owner direction 2026-10-11), so the "locked" guard and the
            // "out of range" guard currently coincide at the same boundary: Progression.UnlockedSlots(s) ==
            // Progression.MaxBarSlots for every save. This still exercises that the slot is rejected right
            // at that boundary, which is what the "locked" check guards against if thresholds ever return.
            Assert.Equal(Progression.MaxBarSlots, Progression.UnlockedSlots(s));
            Assert.False(Abilities.Assign(c, s, hero, Progression.UnlockedSlots(s), null).Ok);
        }

        [Fact]
        public void Assign_rejects_a_non_consumable_item_entry()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As2");
            var hero = s.Hero(s.Party[0]);
            var material = c.Items.Values.First(i => i.Type == ItemType.CraftingMaterial);
            var result = Abilities.Assign(c, s, hero, 0, "item:" + material.Id);
            Assert.False(result.Ok);
            Assert.Equal("taunt", hero.Bar[0]); // untouched
        }

        [Fact]
        public void Assign_rejects_a_weapon_entry_without_a_spare_in_the_bag()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As3");
            var hero = s.Hero(s.Party[0]);
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon);
            Assert.Equal(0, s.Count(weapon.Id));
            var result = Abilities.Assign(c, s, hero, 0, "weapon:" + weapon.Id);
            Assert.False(result.Ok);
        }

        [Fact]
        public void Assign_rejects_an_unlearned_ability()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As4");
            var hero = s.Hero(s.Party[0]); // paladin
            Assert.DoesNotContain("heal", hero.Known); // learnable, but not known out of the gate
            var result = Abilities.Assign(c, s, hero, 0, "heal");
            Assert.False(result.Ok);
            Assert.Equal("taunt", hero.Bar[0]); // untouched
        }

        [Fact]
        public void Assign_moves_a_duplicate_entry_instead_of_copying_it()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As5");
            var hero = s.Hero(s.Party[0]); // paladin, bar[0] == "taunt" by default loadout
            Assert.Equal("taunt", hero.Bar[0]);
            var result = Abilities.Assign(c, s, hero, 3, "taunt");
            Assert.True(result.Ok, result.Message);
            Assert.Null(hero.Bar[0]); // vacated, not duplicated
            Assert.Equal("taunt", hero.Bar[3]);
            Assert.Equal(1, hero.Bar.Count(e => e == "taunt"));
        }

        // ── Abilities.Learn ──

        [Fact]
        public void Learn_rejects_an_ability_outside_the_hero_class_and_not_a_spell()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Le1");
            var hero = s.Hero("paladin");
            Assert.DoesNotContain(c.Class("paladin").Abilities, id => id == "steal");
            var result = Abilities.Learn(c, s, hero, "steal");
            Assert.False(result.Ok);
            Assert.DoesNotContain("steal", hero.Known);
        }

        [Fact]
        public void Learn_rejects_when_gold_is_short()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Le2");
            var hero = s.Hero("paladin");
            s.Gold = 0;
            Assert.Contains(Abilities.Learnable(c, hero), a => a.Id == "heal");
            var result = Abilities.Learn(c, s, hero, "heal");
            Assert.False(result.Ok);
            Assert.DoesNotContain("heal", hero.Known);
        }

        [Fact]
        public void Learn_succeeds_and_spends_gold_on_a_learnable_ability()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Le3");
            var hero = s.Hero("paladin");
            s.Gold = 10000;
            int price = Abilities.Price(c.Ability("heal"));
            int before = s.Gold;
            Assert.True(Abilities.Learn(c, s, hero, "heal").Ok);
            Assert.Contains("heal", hero.Known);
            Assert.Equal(before - price, s.Gold);
        }

        // ── Price formula ──

        [Theory]
        [InlineData("shield_bash", 300)]   // Skill: flat 300
        [InlineData("heal", 200)]          // Spell, Cost 1: 120 + 80*1
        [InlineData("fireball", 280)]      // Spell, Cost 2: 120 + 80*2
        public void Price_follows_the_skill_flat_and_spell_cost_formula(string abilityId, int expected)
        {
            Assert.Equal(expected, Abilities.Price(Real.Ability(abilityId)));
        }

        // ── IsHeroUsable ──

        [Fact]
        public void IsHeroUsable_excludes_passives_charge_abilities_and_enrage()
        {
            var c = Real;
            Assert.False(Abilities.IsHeroUsable(c.Ability("double_attack"))); // Passive
            Assert.False(Abilities.IsHeroUsable(c.Ability("charge_fireball"))); // charge_ prefix
            Assert.False(Abilities.IsHeroUsable(c.Ability("enrage")));
            Assert.True(Abilities.IsHeroUsable(c.Ability("shield_bash")));
            Assert.True(Abilities.IsHeroUsable(c.Ability("fireball")));
        }

        // ── Abilities.Swap ──

        [Fact]
        public void Swap_rejects_the_same_slot()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Sw1");
            var hero = s.Hero(s.Party[0]);
            Assert.False(Abilities.Swap(s, hero, 2, 2).Ok);
        }

        [Fact]
        public void Swap_rejects_an_out_of_range_or_locked_slot()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Sw2");
            var hero = s.Hero(s.Party[0]);
            Assert.False(Abilities.Swap(s, hero, -1, 0).Ok);
            Assert.False(Abilities.Swap(s, hero, 0, Progression.MaxBarSlots).Ok);
        }

        [Fact]
        public void Swap_exchanges_two_unlocked_slots()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Sw3");
            var hero = s.Hero(s.Party[0]);
            var a = hero.Bar[0];
            var b = hero.Bar[1];
            Assert.True(Abilities.Swap(s, hero, 0, 1).Ok);
            Assert.Equal(b, hero.Bar[0]);
            Assert.Equal(a, hero.Bar[1]);
        }

        // ── Progression.NewGame ──

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void NewGame_falls_back_to_Lightbearer_on_a_blank_name(string name)
        {
            var s = Progression.NewGame(Real, name);
            Assert.Equal("Lightbearer", s.Name);
        }

        [Fact]
        public void NewGame_skips_unknown_class_ids_in_a_custom_party()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Custom", new[] { "paladin", "not_a_real_class", "cleric" });
            Assert.Equal(new[] { "paladin", "cleric" }, s.Party);
            Assert.Equal(2, s.Roster.Count);
        }

        // ── Progression.CanStartNewGamePlus / IsStageUnlocked ──

        [Fact]
        public void CanStartNewGamePlus_requires_the_whole_campaign_cleared()
        {
            var c = Real;
            var s = Progression.NewGame(c, "NG1");
            Assert.False(Progression.CanStartNewGamePlus(c, s));
            s.HighestClearedStage = c.CampaignStages().Count - 1;
            Assert.True(Progression.CanStartNewGamePlus(c, s));
        }

        [Fact]
        public void IsStageUnlocked_opens_only_the_next_uncleared_stage()
        {
            var c = Real;
            var s = Progression.NewGame(c, "NG2");
            Assert.True(Progression.IsStageUnlocked(s, 0));
            Assert.False(Progression.IsStageUnlocked(s, 1));
            s.HighestClearedStage = 0;
            Assert.True(Progression.IsStageUnlocked(s, 1));
            Assert.False(Progression.IsStageUnlocked(s, 2));
        }
    }
}
