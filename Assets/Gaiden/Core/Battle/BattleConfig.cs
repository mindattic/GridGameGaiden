namespace Gaiden.Core.Battle
{
    /// <summary>
    /// Every tuning number of the battle simulation in one place (bible §2, §3, §13).
    /// Time is integer ticks (1 tick = 0.1 "game seconds"); the timeline bar is BarLength IP.
    /// The clock only advances when the player commits an action (bible §2, act-advanced amendment).
    /// </summary>
    public static class BattleConfig
    {
        public const int Cols = 6;
        public const int Rows = 8;

        // ── Timeline ────────────────────────────────────────────────────────────────
        public const int TicksPerSecond = 10;
        public const int BarLength = 1000;                 // u = ip / BarLength
        public const float ZoneU = 0.35f;                  // Prepare/Pushback Zone width
        public const int ZoneStart = (int)(BarLength * (1f - ZoneU)); // 650
        /// <summary>Open-track IP per tick per point of Speed (Speed 10 → 16 IP/tick → 4 s to the Zone).</summary>
        public const float OpenRatePerSpeed = 1.6f;
        /// <summary>Shared Zone crawl: every icon crosses the Zone in ZoneCrossingTicks regardless of Speed.</summary>
        public const int ZoneCrossingTicks = 30;
        /// <summary>Reference pace (Speed 10) used to draw cast icons on the shared u-axis.</summary>
        public const float ReferencePacePerTick = 16f;
        /// <summary>Game-clock time one committed hero action costs.</summary>
        public const int ActionTicks = 20;
        /// <summary>Max random head-start IP for icons at wave spawn (staggered entry).</summary>
        public const int SpawnStaggerIp = 250;

        // ── Pushback (§2.3 / §2.7) ─────────────────────────────────────────────────
        public const float PushbackBaseU = 0.18f;
        public const float PushbackMaxU = 0.40f;
        public const int BaseStunTicks = 8;

        // ── Hasten (§2.7.1) ─────────────────────────────────────────────────────────
        public const float DefaultHastenU = 0.30f;

        // ── Buffs (§8.3) ────────────────────────────────────────────────────────────
        public const int BuffTickInterval = 10;            // one tick-unit step per 1.0 s of timeline
        public const float LightningWhenWetMultiplier = 1.5f;
        public const float SleepWhenWarmMultiplier = 1.5f;
        public const float LightningBlindChance = 0.30f;
        /// <summary>Spells never miss but crit at this fraction of the caster's physical crit chance (US-115 decision).</summary>
        public const float SpellCritFraction = 0.5f;
        public const float SpellCritMultiplier = 1.6f;

        // ── Casting (§2.6 / §13.4) ─────────────────────────────────────────────────
        public const int MaxHeroCastsInFlight = 4;
        public const float DefaultChargeCastSeconds = 2.5f;
        public const float BaseInterruptDelaySeconds = 0.6f;
        public const float InterruptStrengthScale = 20f;
        public const float InterruptWisdomDelayScale = 15f;
        public const float WisdomResistPerPoint = 0.015f;
        public const float MaxResistChance = 0.60f;
        public const float ClutchChancePerLuck = 1f / 200f;
        public const float ClutchMaxChance = 0.20f;

        // ── Supporters (§1.2.3) ─────────────────────────────────────────────────────
        public const float SupporterDamageFraction = 0.5f;

        // ── Shield (§2.5) ───────────────────────────────────────────────────────────
        public const string ShieldBuffId = "protection";
        public const int MaxBankCapacityBonus = 4;
        /// <summary>New Game+: enemy levels rise by this per cycle; coin rewards scale by NgPlusCoinBonus per cycle.</summary>
        public const int NgPlusLevelStep = 10;
        public const float NgPlusCoinBonus = 0.5f;

        // ── Time bank (§3.1.8, act-advanced amendment: drag-timer remainder) ────────
        public const float DragTimerSeconds = 4f;
        public const float SecondsPerTimeBankOrb = 1.25f;
        public const int MaxTimeBankOrbs = 3;

        // ── Steal (§6 stage 8) ──────────────────────────────────────────────────────
        public const float StealDivisor = 50f;

        // ── Weapon durability (§24.5) ───────────────────────────────────────────────
        public const float ShatterTargetMultiplier = 1.5f;
        public const float ShatterSelfDamageFraction = 0.15f;

        // ── Enemy AI (§14.1) ────────────────────────────────────────────────────────
        public const float RetreatHpThreshold = 0.30f;

        // ── Power Amplifier (owner request 2026-10-10, from Terra Battle) ────────────
        /// <summary>Chance per player action that an amplifier appears (when none is on the board)…</summary>
        public const float AmplifierBaseChance = 0.12f;
        /// <summary>…plus this per point of the living party's combined Luck…</summary>
        public const float AmplifierChancePerLuck = 0.003f;
        /// <summary>…capped here.</summary>
        public const float AmplifierMaxChance = 0.45f;
        /// <summary>A pincer attacker standing on the amplifier strikes this much harder (and mints a bonus orb).</summary>
        public const float AmplifierDamageMultiplier = 1.75f;

        /// <summary>Knockback into another unit: both take this (+ half the blow).</summary>
        public const int KnockbackCrashDamage = 8;
        /// <summary>Knockback into stone or the board edge: the target takes this (+ the full blow).</summary>
        public const int KnockbackSlamDamage = 18;
    }
}
