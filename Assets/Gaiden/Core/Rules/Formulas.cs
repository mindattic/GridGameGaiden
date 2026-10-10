using System;
using System.Collections.Generic;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;

namespace Gaiden.Core.Rules
{
    public struct AttackRoll
    {
        public int Damage;
        public HitKind Hit;
    }

    /// <summary>Combat math (bible §3.2, §13.1). Ported from the old Formulas with engine types removed.</summary>
    public static class Formulas
    {
        private const float HitShiftPerLevel = 3f;
        private const float DamageScalePerLevel = 1.07f;
        private const float CritMultiplier = 1.60f;
        private const float DefenseMitigation = 0.35f;
        private const float PenetrationFromAttack = 0.25f;
        private const float OffenseFloorFraction = 0.20f;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        public static int MaxHp(Stats s, int level) => (int)Math.Floor(12f + s.Vitality * 5f + level * 1f);

        /// <summary>Level-derived stats: base + per-level growth + instant milestone bonuses, floored (old ActorData.GetStatsWithOptions).</summary>
        public static Stats StatsAtLevel(ClassDef def, int level)
        {
            if (level < 1) level = 1;
            var s = def.BaseStats.Clone();
            var milestones = new Dictionary<int, Stats>();
            foreach (var m in def.Milestones) milestones[m.Level] = m.Growth;
            for (int l = 2; l <= level; l++)
            {
                s = s + def.Growth;
                if (milestones.TryGetValue(l, out var bonus)) s = s + bonus;
            }
            s.Strength = (float)Math.Floor(s.Strength);
            s.Vitality = (float)Math.Floor(s.Vitality);
            s.Agility = (float)Math.Floor(s.Agility);
            s.Speed = (float)Math.Floor(s.Speed);
            s.Stamina = (float)Math.Floor(s.Stamina);
            s.Intelligence = (float)Math.Floor(s.Intelligence);
            s.Wisdom = (float)Math.Floor(s.Wisdom);
            s.Luck = (float)Math.Floor(s.Luck);
            return s;
        }

        public static float HitPercent(Stats s) => Clamp(80f + s.Speed * 0.5f + s.Luck * 0.2f, 5f, 95f);
        public static float CritPercent(Stats s) => Clamp(8f + s.Luck * 0.4f, 0f, 50f);

        public static HitKind RollHit(Unit attacker, Unit defender, Rng rng)
        {
            var a = attacker.Effective;
            int adv = attacker.Level - defender.Level;
            float hit = Clamp(HitPercent(a) + adv * HitShiftPerLevel, 5f, 95f);
            float acc = attacker.AccuracyMultiplier;
            if (acc < 1f) hit = Clamp(hit * acc, 5f, 95f);
            float crit = CritPercent(a);
            float roll = rng.Range(0f, 100f);
            if (roll <= crit) return HitKind.Critical;
            if (roll <= hit) return HitKind.Normal;
            return HitKind.Miss;
        }

        private static float Variance(Stats s, Rng rng, float range)
        {
            float lo = 1f - range, hi = 1f + range;
            float roll = rng.Range(lo, hi);
            float tilt = 0.99f + 0.02f * Clamp(s.Luck / 100f, 0f, 1f);
            return Clamp(roll * tilt, lo, hi);
        }

        private static float Mitigate(float off, float def)
        {
            float pen = off * PenetrationFromAttack;
            float effDef = Math.Max(0f, def - pen);
            float reduced = off - effDef * DefenseMitigation;
            float floor = off * OffenseFloorFraction;
            return Math.Max(1f, Math.Max(reduced, floor));
        }

        /// <summary>Physical pincer / melee attack (§13.1.1).</summary>
        public static AttackRoll Physical(Unit attacker, Unit defender, Rng rng, float multiplier = 1f)
        {
            var a = attacker.Effective;
            var d = defender.Effective;
            float off = a.Strength + attacker.WeaponPower;
            float def = d.Vitality + defender.ArmorRating;
            float raw = Mitigate(off, def);
            raw *= defender.ResistanceMultiplier(DamageType.Physical);
            float varied = raw * Variance(a, rng, 0.10f);
            var hit = RollHit(attacker, defender, rng);
            if (hit == HitKind.Miss) return new AttackRoll { Damage = 0, Hit = HitKind.Miss };
            float typeMult = hit == HitKind.Critical ? CritMultiplier : 1f;
            float levelMult = (float)Math.Pow(DamageScalePerLevel, attacker.Level - defender.Level);
            int dmg = Math.Max(1, (int)Math.Floor(varied * typeMult * levelMult * multiplier));
            float inc = defender.IncomingDamageMultiplier;
            if (inc != 1f) dmg = Math.Max(1, (int)Math.Floor(dmg * inc));
            if (defender.ResistanceMultiplier(DamageType.Physical) <= 0f) dmg = 0;
            return new AttackRoll { Damage = dmg, Hit = hit };
        }

        /// <summary>Enemy magic hit (charge resolution): INT vs WIS, same mitigation shape (§13.1).</summary>
        public static AttackRoll Magic(Unit caster, Unit target, DamageType type, Rng rng)
        {
            var a = caster.Effective;
            var d = target.Effective;
            float raw = Mitigate(a.Intelligence, d.Wisdom);
            float res = target.ResistanceMultiplier(type);
            raw *= res;
            float varied = raw * Variance(a, rng, 0.10f);
            var hit = RollHit(caster, target, rng);
            if (hit == HitKind.Miss) return new AttackRoll { Damage = 0, Hit = HitKind.Miss };
            float typeMult = hit == HitKind.Critical ? CritMultiplier : 1f;
            float levelMult = (float)Math.Pow(DamageScalePerLevel, caster.Level - target.Level);
            int dmg = res <= 0f ? 0 : Math.Max(1, (int)Math.Floor(varied * typeMult * levelMult));
            float inc = target.IncomingDamageMultiplier;
            if (dmg > 0 && inc != 1f) dmg = Math.Max(1, (int)Math.Floor(dmg * inc));
            return new AttackRoll { Damage = dmg, Hit = hit };
        }

        /// <summary>Dispatcher spell damage (§13.1.2): flat base × resistance × wet bonus × incoming mult, min 1 unless immune.</summary>
        public static int SpellDamage(AbilityDef spell, Unit target, bool wetBonus)
        {
            float res = target.ResistanceMultiplier(spell.DamageType);
            float elem = wetBonus ? BattleConfig.LightningWhenWetMultiplier : 1f;
            float final = (float)Math.Round(spell.BaseDamage * res * elem * target.IncomingDamageMultiplier);
            if (spell.BaseDamage > 0 && res > 0f) final = Math.Max(1f, final);
            return (int)final;
        }

        /// <summary>WIS+INT shortens cast time, floored at 25% of base (old Formulas.CastTime).</summary>
        public static float CastTime(float baseSeconds, Stats s)
        {
            if (baseSeconds <= 0f) return 0f;
            float skill = Math.Max(0f, s.Wisdom + s.Intelligence);
            float scale = 50f / (50f + skill * 0.5f);
            return Math.Max(baseSeconds * 0.25f, baseSeconds * scale);
        }

        public static int SecondsToTicks(float seconds) => Math.Max(1, (int)Math.Round(seconds * BattleConfig.TicksPerSecond));

        /// <summary>XP awarded for defeating an enemy (old ExperienceHelper.Calculate shape).</summary>
        public static int XpReward(Unit enemy)
        {
            var s = enemy.Effective;
            float phys = s.Strength + s.Vitality;
            float magic = s.Intelligence + s.Wisdom;
            float reward = enemy.Def.XpReward + enemy.Level * 2f + phys * 0.10f + magic * 0.08f;
            return Math.Max(1, (int)Math.Round(reward));
        }

        /// <summary>Total XP needed to go from <paramref name="level"/> to level+1.</summary>
        public static int XpToNext(int level) => 50 + level * level * 10;

        public static (int level, int intoLevel) LevelFromTotalXp(int totalXp)
        {
            int level = 1, remaining = Math.Max(0, totalXp);
            while (level < 99 && remaining >= XpToNext(level)) { remaining -= XpToNext(level); level++; }
            return (level, remaining);
        }

        public static int TotalXpForLevel(int level)
        {
            int total = 0;
            for (int l = 1; l < level; l++) total += XpToNext(l);
            return total;
        }
    }
}
