using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;
using NUnit.Framework;

namespace Gaiden.Tests
{
    public class FormulasTests
    {
        private static Unit MakeUnit(Team team, int level, Stats stats, int xpReward = 10)
        {
            var def = new ClassDef { Id = "test", Name = "Test", Team = team, XpReward = xpReward };
            return new Unit { Def = def, Team = team, Level = level, LevelStats = stats };
        }
        [TestCase(1000f, 1000f, 95f)]
        [TestCase(-1000f, -1000f, 5f)]
        public void HitPercent_clamps_to_5_95_at_extreme_speed_and_luck(float speed, float luck, float expected)
        {
            Assert.That(Formulas.HitPercent(new Stats { Speed = speed, Luck = luck }), Is.EqualTo(expected));
        }

        [Test]
        public void HitPercent_matches_formula_in_unclamped_range()
        {
            Assert.That(Formulas.HitPercent(new Stats { Speed = 10f, Luck = 5f }), Is.EqualTo(86f).Within(1e-3));
        }
        [TestCase(1000f, 50f)]
        [TestCase(-1000f, 0f)]
        public void CritPercent_clamps_to_0_50_at_extreme_luck(float luck, float expected)
        {
            Assert.That(Formulas.CritPercent(new Stats { Luck = luck }), Is.EqualTo(expected));
        }

        [Test]
        public void CritPercent_matches_formula_in_unclamped_range()
        {
            Assert.That(Formulas.CritPercent(new Stats { Luck = 10f }), Is.EqualTo(12f).Within(1e-3));
        }
        [TestCase(10f, 1, 63)]
        [TestCase(10.7f, 3, 68)]
        [TestCase(0f, 0, 12)]
        public void MaxHp_matches_formula_and_floors_fractional_result(float vitality, int level, int expected)
        {
            Assert.That(Formulas.MaxHp(new Stats { Vitality = vitality }, level), Is.EqualTo(expected));
        }
        [TestCase(0f, 1)]
        [TestCase(-5f, 1)]
        [TestCase(0.01f, 1)]
        [TestCase(1.5f, 15)]
        public void SecondsToTicks_never_returns_below_one(float seconds, int expected)
        {
            Assert.That(Formulas.SecondsToTicks(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void CastTime_returns_zero_when_base_is_non_positive()
        {
            var s = new Stats();
            Assert.That(Formulas.CastTime(0f, s), Is.EqualTo(0f));
            Assert.That(Formulas.CastTime(-1f, s), Is.EqualTo(0f));
        }

        [Test]
        public void CastTime_with_zero_skill_equals_base_seconds()
        {
            Assert.That(Formulas.CastTime(4f, new Stats()), Is.EqualTo(4f).Within(1e-3));
        }

        [Test]
        public void CastTime_scales_down_with_wisdom_and_intelligence_before_the_floor()
        {
            var s = new Stats { Wisdom = 20f, Intelligence = 30f }; // skill=50, scale=50/(50+25)
            Assert.That(Formulas.CastTime(4f, s), Is.EqualTo(4f * (50f / 75f)).Within(1e-3));
        }

        [Test]
        public void CastTime_floors_at_quarter_of_base_for_very_high_speed_stats()
        {
            var s = new Stats { Wisdom = 10000f, Intelligence = 10000f };
            Assert.That(Formulas.CastTime(4f, s), Is.EqualTo(1f).Within(1e-3)); // 0.25 * 4
        }
        [TestCase(1, 60)]
        [TestCase(2, 90)]
        [TestCase(5, 300)]
        public void XpToNext_matches_formula(int level, int expected)
        {
            Assert.That(Formulas.XpToNext(level), Is.EqualTo(expected));
        }

        [Test]
        public void LevelFromTotalXp_at_zero_xp_is_level_one_with_no_progress()
        {
            var (level, into) = Formulas.LevelFromTotalXp(0);
            Assert.That(level, Is.EqualTo(1));
            Assert.That(into, Is.EqualTo(0));
        }
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(10)]
        public void XpRoundTrips_exactly_at_each_level_boundary(int level)
        {
            int totalAtLevel = Formulas.TotalXpForLevel(level);
            var (atLevel, atInto) = Formulas.LevelFromTotalXp(totalAtLevel);
            Assert.That(atLevel, Is.EqualTo(level));
            Assert.That(atInto, Is.EqualTo(0));

            if (totalAtLevel > 0)
            {
                var (belowLevel, belowInto) = Formulas.LevelFromTotalXp(totalAtLevel - 1);
                Assert.That(belowLevel, Is.EqualTo(level - 1));
                Assert.That(belowInto, Is.EqualTo(Formulas.XpToNext(level - 1) - 1));
            }
        }

        [Test]
        public void XpReward_scales_with_def_level_and_stats()
        {
            var stats = new Stats { Strength = 8f, Vitality = 12f, Intelligence = 5f, Wisdom = 5f };
            var enemy = MakeUnit(Team.Enemy, 4, stats, xpReward: 10);
            // phys=20, magic=10; reward=10+4*2+20*0.10+10*0.08=20.8 -> round(21)
            Assert.That(Formulas.XpReward(enemy), Is.EqualTo(21));
        }

        [Test]
        public void XpReward_never_returns_below_one()
        {
            var enemy = MakeUnit(Team.Enemy, 0, new Stats(), xpReward: 0);
            Assert.That(Formulas.XpReward(enemy), Is.EqualTo(1));
        }

        [Test]
        public void HealPower_returns_one_for_non_hero_or_null_caster()
        {
            var enemy = MakeUnit(Team.Enemy, 1, new Stats { Wisdom = 50f });
            Assert.That(Formulas.HealPower(enemy), Is.EqualTo(1f));
            Assert.That(Formulas.HealPower(null), Is.EqualTo(1f));
        }
        [TestCase(12f, 1f)]
        [TestCase(1000f, 1.6f)]
        [TestCase(-1000f, 0.7f)]
        public void HealPower_scales_with_wisdom_and_clamps_to_caster_power_bounds(float wisdom, float expected)
        {
            var hero = MakeUnit(Team.Hero, 1, new Stats { Wisdom = wisdom });
            Assert.That(Formulas.HealPower(hero), Is.EqualTo(expected).Within(1e-3));
        }

        [Test]
        public void CasterPower_returns_one_for_null_ability_or_non_hero_caster()
        {
            var hero = MakeUnit(Team.Hero, 1, new Stats { Strength = 50f });
            Assert.That(Formulas.CasterPower(null, hero), Is.EqualTo(1f));

            var enemy = MakeUnit(Team.Enemy, 1, new Stats { Strength = 50f });
            Assert.That(Formulas.CasterPower(new AbilityDef { DamageType = DamageType.Physical }, enemy), Is.EqualTo(1f));
        }

        [Test]
        public void CasterPower_uses_strength_for_physical_and_intelligence_for_other_types()
        {
            var hero = MakeUnit(Team.Hero, 1, new Stats { Strength = 22f, Intelligence = 9f });
            var physical = new AbilityDef { DamageType = DamageType.Physical };
            var magic = new AbilityDef { DamageType = DamageType.Fire };
            Assert.That(Formulas.CasterPower(physical, hero), Is.EqualTo(1f + (22f - 12f) * 0.035f).Within(1e-3));
            Assert.That(Formulas.CasterPower(magic, hero), Is.EqualTo(1f + (9f - 12f) * 0.035f).Within(1e-3));
        }

        [Test]
        public void Magic_damage_is_zero_when_target_is_immune()
        {
            var caster = MakeUnit(Team.Enemy, 5, new Stats { Intelligence = 50f, Speed = 50f, Luck = 50f });
            var target = MakeUnit(Team.Hero, 5, new Stats { Wisdom = 10f });
            target.Def.Resistances[DamageType.Fire] = 0f;

            for (int seed = 1; seed <= 20; seed++)
            {
                var roll = Formulas.Magic(caster, target, DamageType.Fire, new Rng(seed));
                Assert.That(roll.Damage, Is.EqualTo(0));
            }
        }

        [Test]
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
                    Assert.That(roll.Damage, Is.EqualTo(0));
                }
            }

            Assert.That(sawMiss, Is.True, "Expected at least one miss across the seed sample (hit chance floors at 5%)");
        }

        [Test]
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
                if (roll.Hit == HitKind.Miss) { Assert.That(roll.Damage, Is.EqualTo(0)); continue; }
                sawHit = true;
                Assert.That(roll.Damage >= 1, Is.True);
            }

            Assert.That(sawHit, Is.True, "Expected at least one hit across the seed sample (hit chance floors at 95%)");
        }
    }
}
