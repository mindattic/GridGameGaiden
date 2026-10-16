using System;
using System.IO;
using System.Linq;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Meta;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Gaiden.Tests.TestKit;

namespace Gaiden.Tests
{
    /// <summary>
    /// Battle records (§22.6). The check always runs; set GAIDEN_RECORDS=&lt;dir&gt; to also write a soak of bot-played
    /// first battles there (GAIDEN_RECORD_SEEDS = how many per bot, default 12) for balance analysis and ML.
    /// </summary>
    public class RecordTests
    {

        private static (BattleState state, JObject record) PlayRecorded(int seed, float randomness, string label, int maxActions = 700)
        {
            var c = Real;
            var save = Progression.NewGame(c, "Rec");
            var setup = BattleFactory.Build(c, save, c.CampaignStages()[0].Id, seed);
            var opening = new System.Collections.Generic.List<BattleEvent>();
            var s = BattleEngine.Create(c, setup, opening);
            var rec = new BattleRecorder(s, setup, label, opening);
            var bot = new HeroBot(seed, randomness);
            int refused = 0;
            for (int i = 0; i < maxActions && !s.IsOver; i++)
            {
                var ev = rec.Apply(s, bot.Decide(s));
                refused = ev.Exists(e => e is PlayerControlEvent || e is BattleEndedEvent) ? 0 : refused + 1;
                if (refused > 5) break;
            }
            return (s, rec.Finish(s));
        }

        [Test]
        public void A_recorded_battle_round_trips_as_json_with_every_turn()
        {
            var (s, rec) = PlayRecorded(3, 0f, "bot:greedy", 60);
            var parsed = JObject.Parse(rec.ToString());
            Assert.That((int)parsed["schema"], Is.EqualTo(BattleRecorder.SchemaVersion));
            Assert.That((int)parsed["waves"], Is.EqualTo(5));
            var turns = (JArray)parsed["turns"];
            Assert.That(turns.Count > 10, Is.True);
            var first = turns[0];
            Assert.That(first["before"]["units"], Is.Not.Null);
            Assert.That(first["command"]["type"], Is.Not.Null);
            Assert.That(((JArray)first["events"]).Count > 0, Is.True);
            foreach (var t in turns) Assert.That(t["after"]["heroHp"], Is.Not.Null);
            Assert.That(parsed["outcome"]["phase"], Is.Not.Null);
            foreach (var h in (JArray)parsed["party"]) Assert.That(((JArray)h["bar"]).Count(b => b.Type == JTokenType.Object && (string)b["item"] == "x_potion"), Is.EqualTo(1));
        }

        [Test]
        public void Record_soak_of_the_first_battle()
        {
            var dir = Environment.GetEnvironmentVariable("GAIDEN_RECORDS");
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("GAIDEN_RECORD_SEEDS"), out var n) ? n : 12;
            if (string.IsNullOrEmpty(dir)) { TestContext.Out.WriteLine("GAIDEN_RECORDS not set — nothing written"); return; }
            Directory.CreateDirectory(dir);
            foreach (var (label, randomness) in new[] { ("bot:greedy", 0f), ("bot:casual", 0.5f) })
                for (int seed = 1; seed <= seeds; seed++)
                {
                    var (s, rec) = PlayRecorded(seed, randomness, label);
                    var file = Path.Combine(dir, $"greenvalley-01_{label.Replace(':', '-')}_{seed:000}.json");
                    File.WriteAllText(file, rec.ToString(Newtonsoft.Json.Formatting.None));
                    TestContext.Out.WriteLine($"{label} seed {seed}: {s.Phase} at wave {s.Wave + 1}, {s.Results.ActionsTaken} actions");
                }
        }
    }
}
