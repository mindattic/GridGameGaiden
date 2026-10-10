using System;
using System.Collections.Generic;

namespace Gaiden.Core
{
    /// <summary>
    /// Deterministic, copyable PRNG (xoshiro128**). Every random decision in the simulation
    /// goes through one of these so a battle replays exactly from its seed + command log.
    /// </summary>
    public sealed class Rng
    {
        private uint s0, s1, s2, s3;

        public Rng(int seed)
        {
            // SplitMix32 seeding so nearby seeds diverge immediately.
            uint x = (uint)seed;
            s0 = Mix(ref x); s1 = Mix(ref x); s2 = Mix(ref x); s3 = Mix(ref x);
            if ((s0 | s1 | s2 | s3) == 0) s0 = 1;
        }

        private Rng() { }

        public Rng Clone() => new Rng { s0 = s0, s1 = s1, s2 = s2, s3 = s3 };

        private static uint Mix(ref uint x)
        {
            x += 0x9E3779B9u;
            uint z = x;
            z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
            z = (z ^ (z >> 13)) * 0xC2B2AE35u;
            return z ^ (z >> 16);
        }

        private static uint Rotl(uint x, int k) => (x << k) | (x >> (32 - k));

        public uint NextUInt()
        {
            uint result = Rotl(s1 * 5, 7) * 9;
            uint t = s1 << 9;
            s2 ^= s0; s3 ^= s1; s1 ^= s2; s0 ^= s3;
            s2 ^= t;
            s3 = Rotl(s3, 11);
            return result;
        }

        /// <summary>Uniform float in [0,1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Uniform int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }

        public bool Chance(float p) => p > 0f && NextFloat() < p;

        public T Pick<T>(IReadOnlyList<T> list) => list[Range(0, list.Count)];
    }
}
