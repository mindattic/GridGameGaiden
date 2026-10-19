using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;
using Gaiden.Meta;
using Newtonsoft.Json;
using NUnit.Framework;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>GG-T95: the Crucible (§22.10) — endless escalating waves from camp that keep every reward.</summary>
    public class CrucibleTests
    {
        private static ContentDb FreshReal() => ContentLoader.Load(name =>
        {
            var p = Path.Combine(ContentDir, name + ".json");
            return File.Exists(p) ? File.ReadAllText(p) : null;
        });

        private static string Family(ContentDb c, WaveDef w) => c.Class(w.Enemies[0].Class).DropTable;

        [Test]
        public void Crucible_waves_escalate_one_family_at_a_time_from_the_party_level()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Cru");
            foreach (var h in s.Roster) h.TotalXp = Gaiden.Core.Rules.Formulas.TotalXpForLevel(6);
            int start = Crucible.PartyLevel(s);
            Assert.That(start, Is.EqualTo(6));
            var st = Crucible.Generate(c, s, 1234);
            Assert.That(Crucible.IsStage(st.Id), Is.True);
            Assert.That(st.Waves.Count, Is.EqualTo(Crucible.MaxWaves));
            for (int i = 0; i < st.Waves.Count; i++)
            {
                var w = st.Waves[i];
                Assert.That(w.Enemies.Select(e => e.Level).Distinct(), Is.EqualTo(new[] { start + i * Crucible.LevelStep }), $"wave {i} level");
                Assert.That(w.Enemies.Select(e => c.Class(e.Class).DropTable).Distinct().Count(), Is.EqualTo(1), $"wave {i} mixes families");
                Assert.That(w.Enemies.Count, Is.EqualTo(Crucible.WaveEnemies(i)));
                Assert.That(w.Guests, Is.Empty);
                if (i > 0)
                {
                    Assert.That(w.Enemies.Count, Is.GreaterThanOrEqualTo(st.Waves[i - 1].Enemies.Count));
                    Assert.That(Family(c, w), Is.Not.EqualTo(Family(c, st.Waves[i - 1])), $"wave {i} repeats the last family");
                }
            }
            Assert.That(st.Waves.Last().Enemies.Count, Is.EqualTo(Crucible.MaxWaveEnemies));
        }

        [Test]
        public void Crucible_draws_only_families_the_save_has_met_or_reached()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Cru");
            var first = c.CampaignStages()[0];
            var reached = new HashSet<string>(first.Waves.SelectMany(w => w.Enemies).Select(e => c.Class(e.Class).DropTable).Where(t => t != null));
            var fams = Crucible.Families(c, s).Select(f => f[0].DropTable).ToList();
            Assert.That(fams, Is.Not.Empty);
            Assert.That(fams, Is.SubsetOf(reached), "a fresh save should only see the first stage's families");
            Assert.That(Crucible.Generate(c, s, 9).Waves.Select(w => Family(c, w)).Distinct(), Is.SubsetOf(reached));

            // Meeting a new family (the Bestiary) opens it up — and drops it brings are then relevant mats.
            var newcomer = c.Classes.Values.First(d => d.Team == Team.Enemy && d.DropTable != null && !reached.Contains(d.DropTable) && d.BossPhases.Count == 0 && !d.Tags.Contains("Boss"));
            s.Bestiary[newcomer.Id] = new BestiaryEntry { Seen = true };
            Assert.That(Crucible.Families(c, s).Select(f => f[0].DropTable), Does.Contain(newcomer.DropTable));
            Assert.That(Crucible.Families(c, s).SelectMany(f => f).Any(d => d.BossPhases.Count > 0 || d.Tags.Contains("Boss")), Is.False);
        }

        [Test]
        public void Crucible_is_deterministic_per_seed_and_per_save()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Cru");
            string Gen(int seed) => JsonConvert.SerializeObject(Crucible.Generate(c, s, seed));
            Assert.That(Gen(77), Is.EqualTo(Gen(77)));
            Assert.That(Gen(77), Is.Not.EqualTo(Gen(78)));

            int seed0 = Crucible.SeedFor(s);
            Assert.That(Crucible.SeedFor(s), Is.EqualTo(seed0), "the same save replays the same gauntlet");
            var reloaded = JsonConvert.DeserializeObject<SaveData>(JsonConvert.SerializeObject(s));
            Assert.That(Crucible.SeedFor(reloaded), Is.EqualTo(seed0), "a save/load round trip keeps the seed");
            s.Crucible.Runs++;
            Assert.That(Crucible.SeedFor(s), Is.Not.EqualTo(seed0), "each run fought moves to a new gauntlet");

            // The whole battle replays identically from the same seed, too.
            string Play()
            {
                var sb = new StringBuilder();
                var save = Progression.NewGame(c, "Cru");
                var st = Crucible.Generate(c, save, 5);
                var setup = BattleFactory.Build(c, save, st, 5);
                BotRunner.Play(c, setup, new HeroBot(5), 150, (cmd, ev, b) => { sb.AppendLine(cmd?.ToString()); foreach (var e in ev) sb.AppendLine(e.ToString()); });
                return sb.ToString();
            }
            Assert.That(Play(), Is.EqualTo(Play()));
        }

        [Test]
        public void Crucible_spawns_at_the_authored_level_even_on_new_game_plus()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Cru");
            s.NewGamePlus = 2;
            s.HighestClearedStage = 9; // the campaign curve must not lift it either
            foreach (var h in s.Roster) h.TotalXp = Gaiden.Core.Rules.Formulas.TotalXpForLevel(4);
            var st = Crucible.Generate(c, s, 3);
            var setup = BattleFactory.Build(c, s, st, 3);
            Assert.That(setup.NewGamePlus, Is.EqualTo(0));
            var b = BattleEngine.Create(c, setup, new List<BattleEvent>());
            var foes = b.Units.Where(u => u.Team == Team.Enemy && u.Alive && !u.IsCorpse).ToList();
            Assert.That(foes, Is.Not.Empty);
            Assert.That(foes.Select(u => u.Level).Distinct(), Is.EqualTo(new[] { 4 }));
        }

        [Test]
        public void Crucible_cannot_start_mid_expedition_and_says_why()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Cru");
            Assert.That(Crucible.CanLaunch(c, s, out var why), Is.True, why);
            Expeditions.Begin(c, s, c.CampaignStages()[0].Id);
            Assert.That(Crucible.CanLaunch(c, s, out why), Is.False);
            Assert.That(why, Does.Contain("expedition"));
            Expeditions.Abandon(s);
            Assert.That(Crucible.CanLaunch(c, s, out _), Is.True);
        }

        /// <summary>A Crucible battle ended with a given haul — the meta rules don't care how it played out.</summary>
        private static BattleState Ended(ContentDb c, SaveData s, BattlePhase phase, int wave, int coins, int xp, string item)
        {
            var st = Crucible.Generate(c, s, 42);
            var b = BattleEngine.Create(c, BattleFactory.Build(c, s, st, 42), new List<BattleEvent>());
            foreach (var u in b.Units.Where(u => u.IsHero)) { u.Hp = phase == BattlePhase.Defeat ? 0 : 1; }
            b.Wave = wave;
            b.Phase = phase;
            b.Results.Coins = coins;
            b.Results.Xp = xp;
            b.Results.Items[item] = 3;
            b.Results.Kills["slime00"] = 4;
            return b;
        }

        [TestCase(BattlePhase.Defeat)]
        [TestCase(BattlePhase.Retreated)]
        [TestCase(BattlePhase.Escaped)]
        public void A_crucible_wipe_or_retreat_keeps_every_reward_and_restores_the_party(BattlePhase phase)
        {
            var c = Real;
            var s = Progression.NewGame(c, "Cru");
            foreach (var h in s.Roster) h.Hp = 5; // wounds from earlier
            var mat = c.DropTables.Values.SelectMany(t => t.Entries).Select(e => e.Item).First(id => c.TryItem(id) != null && s.Count(id) == 0);
            int gold = s.Gold, lifetime = s.LifetimeCoins;
            var xpBefore = s.Party.ToDictionary(id => id, id => s.Hero(id).TotalXp);
            var b = Ended(c, s, phase, 4, 321, 90, mat);

            var rep = BattleFactory.Commit(c, s, b);

            Assert.That(rep.Crucible, Is.True);
            Assert.That(rep.Victory, Is.False);
            Assert.That(rep.Gold, Is.EqualTo(321), "the whole haul is kept, not RetreatKeepFraction of it");
            Assert.That(s.Gold, Is.EqualTo(gold + 321));
            Assert.That(s.LifetimeCoins, Is.EqualTo(lifetime + 321));
            Assert.That(s.Count(mat), Is.EqualTo(3));
            Assert.That(rep.Items[mat], Is.EqualTo(3));
            Assert.That(rep.XpEach, Is.EqualTo(90));
            foreach (var id in s.Party) Assert.That(s.Hero(id).TotalXp, Is.EqualTo(xpBefore[id] + 90), id);
            foreach (var id in s.Party) Assert.That(s.Hero(id).Hp, Is.EqualTo(0), $"{id} restored to full");
            Assert.That(s.Bestiary["slime00"].Defeated, Is.EqualTo(4));
            Assert.That(rep.CrucibleWaves, Is.EqualTo(4));
            Assert.That(rep.CrucibleNewBest, Is.True);
            Assert.That(s.Crucible.Best, Is.EqualTo(4));
            Assert.That(s.Crucible.Runs, Is.EqualTo(1));

            // The same ending outside the Crucible keeps nothing but hard-won XP (the exception is Crucible-only).
            var s2 = Progression.NewGame(c, "Normal");
            var normal = BattleEngine.Create(c, BattleFactory.Build(c, s2, c.CampaignStages()[0].Id, 1), new List<BattleEvent>());
            normal.Phase = BattlePhase.Defeat; normal.Results.Coins = 321; normal.Results.Items[mat] = 3;
            int g2 = s2.Gold;
            BattleFactory.Commit(c, s2, normal);
            Assert.That(s2.Gold, Is.EqualTo(g2));
            Assert.That(s2.Count(mat), Is.EqualTo(0));
        }

        [Test]
        public void A_worse_run_never_lowers_the_best_record()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Cru");
            BattleFactory.Commit(c, s, Ended(c, s, BattlePhase.Defeat, 7, 10, 10, "healing_potion"));
            var rep = BattleFactory.Commit(c, s, Ended(c, s, BattlePhase.Defeat, 2, 10, 10, "healing_potion"));
            Assert.That(rep.CrucibleWaves, Is.EqualTo(2));
            Assert.That(rep.CrucibleNewBest, Is.False);
            Assert.That(rep.CrucibleBest, Is.EqualTo(7));
            Assert.That(s.Crucible.Best, Is.EqualTo(7));
            Assert.That(s.Crucible.Runs, Is.EqualTo(2));
            Assert.That(s.Crucible.TotalWaves, Is.EqualTo(9));
        }

        [TestCase(BattlePhase.Defeat)]
        [TestCase(BattlePhase.Victory)]
        public void A_crucible_run_never_touches_campaign_progress_unlocks_or_the_expedition(BattlePhase phase)
        {
            var c = Real;
            var s = Progression.NewGame(c, "Cru");
            s.HighestClearedStage = 1;
            s.ClearedStages.Add(c.CampaignStages()[0].Id);
            s.ClearedStages.Add(c.CampaignStages()[1].Id);
            s.LastStage = c.CampaignStages()[1].Id;
            var cleared = s.ClearedStages.ToList();
            int slots = Progression.UnlockedSlots(s);
            var crawls = s.SeenCrawls.ToList();
            int roster = s.Roster.Count;

            var rep = BattleFactory.Commit(c, s, Ended(c, s, phase, phase == BattlePhase.Victory ? Crucible.MaxWaves : 3, 50, 20, "healing_potion"));

            Assert.That(s.HighestClearedStage, Is.EqualTo(1));
            Assert.That(s.ClearedStages, Is.EquivalentTo(cleared));
            Assert.That(s.LastStage, Is.EqualTo(c.CampaignStages()[1].Id));
            Assert.That(s.Expedition, Is.Null);
            Assert.That(Progression.UnlockedSlots(s), Is.EqualTo(slots));
            Assert.That(s.SeenCrawls, Is.EquivalentTo(crawls));
            Assert.That(s.Roster.Count, Is.EqualTo(roster));
            Assert.That(rep.FirstClear, Is.False);
            Assert.That(rep.UnlockedStage, Is.Null);
            Assert.That(rep.CampaignComplete, Is.False);
            Assert.That(rep.ExpeditionName, Is.Null);
            Assert.That(rep.CrucibleWaves, Is.EqualTo(phase == BattlePhase.Victory ? Crucible.MaxWaves : 3));
        }

        [Test]
        public void A_crucible_wipe_keeps_the_gear_wear_it_caused()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Cru");
            var h = s.Hero(s.Party[0]);
            var sword = c.Items.Values.First(i => i.Type == ItemType.Equipment && i.Slot == EquipmentSlot.Weapon && i.Durability > 3);
            s.AddItem(sword.Id, 1);
            Assert.That(Equipment.Equip(c, s, h, EquipmentSlot.Weapon, sword.Id).Ok, Is.True);
            int full = h.WeaponDurability;
            var b = Ended(c, s, BattlePhase.Defeat, 1, 0, 0, "healing_potion");
            b.Units.First(u => u.IsHero && u.Def.Id == h.ClassId).WeaponDurability = full - 2;
            BattleFactory.Commit(c, s, b);
            Assert.That(h.WeaponDurability, Is.EqualTo(full - 2));
            Assert.That(h.WeaponId, Is.EqualTo(sword.Id));
        }

        /// <summary>Bot soak: the bot fights whole Crucible runs end to end through the real Meta path (prepare →
        /// build → play → commit), across seeds and party strengths, with the battle invariants checked after every
        /// command. Every run must end (the climbing levels guarantee a wipe), clear at least the opening wave,
        /// and bank its haul.</summary>
        [TestCase(1, 1)]
        [TestCase(2, 3)]
        [TestCase(3, 6)]
        [TestCase(4, 10)]
        public void Bot_soak_over_a_crucible_run(int seed, int level)
        {
            var c = FreshReal();
            var s = Progression.NewGame(c, "Soak");
            s.HighestClearedStage = level - 1;
            foreach (var h in s.Roster) { h.TotalXp = Gaiden.Core.Rules.Formulas.TotalXpForLevel(level); }
            s.Crucible.Runs = seed;
            Assert.That(Crucible.CanLaunch(c, s, out var why), Is.True, why);
            var st = Crucible.Prepare(c, s);
            Assert.That(c.TryStage(st.Id), Is.SameAs(st));
            var setup = BattleFactory.Build(c, s, st.Id, Crucible.SeedFor(s));
            int gold = s.Gold;
            var waves = new List<int>();
            var b = BotRunner.Play(c, setup, new HeroBot(seed), 6000, (cmd, ev, state) =>
            {
                AssertInvariants(state);
                foreach (var e in ev) if (e is WaveStartedEvent w) waves.Add(w.Wave);
            });
            Assert.That(b.IsOver, Is.True, $"run did not end within the action cap (wave {b.Wave + 1})");
            Assert.That(b.Phase, Is.EqualTo(BattlePhase.Defeat), "an endless run ends in a wipe");
            Assert.That(waves, Is.EqualTo(Enumerable.Range(0, waves.Count).ToList()), "waves arrive back to back, in order");
            int coins = b.Results.Coins, xp = b.Results.Xp;
            var rep = BattleFactory.Commit(c, s, b);
            TestContext.Out.WriteLine($"seed {seed} Lv {level}: survived {rep.CrucibleWaves} waves, {b.Results.ActionsTaken} actions, +{rep.Gold}g, +{rep.XpEach} xp, {rep.Items.Sum(kv => kv.Value)} items, {rep.LevelUps.Count} level-ups");
            Assert.That(rep.CrucibleWaves, Is.GreaterThanOrEqualTo(1), "the party should clear at least the opening wave");
            Assert.That(rep.Gold, Is.GreaterThanOrEqualTo(coins));
            Assert.That(s.Gold, Is.GreaterThanOrEqualTo(gold + coins));
            Assert.That(rep.XpEach, Is.EqualTo(xp));
            Assert.That(xp, Is.GreaterThan(0));
            Assert.That(s.Crucible.Best, Is.EqualTo(rep.CrucibleWaves));
            foreach (var id in s.Party) Assert.That(s.Hero(id).Hp, Is.EqualTo(0));
            Assert.That(s.HighestClearedStage, Is.EqualTo(level - 1));
        }
    }
}
