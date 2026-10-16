using System.Linq;
using Gaiden.Core.Content;
using Gaiden.Meta;
using NUnit.Framework;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>Camp-screen edge cases (GG-T31): equip/unequip, the ability bar, learning, and new-game/progression rules.</summary>
    public class LoadoutAndProgressionTests
    {
        // ── Equipment.Fits ──

        [Test]
        public void Fits_rejects_non_equipment_and_null()
        {
            var c = Real;
            var material = c.Items.Values.First(i => i.Type == ItemType.CraftingMaterial);
            Assert.That(Equipment.Fits(material, EquipmentSlot.Weapon), Is.False);
            Assert.That(Equipment.Fits(null, EquipmentSlot.Relic1), Is.False);
        }

        [Test]
        public void Fits_allows_a_relic_only_into_any_relic_slot()
        {
            var c = Real;
            var relic = c.Items.Values.First(i => i.IsRelic);
            Assert.That(Equipment.Fits(relic, EquipmentSlot.Relic1), Is.True);
            Assert.That(Equipment.Fits(relic, EquipmentSlot.Relic2), Is.True);
            Assert.That(Equipment.Fits(relic, EquipmentSlot.Relic3), Is.True);
            Assert.That(Equipment.Fits(relic, EquipmentSlot.Weapon), Is.False);
            Assert.That(Equipment.Fits(relic, EquipmentSlot.Armor), Is.False);
        }

        [Test]
        public void Fits_matches_weapons_and_armor_to_their_own_slot_only()
        {
            var c = Real;
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon);
            var armor = c.Items.Values.First(i => i.Slot == EquipmentSlot.Armor);
            Assert.That(Equipment.Fits(weapon, EquipmentSlot.Weapon), Is.True);
            Assert.That(Equipment.Fits(weapon, EquipmentSlot.Armor), Is.False);
            Assert.That(Equipment.Fits(weapon, EquipmentSlot.Relic1), Is.False);
            Assert.That(Equipment.Fits(armor, EquipmentSlot.Armor), Is.True);
            Assert.That(Equipment.Fits(armor, EquipmentSlot.Weapon), Is.False);
        }

        // ── Equipment.Equip / Unequip ──

        [Test]
        public void Equip_rejects_an_item_that_does_not_fit_the_slot()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Eq1");
            var hero = s.Hero(s.Party[0]);
            var armor = c.Items.Values.First(i => i.Slot == EquipmentSlot.Armor);
            s.AddItem(armor.Id, 1);
            var result = Equipment.Equip(c, s, hero, EquipmentSlot.Weapon, armor.Id);
            Assert.That(result.Ok, Is.False);
            Assert.That(hero.WeaponId, Is.Null);
            Assert.That(s.Count(armor.Id), Is.EqualTo(1)); // nothing consumed on a rejected equip
        }

        [Test]
        public void Equip_rejects_an_item_the_player_does_not_own()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Eq2");
            var hero = s.Hero(s.Party[0]);
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon);
            Assert.That(s.Count(weapon.Id), Is.EqualTo(0));
            var result = Equipment.Equip(c, s, hero, EquipmentSlot.Weapon, weapon.Id);
            Assert.That(result.Ok, Is.False);
            Assert.That(hero.WeaponId, Is.Null);
        }

        [Test]
        public void Unequip_an_empty_slot_fails()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Eq3");
            var hero = s.Hero(s.Party[0]);
            Assert.That(Equipment.Equipped(hero, EquipmentSlot.Relic1), Is.Null);
            Assert.That(Equipment.Unequip(c, s, hero, EquipmentSlot.Relic1).Ok, Is.False);
        }

        [Test]
        public void Compare_previews_stats_before_and_after_equipping_a_candidate()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Eq4");
            var hero = s.Hero(s.Party[0]);
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon && i.Stats.Strength > 0);
            var (now, then) = Equipment.Compare(c, hero, EquipmentSlot.Weapon, weapon.Id);
            Assert.That(hero.WeaponId, Is.Null); // Compare previews only; it doesn't mutate the hero
            Assert.That(then.Strength, Is.Not.EqualTo(now.Strength));
        }

        // ── Abilities.Assign ──

        [Test]
        public void Assign_rejects_an_out_of_range_slot()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As1");
            var hero = s.Hero(s.Party[0]);
            Assert.That(Abilities.Assign(c, s, hero, -1, null).Ok, Is.False);
            Assert.That(Abilities.Assign(c, s, hero, Progression.MaxBarSlots, null).Ok, Is.False);
        }

        [Test]
        public void Assign_rejects_a_slot_beyond_what_is_unlocked()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As1b");
            var hero = s.Hero(s.Party[0]);
            // Every slot ships unlocked today (owner direction 2026-10-11), so the "locked" guard and the
            // "out of range" guard currently coincide at the same boundary: Progression.UnlockedSlots(s) ==
            // Progression.MaxBarSlots for every save. This still exercises that the slot is rejected right
            // at that boundary, which is what the "locked" check guards against if thresholds ever return.
            Assert.That(Progression.UnlockedSlots(s), Is.EqualTo(Progression.MaxBarSlots));
            Assert.That(Abilities.Assign(c, s, hero, Progression.UnlockedSlots(s), null).Ok, Is.False);
        }

        [Test]
        public void Assign_rejects_a_non_consumable_item_entry()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As2");
            var hero = s.Hero(s.Party[0]);
            var material = c.Items.Values.First(i => i.Type == ItemType.CraftingMaterial);
            var result = Abilities.Assign(c, s, hero, 0, "item:" + material.Id);
            Assert.That(result.Ok, Is.False);
            Assert.That(hero.Bar[0], Is.EqualTo("taunt")); // untouched
        }

        [Test]
        public void Assign_rejects_a_weapon_entry_without_a_spare_in_the_bag()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As3");
            var hero = s.Hero(s.Party[0]);
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon);
            Assert.That(s.Count(weapon.Id), Is.EqualTo(0));
            var result = Abilities.Assign(c, s, hero, 0, "weapon:" + weapon.Id);
            Assert.That(result.Ok, Is.False);
        }

        [Test]
        public void Assign_rejects_an_unlearned_ability()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As4");
            var hero = s.Hero(s.Party[0]); // paladin
            Assert.That(hero.Known, Does.Not.Contain("heal")); // learnable, but not known out of the gate
            var result = Abilities.Assign(c, s, hero, 0, "heal");
            Assert.That(result.Ok, Is.False);
            Assert.That(hero.Bar[0], Is.EqualTo("taunt")); // untouched
        }

        [Test]
        public void Assign_moves_a_duplicate_entry_instead_of_copying_it()
        {
            var c = Real;
            var s = Progression.NewGame(c, "As5");
            var hero = s.Hero(s.Party[0]); // paladin, bar[0] == "taunt" by default loadout
            Assert.That(hero.Bar[0], Is.EqualTo("taunt"));
            var result = Abilities.Assign(c, s, hero, 3, "taunt");
            Assert.That(result.Ok, Is.True, result.Message);
            Assert.That(hero.Bar[0], Is.Null); // vacated, not duplicated
            Assert.That(hero.Bar[3], Is.EqualTo("taunt"));
            Assert.That(hero.Bar.Count(e => e == "taunt"), Is.EqualTo(1));
        }

        // ── Abilities.Learn ──

        [Test]
        public void Learn_rejects_an_ability_outside_the_hero_class_and_not_a_spell()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Le1");
            var hero = s.Hero("paladin");
            Assert.That(c.Class("paladin").Abilities.Any(id => id == "steal"), Is.False);
            var result = Abilities.Learn(c, s, hero, "steal");
            Assert.That(result.Ok, Is.False);
            Assert.That(hero.Known, Does.Not.Contain("steal"));
        }

        [Test]
        public void Learn_rejects_when_gold_is_short()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Le2");
            var hero = s.Hero("paladin");
            s.Gold = 0;
            Assert.That(Abilities.Learnable(c, hero).Any(a => a.Id == "heal"), Is.True);
            var result = Abilities.Learn(c, s, hero, "heal");
            Assert.That(result.Ok, Is.False);
            Assert.That(hero.Known, Does.Not.Contain("heal"));
        }

        [Test]
        public void Learn_succeeds_and_spends_gold_on_a_learnable_ability()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Le3");
            var hero = s.Hero("paladin");
            s.Gold = 10000;
            int price = Abilities.Price(c.Ability("heal"));
            int before = s.Gold;
            Assert.That(Abilities.Learn(c, s, hero, "heal").Ok, Is.True);
            Assert.That(hero.Known, Does.Contain("heal"));
            Assert.That(s.Gold, Is.EqualTo(before - price));
        }

        // ── Price formula ──
        [TestCase("shield_bash", 300)]   // Skill: flat 300
        [TestCase("heal", 200)]          // Spell, Cost 1: 120 + 80*1
        [TestCase("fireball", 280)]      // Spell, Cost 2: 120 + 80*2
        public void Price_follows_the_skill_flat_and_spell_cost_formula(string abilityId, int expected)
        {
            Assert.That(Abilities.Price(Real.Ability(abilityId)), Is.EqualTo(expected));
        }

        // ── IsHeroUsable ──

        [Test]
        public void IsHeroUsable_excludes_passives_charge_abilities_and_enrage()
        {
            var c = Real;
            Assert.That(Abilities.IsHeroUsable(c.Ability("double_attack")), Is.False); // Passive
            Assert.That(Abilities.IsHeroUsable(c.Ability("charge_fireball")), Is.False); // charge_ prefix
            Assert.That(Abilities.IsHeroUsable(c.Ability("enrage")), Is.False);
            Assert.That(Abilities.IsHeroUsable(c.Ability("shield_bash")), Is.True);
            Assert.That(Abilities.IsHeroUsable(c.Ability("fireball")), Is.True);
        }

        // ── Abilities.Swap ──

        [Test]
        public void Swap_rejects_the_same_slot()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Sw1");
            var hero = s.Hero(s.Party[0]);
            Assert.That(Abilities.Swap(s, hero, 2, 2).Ok, Is.False);
        }

        [Test]
        public void Swap_rejects_an_out_of_range_or_locked_slot()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Sw2");
            var hero = s.Hero(s.Party[0]);
            Assert.That(Abilities.Swap(s, hero, -1, 0).Ok, Is.False);
            Assert.That(Abilities.Swap(s, hero, 0, Progression.MaxBarSlots).Ok, Is.False);
        }

        [Test]
        public void Swap_exchanges_two_unlocked_slots()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Sw3");
            var hero = s.Hero(s.Party[0]);
            var a = hero.Bar[0];
            var b = hero.Bar[1];
            Assert.That(Abilities.Swap(s, hero, 0, 1).Ok, Is.True);
            Assert.That(hero.Bar[0], Is.EqualTo(b));
            Assert.That(hero.Bar[1], Is.EqualTo(a));
        }

        // ── Progression.NewGame ──
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void NewGame_falls_back_to_Lightbearer_on_a_blank_name(string name)
        {
            var s = Progression.NewGame(Real, name);
            Assert.That(s.Name, Is.EqualTo("Lightbearer"));
        }

        [Test]
        public void NewGame_skips_unknown_class_ids_in_a_custom_party()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Custom", new[] { "paladin", "not_a_real_class", "cleric" });
            Assert.That(s.Party, Is.EqualTo(new[] { "paladin", "cleric" }));
            Assert.That(s.Roster.Count, Is.EqualTo(2));
        }

        // ── Progression.CanStartNewGamePlus / IsStageUnlocked ──

        [Test]
        public void CanStartNewGamePlus_requires_the_whole_campaign_cleared()
        {
            var c = Real;
            var s = Progression.NewGame(c, "NG1");
            Assert.That(Progression.CanStartNewGamePlus(c, s), Is.False);
            s.HighestClearedStage = c.CampaignStages().Count - 1;
            Assert.That(Progression.CanStartNewGamePlus(c, s), Is.True);
        }

        [Test]
        public void IsStageUnlocked_opens_only_the_next_uncleared_stage()
        {
            var c = Real;
            var s = Progression.NewGame(c, "NG2");
            Assert.That(Progression.IsStageUnlocked(s, 0), Is.True);
            Assert.That(Progression.IsStageUnlocked(s, 1), Is.False);
            s.HighestClearedStage = 0;
            Assert.That(Progression.IsStageUnlocked(s, 1), Is.True);
            Assert.That(Progression.IsStageUnlocked(s, 2), Is.False);
        }
    }
}
