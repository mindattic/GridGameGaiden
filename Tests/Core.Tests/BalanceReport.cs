using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Meta;
using NUnit.Framework;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>
    /// Balance instrument (bible §0.3 pacing targets): plays the campaign in order with natural progression —
    /// XP, carried wounds, inn rests when affordable, retries on defeat — for bots of different skill, and
    /// reports win rate, actions per battle and HP left. Asserts only the guard rails (no stalls; a strong
    /// bot can finish the campaign); the table is the tuning dashboard.
    /// </summary>
    public class BalanceReport
    {

        private sealed class Row { public int Runs, Wins, Actions, HpPct; }

        private Dictionary<string, Row> Campaign(float randomness, int seed, int maxAttemptsPerStage, out bool finished, bool manageCamp = false) =>
            Campaign(randomness, seed, maxAttemptsPerStage, out finished, out _, manageCamp, Progression.StartingRoster);

        private Dictionary<string, Row> Campaign(float randomness, int seed, int maxAttemptsPerStage, out bool finished, out SaveData save, bool manageCamp, IReadOnlyList<string> startingParty)
        {
            var c = Real;
            var s = save = Progression.NewGame(c, "Sim", startingParty);
            var rows = new Dictionary<string, Row>();
            var stages = c.CampaignStages();
            finished = true;
            int battleSeed = seed * 1000;
            foreach (var stage in stages)
            {
                bool cleared = false;
                for (int attempt = 0; attempt < maxAttemptsPerStage && !cleared; attempt++)
                {
                    if (manageCamp) CampRoutine.Run(c, s);
                    else if (Inn.Cost(c, s) > 0 && Inn.Cost(c, s) <= s.Gold) Inn.Rest(c, s);
                    var setup = BattleFactory.Build(c, s, stage.Id, battleSeed++);
                    var st = BotRunner.Play(c, setup, new HeroBot(battleSeed, randomness), 900); // the first battle is a 20-wave gauntlet
                    if (!rows.TryGetValue(stage.Id, out var row)) rows[stage.Id] = row = new Row();
                    row.Runs++;
                    row.Actions += st.Results.ActionsTaken;
                    var heroes = st.Units.Where(u => u.IsHero).ToList();
                    row.HpPct += (int)(100f * heroes.Sum(u => u.Alive ? u.Hp : 0) / heroes.Sum(u => u.MaxHp));
                    if (st.Phase == BattlePhase.Victory) { row.Wins++; cleared = true; }
                    BattleFactory.Commit(c, s, st);
                }
                if (!cleared) { finished = false; break; }
            }
            return rows;
        }
        /// <summary>The trio start (GG-T87) begins two short of <see cref="Progression.MaxPartySize"/> so the
        /// camp routine's Summon Circle step has an open party slot to fill — a full five-hero start (every
        /// real new game) never opens one, so recruiting would otherwise never enter the loop.</summary>
        [TestCase(0.0f, "greedy", false, 5)]
        [TestCase(0.5f, "casual", false, 5)]
        [TestCase(0.85f, "button-masher", false, 5)]
        [TestCase(0.0f, "greedy+camp", true, 5)]
        [TestCase(0.5f, "casual+camp", true, 5)]
        [TestCase(0.0f, "greedy+camp trio-start", true, 3)]
        public void Campaign_pacing_table(float randomness, string label, bool manageCamp, int startingHeroes)
        {
            var total = new Dictionary<string, Row>();
            int finishedRuns = 0, recruits = 0, bountiesClaimed = 0;
            const int seeds = 4;
            var party = Progression.StartingRoster.Take(startingHeroes).ToList();
            for (int seed = 1; seed <= seeds; seed++)
            {
                var rows = Campaign(randomness, seed, 4, out bool finished, out var save, manageCamp, party);
                recruits += save.Recruits;
                bountiesClaimed += save.Bounty.Claimed.Count;
                if (finished) finishedRuns++;
                foreach (var kv in rows)
                {
                    if (!total.TryGetValue(kv.Key, out var t)) total[kv.Key] = t = new Row();
                    t.Runs += kv.Value.Runs; t.Wins += kv.Value.Wins; t.Actions += kv.Value.Actions; t.HpPct += kv.Value.HpPct;
                }
            }
            var sb = new StringBuilder();
            sb.AppendLine($"=== {label} bot (randomness {randomness}) — campaign finished {finishedRuns}/{seeds} — recruits {recruits}, bounties claimed {bountiesClaimed} ===");
            sb.AppendLine("stage                  win%   actions   hp-left%");
            foreach (var kv in total)
                sb.AppendLine($"{kv.Key,-22} {100 * kv.Value.Wins / kv.Value.Runs,4}%   {kv.Value.Actions / (float)kv.Value.Runs,7:F1}   {kv.Value.HpPct / kv.Value.Runs,6}%");
            TestContext.Out.WriteLine(sb.ToString());
            if (randomness == 0f) Assert.That(finishedRuns >= 1, Is.True, "a greedy bot should be able to finish the campaign");
            // GG-T87: the camp routine must actually drive the Bounty Board and (given an open slot) the
            // Summon Circle as part of organic progression, not just leave them untouched.
            // (Only a run that finishes is guaranteed to pass a contract's last target — a bot that stalls early may not.)
            if (manageCamp && finishedRuns > 0) Assert.That(bountiesClaimed > 0, Is.True, "the camp routine never claimed a bounty over a played-out campaign");
            if (manageCamp && startingHeroes < Progression.MaxPartySize) Assert.That(recruits > 0, Is.True, "the camp routine never recruited into an open party slot");
        }
    }
}
