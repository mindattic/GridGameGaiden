using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Meta;
using Xunit;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>Covers BattleFactory.Commit/Build branches, Expeditions.AfterBattle and CampRoutine.Run
    /// that MetaLoopTests' end-to-end flows don't happen to exercise (GG-T32).</summary>
    public class MetaCommitBuildTests
    {
        private static BattleState DirectWin(ContentDb c, SaveData s, string stageId, int seed = 5)
        {
            var st = BattleEngine.Create(c, BattleFactory.Build(c, s, stageId, seed), new List<BattleEvent>());
            st.Phase = BattlePhase.Victory;
            return st;
        }

        private static BattleState FightWithCoins(ContentDb c, SaveData s, string stageId, int coins)
        {
            Expeditions.Begin(c, s, stageId);
            var st = DirectWin(c, s, stageId, 7);
            st.Results.Coins = coins;
            return st;
        }

        // ── Commit: equipment ──

        [Fact]
        public void Victory_with_a_final_weapon_swap_returns_the_old_one_and_removes_the_new_one()
        {
            var c = Real; var s = Progression.NewGame(c, "FW");
            var hero = s.Hero(s.Party[0]);
            var weapons = c.Items.Values.Where(i => i.Slot == EquipmentSlot.Weapon && i.Type == ItemType.Equipment).Take(2).ToList();
            var oldWeapon = weapons[0];
            var newWeapon = weapons[1];
            hero.WeaponId = oldWeapon.Id;
            hero.WeaponDurability = oldWeapon.Durability;
            s.AddItem(newWeapon.Id, 1);

            var st = DirectWin(c, s, c.CampaignStages()[0].Id);
            st.Results.FinalWeapon[hero.ClassId] = newWeapon.Id;
            BattleFactory.Commit(c, s, st);

            Assert.Equal(newWeapon.Id, hero.WeaponId);
            Assert.Equal(0, hero.WeaponRepairs);
            Assert.Equal(1, s.Count(oldWeapon.Id)); // the old weapon goes back to the bag
            Assert.Equal(0, s.Count(newWeapon.Id)); // the new one leaves it
        }

        [Fact]
        public void Negative_weapon_durability_on_a_broken_unit_clears_the_slot()
        {
            var c = Real; var s = Progression.NewGame(c, "WD");
            var hero = s.Hero(s.Party[0]);
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon && i.Durability > 0);
            hero.WeaponId = weapon.Id;
            hero.WeaponDurability = weapon.Durability;

            var st = DirectWin(c, s, c.CampaignStages()[0].Id);
            var unit = st.Units.First(u => u.IsHero && u.Def.Id == hero.ClassId);
            unit.WeaponId = null; // shattered mid-battle
            st.Results.WeaponDurability[hero.ClassId] = -1;
            BattleFactory.Commit(c, s, st);

            Assert.Null(hero.WeaponId);
            Assert.Equal(0, hero.WeaponDurability);
            Assert.Equal(0, hero.WeaponRepairs);
        }

        [Fact]
        public void Negative_armor_durability_on_a_broken_unit_clears_the_slot()
        {
            var c = Real; var s = Progression.NewGame(c, "AD");
            var hero = s.Hero(s.Party[0]);
            var armor = c.Items.Values.First(i => i.Slot == EquipmentSlot.Armor && i.Durability > 0);
            hero.ArmorId = armor.Id;
            hero.ArmorDurability = armor.Durability;

            var st = DirectWin(c, s, c.CampaignStages()[0].Id);
            var unit = st.Units.First(u => u.IsHero && u.Def.Id == hero.ClassId);
            unit.ArmorId = null; // broke mid-battle
            st.Results.ArmorDurability[hero.ClassId] = -1;
            BattleFactory.Commit(c, s, st);

            Assert.Null(hero.ArmorId);
            Assert.Equal(0, hero.ArmorDurability);
            Assert.Equal(0, hero.ArmorRepairs);
        }

        [Fact] // GG-T41
        public void A_shattered_weapon_becomes_a_broken_fragment_instead_of_vanishing()
        {
            var c = Real; var s = Progression.NewGame(c, "BW");
            var hero = s.Hero(s.Party[0]);
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon && i.Durability > 0);
            hero.WeaponId = weapon.Id;
            hero.WeaponDurability = weapon.Durability;

            var st = DirectWin(c, s, c.CampaignStages()[0].Id);
            var unit = st.Units.First(u => u.IsHero && u.Def.Id == hero.ClassId);
            unit.WeaponId = null; // shattered mid-battle
            st.Results.WeaponDurability[hero.ClassId] = -1;
            st.Results.BrokenWeapons[hero.ClassId] = new List<string> { weapon.Id };
            BattleFactory.Commit(c, s, st);

            Assert.Null(hero.WeaponId);
            Assert.Equal(0, s.Count(weapon.Id)); // not a whole item back in the bag
            Assert.Equal(1, s.BrokenCount(weapon.Id));
        }

        [Fact] // GG-T41
        public void A_shattered_armor_becomes_a_broken_fragment_instead_of_vanishing()
        {
            var c = Real; var s = Progression.NewGame(c, "BA");
            var hero = s.Hero(s.Party[0]);
            var armor = c.Items.Values.First(i => i.Slot == EquipmentSlot.Armor && i.Durability > 0);
            hero.ArmorId = armor.Id;
            hero.ArmorDurability = armor.Durability;

            var st = DirectWin(c, s, c.CampaignStages()[0].Id);
            var unit = st.Units.First(u => u.IsHero && u.Def.Id == hero.ClassId);
            unit.ArmorId = null; // broke mid-battle
            st.Results.ArmorDurability[hero.ClassId] = -1;
            st.Results.BrokenArmors[hero.ClassId] = new List<string> { armor.Id };
            BattleFactory.Commit(c, s, st);

            Assert.Null(hero.ArmorId);
            Assert.Equal(0, s.Count(armor.Id));
            Assert.Equal(1, s.BrokenCount(armor.Id));
        }

        [Fact] // GG-T41
        public void A_weapon_that_shattered_and_had_a_bar_backup_auto_equipped_does_not_also_return_whole_to_the_bag()
        {
            var c = Real; var s = Progression.NewGame(c, "SW");
            var hero = s.Hero(s.Party[0]);
            var weapons = c.Items.Values.Where(i => i.Slot == EquipmentSlot.Weapon && i.Type == ItemType.Equipment && i.Durability > 0).Take(2).ToList();
            var oldWeapon = weapons[0];
            var backup = weapons[1];
            hero.WeaponId = oldWeapon.Id;
            hero.WeaponDurability = oldWeapon.Durability;
            s.AddItem(backup.Id, 1);

            var st = DirectWin(c, s, c.CampaignStages()[0].Id);
            // oldWeapon shattered mid-battle and the bar backup auto-equipped in its place.
            st.Results.BrokenWeapons[hero.ClassId] = new List<string> { oldWeapon.Id };
            st.Results.FinalWeapon[hero.ClassId] = backup.Id;
            st.Results.WeaponDurability[hero.ClassId] = backup.Durability;
            BattleFactory.Commit(c, s, st);

            Assert.Equal(backup.Id, hero.WeaponId);
            Assert.Equal(0, s.Count(oldWeapon.Id)); // the shattered one never returns to the bag whole
            Assert.Equal(1, s.BrokenCount(oldWeapon.Id));
            Assert.Equal(0, s.Count(backup.Id)); // the backup left the bag onto the hero
        }

        [Fact] // GG-T41
        public void Broken_fragments_cannot_be_equipped_and_salvaging_one_grants_materials_and_blacksmith_xp()
        {
            var c = Real; var s = Progression.NewGame(c, "SB2");
            var weapon = c.Items.Values.First(i => i.Slot == EquipmentSlot.Weapon && i.Durability > 0);
            s.AddBroken(weapon.Id, 1);

            // A Broken fragment never shows up as an equip candidate — it was never added to Inventory.
            var hero = s.Hero(s.Party[0]);
            Assert.Equal(0, s.Count(weapon.Id));
            Assert.DoesNotContain(weapon, Equipment.Candidates(c, s, EquipmentSlot.Weapon));

            var res = Crafting.SalvageBroken(c, s, weapon.Id);
            Assert.True(res.Ok, res.Message);
            Assert.Equal(0, s.BrokenCount(weapon.Id));
            Assert.Equal(Crafting.SalvageXp, StationProgress.Xp(s, StationKind.Blacksmith));

            Assert.False(Crafting.SalvageBroken(c, s, weapon.Id).Ok); // nothing left to salvage
        }

        // ── Commit: XP, HP, progression ──

        [Fact]
        public void A_level_up_refreshes_hp_to_full_while_a_fallen_hero_revives_at_1_hp()
        {
            var c = Real; var s = Progression.NewGame(c, "LV");
            var leveler = s.Hero(s.Party[0]);
            var fallen = s.Hero(s.Party[1]);
            leveler.TotalXp = 55; // 5 short of levelling at 60 (Formulas.XpToNext(1))
            fallen.TotalXp = 0;

            var st = DirectWin(c, s, c.CampaignStages()[0].Id);
            st.Results.Xp = 10; // pushes the leveler over, not the other
            var fallenUnit = st.Units.First(u => u.IsHero && u.Def.Id == fallen.ClassId);
            fallenUnit.Hp = 0;
            BattleFactory.Commit(c, s, st);

            Assert.Equal(2, Progression.Level(leveler));
            Assert.Equal(0, leveler.Hp); // 0 = full
            Assert.Equal(1, Progression.Level(fallen));
            Assert.Equal(1, fallen.Hp);
        }

        [Fact]
        public void FirstClear_is_true_only_on_the_first_victory_of_a_stage()
        {
            var c = Real; var s = Progression.NewGame(c, "FC");
            var stageId = c.CampaignStages()[0].Id;
            var first = BattleFactory.Commit(c, s, DirectWin(c, s, stageId));
            var second = BattleFactory.Commit(c, s, DirectWin(c, s, stageId));
            Assert.True(first.FirstClear);
            Assert.False(second.FirstClear);
        }

        [Fact]
        public void Replaying_an_earlier_stage_never_lowers_HighestClearedStage()
        {
            var c = Real; var s = Progression.NewGame(c, "HC");
            var stages = c.CampaignStages();
            BattleFactory.Commit(c, s, DirectWin(c, s, stages[2].Id));
            Assert.Equal(2, s.HighestClearedStage);
            BattleFactory.Commit(c, s, DirectWin(c, s, stages[0].Id));
            Assert.Equal(2, s.HighestClearedStage);
        }

        [Fact]
        public void CampaignComplete_is_true_only_on_the_last_stage()
        {
            var c = Real; var s = Progression.NewGame(c, "CC");
            var stages = c.CampaignStages();
            var early = BattleFactory.Commit(c, s, DirectWin(c, s, stages[0].Id));
            var last = BattleFactory.Commit(c, s, DirectWin(c, s, stages[stages.Count - 1].Id));
            Assert.False(early.CampaignComplete);
            Assert.True(last.CampaignComplete);
        }

        [Fact]
        public void Commit_reports_the_newly_unlocked_stages_name_once()
        {
            var c = Real; var s = Progression.NewGame(c, "US");
            var stages = c.CampaignStages();
            var first = BattleFactory.Commit(c, s, DirectWin(c, s, stages[0].Id));
            var replay = BattleFactory.Commit(c, s, DirectWin(c, s, stages[0].Id));
            Assert.Equal(stages[1].Name, first.UnlockedStage);
            Assert.Null(replay.UnlockedStage);
        }

        // ── Build ──

        [Fact]
        public void Build_excludes_roster_and_lost_guests_from_appearing_as_guests()
        {
            var c = Real; var s = Progression.NewGame(c, "EX");
            s.LostGuests.Add("drifter");
            var setup = BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 1);
            foreach (var h in s.Roster) Assert.Contains(h.ClassId, setup.ExcludedGuests);
            Assert.Contains("drifter", setup.ExcludedGuests);
        }

        [Fact]
        public void Build_honours_a_stages_level_override()
        {
            var c = Real; var s = Progression.NewGame(c, "LO");
            var overridden = c.CampaignStages().First(x => x.LevelOverride.HasValue);
            var setup = BattleFactory.Build(c, s, overridden.Id, 1);
            Assert.Equal(overridden.LevelOverride.Value, setup.RecommendedLevel);
        }

        [Fact]
        public void Build_adds_mana_casks_to_start_orbs()
        {
            var c = Real; var s = Progression.NewGame(c, "MC");
            s.Gold = 10000;
            var stageId = c.CampaignStages()[0].Id;
            int before = BattleFactory.Build(c, s, stageId, 1).StartOrbs;
            Assert.True(CaravanUpgrades.Buy(c, s, "mana_casks").Ok);
            Assert.Equal(before + 1, BattleFactory.Build(c, s, stageId, 1).StartOrbs);
        }

        // ── Expeditions ──

        [Fact]
        public void AfterBattle_bonus_includes_the_strongbox_upgrade()
        {
            var c = Real; var s = Progression.NewGame(c, "SB");
            s.Gold = 10000;
            Assert.True(CaravanUpgrades.Buy(c, s, "strongbox").Ok);
            var theme = c.Campaign.Themes[0];
            BattleReport last = null;
            int totalCoins = 0;
            foreach (var id in theme.Stages)
            {
                last = BattleFactory.Commit(c, s, FightWithCoins(c, s, id, 100));
                totalCoins += 100;
            }
            Assert.True(last.ExpeditionComplete);
            int expected = (int)System.Math.Round(totalCoins * (Expeditions.CompletionBonus + CaravanUpgrades.Value(c, s, "strongbox")));
            Assert.Equal(expected, last.ExpeditionBonus);
        }

        [Fact]
        public void StartOrbs_is_zero_for_a_stage_outside_the_active_expeditions_theme()
        {
            var c = Real; var s = Progression.NewGame(c, "OT");
            var theme0 = c.Campaign.Themes[0];
            var theme1 = c.Campaign.Themes[1];
            Expeditions.Begin(c, s, theme0.Stages[0]);
            s.Expedition.CarriedOrbs = 5;
            Assert.Equal(0, Expeditions.StartOrbs(c, s, theme1.Stages[0]));
        }

        [Fact]
        public void Abandon_fails_when_no_expedition_is_active()
        {
            var c = Real; var s = Progression.NewGame(c, "AB");
            Assert.False(Expeditions.Abandon(s).Ok);
        }

        // ── CampRoutine ──

        [Fact]
        public void CampRoutine_never_spends_below_zero_gold()
        {
            var c = Real; var s = Progression.NewGame(c, "CR");
            s.Gold = 50;
            for (int i = 0; i < 20; i++)
            {
                CampRoutine.Run(c, s);
                Assert.True(s.Gold >= 0);
                s.Gold += 37; // trickle gold in between runs to reach different affordability thresholds
            }
        }
    }
}
