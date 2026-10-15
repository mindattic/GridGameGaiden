using NUnit.Framework;

namespace Gaiden.App.Tests
{
    public class BotSkillProfileTests
    {
        private float originalLevel;

        [SetUp]
        public void SetUp()
        {
            originalLevel = BotSkillProfile.Level;
        }

        [TearDown]
        public void TearDown()
        {
            BotSkillProfile.Level = originalLevel;
        }

        [TestCase(-50f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(50f, 0.5f)]
        [TestCase(100f, 1f)]
        [TestCase(150f, 1f)]
        public void Skill01_Clamps(float level, float expected)
        {
            BotSkillProfile.Level = level;
            Assert.AreEqual(expected, BotSkillProfile.Skill01, 1e-6f);
        }

        [Test]
        public void FittsDuration_GrowsWithDistance()
        {
            BotSkillProfile.Level = 50f;
            var near = BotSkillProfile.FittsDuration(10f, 50f);
            var far = BotSkillProfile.FittsDuration(500f, 50f);
            Assert.Less(near, far);
        }

        [Test]
        public void FittsDuration_ShrinksWithSkill()
        {
            BotSkillProfile.Level = 0f;
            var clumsy = BotSkillProfile.FittsDuration(100f, 50f);
            BotSkillProfile.Level = 100f;
            var expert = BotSkillProfile.FittsDuration(100f, 50f);
            Assert.Less(expert, clumsy);
        }

        [Test]
        public void MinimumJerk_IsZeroAtZero_OneAtOne()
        {
            Assert.AreEqual(0f, BotSkillProfile.MinimumJerk(0f), 1e-6f);
            Assert.AreEqual(1f, BotSkillProfile.MinimumJerk(1f), 1e-6f);
        }

        [Test]
        public void MinimumJerk_ClampsOutsideRange()
        {
            Assert.AreEqual(BotSkillProfile.MinimumJerk(0f), BotSkillProfile.MinimumJerk(-1f), 1e-6f);
            Assert.AreEqual(BotSkillProfile.MinimumJerk(1f), BotSkillProfile.MinimumJerk(2f), 1e-6f);
        }

        [Test]
        public void TremorPixels_IsMonotonicDecreasing_WithLevel()
        {
            BotSkillProfile.Level = 0f;
            var low = BotSkillProfile.TremorPixels;
            BotSkillProfile.Level = 50f;
            var mid = BotSkillProfile.TremorPixels;
            BotSkillProfile.Level = 100f;
            var high = BotSkillProfile.TremorPixels;
            Assert.Greater(low, mid);
            Assert.Greater(mid, high);
        }

        [Test]
        public void OvershootChance_IsMonotonicDecreasing_WithLevel()
        {
            BotSkillProfile.Level = 0f;
            var low = BotSkillProfile.OvershootChance;
            BotSkillProfile.Level = 50f;
            var mid = BotSkillProfile.OvershootChance;
            BotSkillProfile.Level = 100f;
            var high = BotSkillProfile.OvershootChance;
            Assert.Greater(low, mid);
            Assert.Greater(mid, high);
        }

        [Test]
        public void DiagonalAttemptChance_IsMonotonicIncreasing_WithLevel()
        {
            BotSkillProfile.Level = 0f;
            var low = BotSkillProfile.DiagonalAttemptChance;
            BotSkillProfile.Level = 50f;
            var mid = BotSkillProfile.DiagonalAttemptChance;
            BotSkillProfile.Level = 100f;
            var high = BotSkillProfile.DiagonalAttemptChance;
            Assert.Less(low, mid);
            Assert.Less(mid, high);
        }

        [Test]
        public void ExecutionNoise_IsMonotonicDecreasing_WithLevel()
        {
            BotSkillProfile.Level = 0f;
            var low = BotSkillProfile.ExecutionNoise;
            BotSkillProfile.Level = 50f;
            var mid = BotSkillProfile.ExecutionNoise;
            BotSkillProfile.Level = 100f;
            var high = BotSkillProfile.ExecutionNoise;
            Assert.Greater(low, mid);
            Assert.Greater(mid, high);
        }
    }
}
