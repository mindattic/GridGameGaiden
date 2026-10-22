using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;

namespace GridGame.Core.Content
{
    /// <summary>
    /// GG-T184: the authored enemy groups (encounters.json, <see cref="EncounterGroupDef"/>). Each group is one enemy
    /// family's mix on fixed tiles (its formation) at a level. Campaign waves name them (<see cref="WaveDef.Group"/>)
    /// in place of the waves they used to repeat, and the Crucible and endless encounters build every wave from a
    /// group rather than a random handful of one family, so a run walks through distinct formations. <see cref="Validate"/>
    /// keeps the catalog honest: no two groups are the same mix on the same tiles, and no two campaign waves are the
    /// same foes at the same level on the same tiles.
    /// </summary>
    public static class EncounterCatalog
    {
        public const int MaxGroupSize = 5;
        /// <summary>Formation tiles sit on the enemy half: rows 0..EnemyRows-1 (the band random enemy spawns use).</summary>
        public const int EnemyRows = 5;

        /// <summary>A class a group may hold: an ordinary one-tile enemy with a family (drop table). Bosses, multi-tile
        /// foes, snakes and egg layers stay in their own hand-authored stages.</summary>
        public static bool IsGroupable(ClassDef c) =>
            c != null && c.Team == Team.Enemy && !string.IsNullOrEmpty(c.DropTable) && c.BossPhases.Count == 0
            && !c.Tags.Contains("Boss") && (c.Footprint?.W ?? 1) <= 1 && (c.Footprint?.H ?? 1) <= 1
            && c.SnakeSegments <= 0 && string.IsNullOrEmpty(c.LaysEgg);

        /// <summary>The group's foes as wave spawns at <paramref name="level"/>, each on its formation tile.</summary>
        public static List<SpawnDef> Spawns(EncounterGroupDef g, int level) =>
            g.Enemies.Select(e => new SpawnDef { Class = e.Class, Level = Math.Max(1, level), X = e.X, Y = e.Y }).ToList();

        /// <summary>The group's family (its foes' shared drop table), or null if its first foe is unknown.</summary>
        public static string Family(ContentDb db, EncounterGroupDef g) =>
            g.Enemies.Count > 0 ? db.TryClass(g.Enemies[0].Class)?.DropTable : null;

        /// <summary>Identity of a set of spawns: the sorted foes with their tiles (and, for waves, level and delay).
        /// Two groups or waves with the same signature are the same encounter.</summary>
        public static string Signature(IEnumerable<SpawnDef> spawns, bool withLevel) =>
            string.Join(";", spawns.Select(e => withLevel ? $"{e.Class}@{e.X},{e.Y}L{e.Level}d{e.DelayTicks}" : $"{e.Class}@{e.X},{e.Y}")
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        /// <summary>Every group of <paramref name="family"/>, in file order.</summary>
        public static List<EncounterGroupDef> ForFamily(ContentDb db, string family) =>
            db.EncounterOrder.Where(g => string.Equals(Family(db, g), family, StringComparison.OrdinalIgnoreCase)).ToList();

        /// <summary>
        /// The next group for a generated wave of <paramref name="family"/>: the largest that fits in
        /// <paramref name="maxSize"/> foes, one not yet in <paramref name="used"/> while any of that size is left, at or
        /// under <paramref name="level"/> when one is; ties broken by <paramref name="rng"/>. Records the pick in
        /// <paramref name="used"/>. Null when the family has no group that small.
        /// </summary>
        public static EncounterGroupDef Pick(ContentDb db, Rng rng, string family, int maxSize, int level, HashSet<string> used)
        {
            var fits = ForFamily(db, family).Where(g => g.Enemies.Count > 0 && g.Enemies.Count <= maxSize).ToList();
            if (fits.Count == 0) return null;
            int size = fits.Max(g => g.Enemies.Count);
            var pool = fits.Where(g => g.Enemies.Count == size).ToList();
            var fresh = pool.Where(g => !used.Contains(g.Id)).ToList();
            if (fresh.Count > 0) pool = fresh;
            else foreach (var g in pool) used.Remove(g.Id); // every group of this size has had its turn: start over
            var atLevel = pool.Where(g => g.Level <= level).ToList();
            if (atLevel.Count > 0) pool = atLevel;
            var pick = rng.Pick(pool);
            used.Add(pick.Id);
            return pick;
        }

        /// <summary>Catalog and campaign checks, appended to <see cref="ContentLoader.Validate"/>'s problems.</summary>
        public static void Validate(ContentDb db, List<string> p)
        {
            var bySig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in db.EncounterOrder)
            {
                string w = $"encounter {g.Id}";
                if (string.IsNullOrWhiteSpace(g.Name)) p.Add($"{w}: needs a Name");
                if (string.IsNullOrWhiteSpace(g.Formation)) p.Add($"{w}: needs a Formation");
                if (g.Level < 1) p.Add($"{w}: Level must be at least 1");
                if (g.Enemies.Count < 1 || g.Enemies.Count > MaxGroupSize) p.Add($"{w}: needs 1-{MaxGroupSize} foes, has {g.Enemies.Count}");
                var tiles = new HashSet<(int, int)>();
                var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in g.Enemies)
                {
                    var c = db.TryClass(e.Class);
                    if (c == null) { p.Add($"{w}: unknown class '{e.Class}'"); continue; }
                    if (!IsGroupable(c)) p.Add($"{w}: '{e.Class}' can't be in a group (boss, multi-tile, snake, egg layer, hero or no family)");
                    else families.Add(c.DropTable);
                    if (e.X < 0 || e.X >= BattleConfig.Cols || e.Y < 0 || e.Y >= EnemyRows) p.Add($"{w}: '{e.Class}' at ({e.X},{e.Y}) is off the enemy half (0-{BattleConfig.Cols - 1}, 0-{EnemyRows - 1})");
                    else if (!tiles.Add((e.X, e.Y))) p.Add($"{w}: two foes on ({e.X},{e.Y})");
                }
                if (families.Count > 1) p.Add($"{w}: mixes families ({string.Join(", ", families)})");
                var sig = Signature(g.Enemies, false);
                if (bySig.TryGetValue(sig, out var twin)) p.Add($"{w}: same foes on the same tiles as '{twin}'");
                else bySig[sig] = g.Id;
            }

            foreach (var s in db.Stages.Values)
                foreach (var wave in s.Waves)
                    if (wave.Group != null && db.TryEncounter(wave.Group) == null) p.Add($"stage {s.Id}: unknown encounter group '{wave.Group}'");

            // No two campaign waves are the same encounter, and a group is used by one campaign wave at most.
            var waveSig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var groupUse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in db.CampaignStages())
                for (int i = 0; i < s.Waves.Count; i++)
                {
                    var wave = s.Waves[i];
                    string here = $"{s.Id} wave {i + 1}";
                    if (wave.Enemies.Count == 0) continue;
                    var sig = Signature(wave.Enemies, true);
                    if (waveSig.TryGetValue(sig, out var first)) p.Add($"stage {here}: the same foes, levels and tiles as {first}");
                    else waveSig[sig] = here;
                    if (wave.Group == null) continue;
                    if (groupUse.TryGetValue(wave.Group, out var other)) p.Add($"stage {here}: encounter group '{wave.Group}' is already used by {other}");
                    else groupUse[wave.Group] = here;
                }
        }
    }
}
