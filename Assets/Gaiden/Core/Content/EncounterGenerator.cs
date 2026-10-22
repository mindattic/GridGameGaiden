using System;
using System.Collections.Generic;
using System.Linq;

namespace GridGame.Core.Content
{
    /// <summary>
    /// GG-T55: deterministic endless-mode encounters. Builds a synthetic 5-wave <see cref="StageDef"/> from a
    /// single seed — location/board borrowed from a random real stage, one enemy family per wave (house
    /// convention: never mix families in a wave, §droptables "one per enemy family"), families varying wave to
    /// wave, and levels rising across the full campaign spectrum. Builds on GG-T22's randombattle, which does the
    /// same "register a throwaway StageDef in the live ContentDb" trick for a single borrowed wave; this generates
    /// the whole 5-wave arc instead of borrowing one.
    /// </summary>
    public static class EncounterGenerator
    {
        public const int WaveCount = 5;
        public const string IdPrefix = "endless-";

        public static StageDef Generate(ContentDb db, int seed)
        {
            var rng = new Rng(seed);

            var donors = db.Stages.Values.Where(s => !s.IsTest && s.Waves.Count > 0)
                .OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
            if (donors.Count == 0) throw new InvalidOperationException("EncounterGenerator: no real stage to borrow location/blocks from");
            var donor = rng.Pick(donors);

            var families = db.Classes.Values
                .Where(c => c.Team == Team.Enemy && !string.IsNullOrEmpty(c.DropTable))
                .GroupBy(c => c.DropTable, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
                .ToList();
            if (families.Count == 0) throw new InvalidOperationException("EncounterGenerator: no enemy family (ClassDef.DropTable) to draw from");
            var waveFamilies = PickFamilies(rng, families, WaveCount);

            int campaignMax = Math.Max(1, db.CampaignStages().Count);
            var levels = RisingLevels(rng, WaveCount, campaignMax);

            var stage = new StageDef
            {
                Id = IdPrefix + unchecked((uint)seed).ToString("x8"),
                Name = $"{donor.Name} — Endless",
                Theme = donor.Theme,
                Background = donor.Background,
                Music = donor.Music,
                IsTest = false,
                LevelOverride = levels[WaveCount / 2],
                Blocks = donor.Blocks.Select(c => new Coord(c.X, c.Y)).ToList(),
            };

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < WaveCount; i++)
            {
                var pool = waveFamilies[i];
                int enemyCount = rng.Range(2, 5); // 2-4, matching real stages' typical wave size
                var wave = new WaveDef();
                // GG-T184: an authored group of the family (mix + formation) when it has one that fits; else random foes.
                var group = EncounterCatalog.Pick(db, rng, pool[0].DropTable, enemyCount, levels[i], used);
                if (group != null) { wave.Group = group.Id; wave.Enemies.AddRange(EncounterCatalog.Spawns(group, levels[i])); }
                else for (int e = 0; e < enemyCount; e++) wave.Enemies.Add(new SpawnDef { Class = rng.Pick(pool).Id, Level = levels[i] });
                stage.Waves.Add(wave);
            }

            return stage;
        }

        private static List<List<ClassDef>> PickFamilies(Rng rng, List<List<ClassDef>> families, int count)
        {
            var shuffled = new List<List<ClassDef>>(families);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            var picked = new List<List<ClassDef>>(count);
            for (int i = 0; i < count; i++) picked.Add(shuffled[i % shuffled.Count]);
            return picked;
        }

        /// <summary>Non-decreasing levels for each wave, spanning up to <paramref name="campaignMax"/> at the last
        /// wave and down to 1 at the floor, so a single random anchor (the final wave's level) determines where in
        /// the full 1..campaignMax spectrum this encounter sits while still rising wave to wave.</summary>
        private static int[] RisingLevels(Rng rng, int waveCount, int campaignMax)
        {
            int anchor = rng.Range(1, campaignMax + 1);
            var levels = new int[waveCount];
            for (int i = 0; i < waveCount; i++)
            {
                int offset = waveCount - 1 - i;
                levels[i] = Math.Max(1, Math.Min(campaignMax, anchor - offset));
            }
            return levels;
        }
    }
}
