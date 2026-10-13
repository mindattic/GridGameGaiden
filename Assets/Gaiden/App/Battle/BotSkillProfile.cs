using UnityEngine;

namespace Gaiden.App
{
    /// <summary>
    /// Demo/capture-only knob (owner request): one 0-100 number spanning "perfect campaign" down to "a
    /// player with only basic understanding of the game". Feeds two independent things from the same
    /// number — HeroBot.SkillLevel (decision quality: which move it picks) and the human-cursor
    /// simulation in BattleScreen (execution precision: how cleanly a chosen move's drag is carried out,
    /// including whether a diagonal slip lands clean or slides onto the wrong tile). Never wired into a
    /// real difficulty setting — set via `-skill N` at the command line, same pattern as `-noDiagonalSlip`.
    /// </summary>
    public static class BotSkillProfile
    {
        /// <summary>0 = clumsiest simulated player, 100 = perfect (default — untouched autoplay behaves exactly
        /// as it always has).</summary>
        public static float Level = 100f;

        /// <summary>`-telemetry`: tag battle records with this run's skill level and the road-not-taken
        /// "perfect" move at every autoplay decision, so ML can learn the cost of imperfect play. Off by
        /// default — never active in a normal release run.</summary>
        public static bool TelemetryEnabled;

        public static float Skill01 => Mathf.Clamp01(Level / 100f);

        /// <summary>
        /// Fitts's Law (MT = a + b*log2(D/W + 1)): bigger, more precise moves take longer; skill shortens
        /// both terms so an expert's drag is quick and decisive while a novice's is slow and effortful.
        /// </summary>
        public static float FittsDuration(float distance, float targetWidth)
        {
            float skill01 = Skill01;
            float a = Mathf.Lerp(0.30f, 0.06f, skill01);
            float b = Mathf.Lerp(0.40f, 0.09f, skill01);
            return a + b * Mathf.Log(Mathf.Max(0f, distance) / Mathf.Max(1f, targetWidth) + 1f, 2f);
        }

        /// <summary>Minimum-jerk quintic (Flash &amp; Hogan 1985): zero velocity and acceleration at both ends of
        /// the move — the shape of a real, unhurried hand motion, not a linear slide.</summary>
        public static float MinimumJerk(float tau)
        {
            tau = Mathf.Clamp01(tau);
            return 10f * tau * tau * tau - 15f * tau * tau * tau * tau + 6f * tau * tau * tau * tau * tau;
        }

        /// <summary>Physiological micro-tremor amplitude (px) layered on top of the path while it travels.</summary>
        public static float TremorPixels => Mathf.Lerp(16f, 1.5f, Skill01);

        /// <summary>Chance the cursor overshoots the target a little before correcting back (rises for a
        /// clumsier simulated player, per the overshoot curves documented in human-mouse-movement tooling).</summary>
        public static float OvershootChance => Mathf.Lerp(0.38f, 0.02f, Skill01);

        /// <summary>Chance a drag even *attempts* a diagonal-slip corner-cut once it meets one, rather than
        /// taking the safe two-cardinal-step route (a confident player reaches for the tech; a novice doesn't).</summary>
        public static float DiagonalAttemptChance => Mathf.Lerp(0.05f, 0.9f, Skill01);

        /// <summary>How far off a true 45° flick and how much slower than the real gate's minimum speed a
        /// clumsier attempt drifts — large enough at low skill that the attempt legitimately misses the real
        /// speed/angle gate in BattleScreen.Input and falls back on its own, not by being told to fail.</summary>
        public static float ExecutionNoise => Mathf.Lerp(1f, 0f, Skill01);
    }
}
