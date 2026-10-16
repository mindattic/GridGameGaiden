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

        private Dictionary<string, Row> Campaign(float randomness, int seed, int maxAttemptsPerStage, out bool finished, bool manageCamp = false)
        {
            var c = Real;
            var s = Progression.NewGame(c, "Sim");
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
        [TestCase(0.0f, "greedy", false)]
        [TestCase(0.5f, "casual", false)]
        [TestCase(0.85f, "button-masher", false)]
        [TestCase(0.0f, "greedy+camp", true)]
        [TestCase(0.5f, "casual+camp", true)]
        public void Campaign_pacing_table(float randomness, string label, bool manageCamp)
        {
            var total = new Dictionary<string, Row>();
            int finishedRuns = 0;
            const int seeds = 4;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var rows = Campaign(randomness, seed, 4, out bool finished, manageCamp);
                if (finished) finishedRuns++;
                foreach (var kv in rows)
                {
                    if (!total.TryGetValue(kv.Key, out var t)) total[kv.Key] = t = new Row();
                    t.Runs += kv.Value.Runs; t.Wins += kv.Value.Wins; t.Actions += kv.Value.Actions; t.HpPct += kv.Value.HpPct;
                }
            }
            var sb = new StringBuilder();
            sb.AppendLine($"=== {label} bot (randomness {randomness}) — campaign finished {finishedRuns}/{seeds} ===");
            sb.AppendLine("stage                  win%   actions   hp-left%");
            foreach (var kv in total)
                sb.AppendLine($"{kv.Key,-22} {100 * kv.Value.Wins / kv.Value.Runs,4}%   {kv.Value.Actions / (float)kv.Value.Runs,7:F1}   {kv.Value.HpPct / kv.Value.Runs,6}%");
            TestContext.Out.WriteLine(sb.ToString());
            if (randomness == 0f) Assert.That(finishedRuns >= 1, Is.True, "a greedy bot should be able to finish the campaign");
        }
    }
}
