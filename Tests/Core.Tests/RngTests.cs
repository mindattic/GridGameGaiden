using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>
    /// Battle replays and records are keyed on a seed, so Rng must stay bit-exact across runs (GG-T24).
    /// </summary>
    public class RngTests
    {
        private static uint[] Sequence(Rng rng, int count)
        {
            var result = new uint[count];
            for (int i = 0; i < count; i++) result[i] = rng.NextUInt();
            return result;
        }

        [Test]
        public void Same_seed_yields_identical_sequence()
        {
            var a = Sequence(new Rng(42), 50);
            var b = Sequence(new Rng(42), 50);
            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void Nearby_seeds_diverge_immediately()
        {
            var a = new Rng(1);
            var b = new Rng(2);
            Assert.That(b.NextUInt(), Is.Not.EqualTo(a.NextUInt()));
        }

        [Test]
        public void Seed_zero_is_non_degenerate()
        {
            var values = Sequence(new Rng(0), 50);
            Assert.That(values, Does.Not.Contain(0u));
            Assert.That(values.Distinct().Count() > 1, Is.True);
        }

        [Test]
        public void Clone_continues_the_exact_same_sequence_without_advancing_the_original()
        {
            var original = new Rng(7);
            var before = Sequence(original, 5);
            var clone = original.Clone();
            var fromClone = Sequence(clone, 10);
            var fromOriginal = Sequence(original, 10);
            Assert.That(fromOriginal, Is.EqualTo(fromClone));
            Assert.That(fromOriginal, Is.Not.EqualTo(before));
        }

        [Test]
        public void NextFloat_stays_in_zero_to_one_exclusive()
        {
            var rng = new Rng(123);
            for (int i = 0; i < 10000; i++)
            {
                float f = rng.NextFloat();
                Assert.That(f >= 0f && f < 1f, Is.True, $"out of range: {f}");
            }
        }

        [Test]
        public void Int_range_stays_within_min_and_max_exclusive()
        {
            var rng = new Rng(55);
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.Range(5, 10);
                Assert.That(v >= 5 && v < 10, Is.True, $"out of range: {v}");
            }
        }

        [Test]
        public void Int_range_returns_min_when_max_exclusive_is_not_greater_than_min()
        {
            var rng = new Rng(1);
            Assert.That(rng.Range(3, 3), Is.EqualTo(3));
            Assert.That(rng.Range(3, 2), Is.EqualTo(3));
        }

        [Test]
        public void Float_range_stays_within_min_and_max()
        {
            var rng = new Rng(77);
            for (int i = 0; i < 10000; i++)
            {
                float v = rng.Range(-2f, 3f);
                Assert.That(v >= -2f && v < 3f, Is.True, $"out of range: {v}");
            }
        }

        [Test]
        public void Chance_zero_is_always_false()
        {
            var rng = new Rng(9);
            for (int i = 0; i < 1000; i++) Assert.That(rng.Chance(0f), Is.False);
        }

        [Test]
        public void Chance_negative_is_always_false()
        {
            var rng = new Rng(9);
            for (int i = 0; i < 1000; i++) Assert.That(rng.Chance(-0.5f), Is.False);
        }

        [Test]
        public void Pick_throws_on_null_list()
        {
            var rng = new Rng(1);
            Assert.Throws<ArgumentException>(() => rng.Pick<int>(null));
        }

        [Test]
        public void Pick_throws_on_empty_list()
        {
            var rng = new Rng(1);
            Assert.Throws<ArgumentException>(() => rng.Pick(new List<int>()));
        }
    }
}
