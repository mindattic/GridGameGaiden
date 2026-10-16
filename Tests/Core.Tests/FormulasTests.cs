using System.Collections.Generic;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;
using Xunit;

namespace Gaiden.Tests
{
    public class FormulasTests
    {
        private static Unit MakeUnit(Team team, int level, Stats stats, int xpReward = 10)
        {
            var def = new ClassDef { Id = "test", Name = "Test", Team = team, XpReward = xpReward };
            return new Unit { Def = def, Team = team, Level = level, LevelStats = stats };
        }

        [Theory]
        [InlineData(1000f, 1000f, 95f)]
        [InlineData(-1000f, -1000f, 5f)]
        public void HitPercent_clamps_to_5_95_at_extreme_speed_and_luck(float speed, float luck, float expected)
        {
            Assert.Equal(expected, Formulas.HitPercent(new Stats { Speed = speed, Luck = luck }));
        }

        [Fact]
        public void HitPercent_matches_formula_in_unclamped_range()
        {
            Assert.Equal(86f, Formulas.HitPercent(new Stats { Speed = 10f, Luck = 5f }), 3);
        }

        [Theory]
        [InlineData(1000f, 50f)]
        [InlineData(-1000f, 0f)]
        public void CritPercent_clamps_to_0_50_at_extreme_luck(float luck, float expected)
        {
            Assert.Equal(expected, Formulas.CritPercent(new Stats { Luck = luck }));
        }

        [Fact]
        public void CritPercent_matches_formula_in_unclamped_range()
        {
            Assert.Equal(12f, Formulas.CritPercent(new Stats { Luck = 10f }), 3);
        }

        [Theory]
        [InlineData(10f, 1, 63)]
        [InlineData(10.7f, 3, 68)]
        [InlineData(0f, 0, 12)]
        public void MaxHp_matches_formula_and_floors_fractional_result(float vitality, int level, int expected)
        {
            Assert.Equal(expected, Formulas.MaxHp(new Stats { Vitality = vitality }, level));
        }

        [Theory]
        [InlineData(0f, 1)]
        [InlineData(-5f, 1)]
        [InlineData(0.01f, 1)]
        [InlineData(1.5f, 15)]
        public void SecondsToTicks_never_returns_below_one(float seconds, int expected)
        {
            Assert.Equal(expected, Formulas.SecondsToTicks(seconds));
        }

        [Fact]
        public void CastTime_returns_zero_when_base_is_non_positive()
        {
            var s = new Stats();
            Assert.Equal(0f, Formulas.CastTime(0f, s));
            Assert.Equal(0f, Formulas.CastTime(-1f, s));
        }

        [Fact]
        public void CastTime_with_zero_skill_equals_base_seconds()
        {
            Assert.Equal(4f, Formulas.CastTime(4f, new Stats()), 3);
        }

        [Fact]
        public void CastTime_scales_down_with_wisdom_and_intelligence_before_the_floor()
        {
            var s = new Stats { Wisdom = 20f, Intelligence = 30f }; // skill=50, scale=50/(50+25)
            Assert.Equal(4f * (50f / 75f), Formulas.CastTime(4f, s), 3);
        }

        [Fact]
        public void CastTime_floors_at_quarter_of_base_for_very_high_speed_stats()
        {
            var s = new Stats { Wisdom = 10000f, Intelligence = 10000f };
            Assert.Equal(1f, Formulas.CastTime(4f, s), 3); // 0.25 * 4
        }

        [Theory]
        [InlineData(1, 60)]
        [InlineData(2, 90)]
        [InlineData(5, 300)]
        public void XpToNext_matches_formula(int level, int expected)
        {
            Assert.Equal(expected, Formulas.XpToNext(level));
        }

        [Fact]
        public void LevelFromTotalXp_at_zero_xp_is_level_one_with_no_progress()
        {
            var (level, into) = Formulas.LevelFromTotalXp(0);
            Assert.Equal(1, level);
            Assert.Equal(0, into);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(10)]
        public void XpRoundTrips_exactly_at_each_level_boundary(int level)
        {
            int totalAtLevel = Formulas.TotalXpForLevel(level);
            var (atLevel, atInto) = Formulas.LevelFromTotalXp(totalAtLevel);
            Assert.Equal(level, atLevel);
            Assert.Equal(0, atInto);

            if (totalAtLevel > 0)
            {
                var (belowLevel, belowInto) = Formulas.LevelFromTotalXp(totalAtLevel - 1);
                Assert.Equal(level - 1, belowLevel);
                Assert.Equal(Formulas.XpToNext(level - 1) - 1, belowInto);
            }
        }

        [Fact]
        public void XpReward_scales_with_def_level_and_stats()
        {
            var stats = new Stats { Strength = 8f, Vitality = 12f, Intelligence = 5f, Wisdom = 5f };
            var enemy = MakeUnit(Team.Enemy, 4, stats, xpReward: 10);
            // phys=20, magic=10; reward=10+4*2+20*0.10+10*0.08=20.8 -> round(21)
            Assert.Equal(21, Formulas.XpReward(enemy));
        }

        [Fact]
        public void XpReward_never_returns_below_one()
        {
            var enemy = MakeUnit(Team.Enemy, 0, new Stats(), xpReward: 0);
            Assert.Equal(1, Formulas.XpReward(enemy));
        }

        [Fact]
        public void HealPower_returns_one_for_non_hero_or_null_caster()
        {
            var enemy = MakeUnit(Team.Enemy, 1, new Stats { Wisdom = 50f });
            Assert.Equal(1f, Formulas.HealPower(enemy));
            Assert.Equal(1f, Formulas.HealPower(null));
        }

        [Theory]
        [InlineData(12f, 1f)]
        [InlineData(1000f, 1.6f)]
        [InlineData(-1000f, 0.7f)]
        public void HealPower_scales_with_wisdom_and_clamps_to_caster_power_bounds(float wisdom, float expected)
        {
            var hero = MakeUnit(Team.Hero, 1, new Stats { Wisdom = wisdom });
            Assert.Equal(expected, Formulas.HealPower(hero), 3);
        }

        [Fact]
        public void CasterPower_returns_one_for_null_ability_or_non_hero_caster()
        {
            var hero = MakeUnit(Team.Hero, 1, new Stats { Strength = 50f });
            Assert.Equal(1f, Formulas.CasterPower(null, hero));

            var enemy = MakeUnit(Team.Enemy, 1, new Stats { Strength = 50f });
            Assert.Equal(1f, Formulas.CasterPower(new AbilityDef { DamageType = DamageType.Physical }, enemy));
        }

        [Fact]
        public void CasterPower_uses_strength_for_physical_and_intelligence_for_other_types()
        {
            var hero = MakeUnit(Team.Hero, 1, new Stats { Strength = 22f, Intelligence = 9f });
            var physical = new AbilityDef { DamageType = DamageType.Physical };
            var magic = new AbilityDef { DamageType = DamageType.Fire };
            Assert.Equal(1f + (22f - 12f) * 0.035f, Formulas.CasterPower(physical, hero), 3);
            Assert.Equal(1f + (9f - 12f) * 0.035f, Formulas.CasterPower(magic, hero), 3);
        }

        [Fact]
        public void Magic_damage_is_zero_when_target_is_immune()
        {
            var caster = MakeUnit(Team.Enemy, 5, new Stats { Intelligence = 50f, Speed = 50f, Luck = 50f });
            var target = MakeUnit(Team.Hero, 5, new Stats { Wisdom = 10f });
            target.Def.Resistances[DamageType.Fire] = 0f;

            for (int seed = 1; seed <= 20; seed++)
            {
                var roll = Formulas.Magic(caster, target, DamageType.Fire, new Rng(seed));
                Assert.Equal(0, roll.Damage);
            }
        }

        [Fact]
        public void Magic_damage_is_zero_on_miss()
        {
            // Speed/Luck deep negative floors hit chance at 5%, so misses dominate the sample.
            var caster = MakeUnit(Team.Enemy, 1, new Stats { Intelligence = 50f, Speed = -1000f, Luck = -1000f });
            var target = MakeUnit(Team.Hero, 1, new Stats { Wisdom = 10f });
            bool sawMiss = false;

            for (int seed = 1; seed <= 30; seed++)
            {
                var roll = Formulas.Magic(caster, target, DamageType.Physical, new Rng(seed));
                if (roll.Hit == HitKind.Miss)
                {
                    sawMiss = true;
                    Assert.Equal(0, roll.Damage);
                }
            }

            Assert.True(sawMiss, "Expected at least one miss across the seed sample (hit chance floors at 5%)");
        }

        [Fact]
        public void Magic_damage_is_at_least_one_when_it_lands_even_against_huge_mitigation()
        {
            // Speed/Luck deep positive floors hit chance at 95%; low caster INT vs huge target WIS drives
            // Mitigate's offense-floor branch down near zero, so this also exercises the hard Max(1, ...) floor.
            var caster = MakeUnit(Team.Enemy, 1, new Stats { Intelligence = 2f, Speed = 1000f, Luck = 1000f });
            var target = MakeUnit(Team.Hero, 1, new Stats { Wisdom = 1000f });
            bool sawHit = false;

            for (int seed = 1; seed <= 30; seed++)
            {
                var roll = Formulas.Magic(caster, target, DamageType.Physical, new Rng(seed));
                if (roll.Hit == HitKind.Miss) { Assert.Equal(0, roll.Damage); continue; }
                sawHit = true;
                Assert.True(roll.Damage >= 1);
            }

            Assert.True(sawHit, "Expected at least one hit across the seed sample (hit chance floors at 95%)");
        }
    }
}
