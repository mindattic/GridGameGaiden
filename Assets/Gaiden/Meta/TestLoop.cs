using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// Test Mode (`-testmode`, GG-T93): the engine-free half of a bot-only loop that self-plays the whole
    /// mechanical cycle — launch a stage → autoplayed battle → results → camp (<see cref="CampRoutine"/>)
    /// → launch again, forever — on a throwaway save with no story, crawl or tutorial. The app side
    /// (GameApp.TestMode) only drives screens; every decision and the per-cycle log line live here so the
    /// dotnet tests can run the same loop headlessly.
    ///
    /// Difficulty follows the party (owner 2026-10-19): a test-mode save's stages take their recommended
    /// level from <see cref="Progression.PartyLevel"/> instead of the campaign index (see
    /// <see cref="Progression.StageRecommendedLevel"/>), and spawns stay max(authored, recommended), so
    /// enemies climb as the party levels and the loop keeps reflecting rising difficulty.
    /// </summary>
    public static class TestLoop
    {
        public const string SaveName = "Test Mode";
        /// <summary>Losses in a row on the highest unlocked expedition before stepping back one theme to grind.</summary>
        public const int LossesBeforeStepBack = 3;

        /// <summary>A throwaway save (the app keeps it on slot -1, which Persist never writes): the default small
        /// starting party (GG-T118; story recruits join headlessly in <see cref="TestCycle.Begin"/>), the tutorial
        /// marked done, every story crawl marked seen and every Camp NPC joined (GG-T119).</summary>
        public static SaveData NewSave(ContentDb c)
        {
            var s = Progression.NewGame(c, SaveName);
            s.TestMode = true;
            s.TutorialDone = true;
            foreach (var t in c.Campaign.Themes) s.SeenCrawls.Add(t.Id);
            // Every Camp NPC joins up front (GG-T119): Test Mode shows no story, and although TestCycle.Begin's headless
            // beats would bring them along the way, the loop should exercise repair, rest, buying and brewing from cycle 1.
            CampNpcs.JoinAll(s);
            return s;
        }

        /// <summary>The next stage to launch from camp: the active expedition's next battle; otherwise battle 1
        /// of the highest unlocked theme, stepping back one theme per <see cref="LossesBeforeStepBack"/>
        /// consecutive losses so a wall never stalls the loop (a lost expedition restarts its theme).</summary>
        public static string PickStage(ContentDb c, SaveData s, int consecutiveLosses = 0)
        {
            if (s.Expedition != null)
            {
                var active = Expeditions.Theme(c, s.Expedition.ThemeId);
                if (active != null && s.Expedition.Next < active.Stages.Count) return active.Stages[s.Expedition.Next];
            }
            var stages = c.CampaignStages();
            var unlocked = c.Campaign.Themes
                .Select(t => (theme: t, first: t.Stages.Count > 0 ? stages.FindIndex(x => x.Id == t.Stages[0]) : -1))
                .Where(x => x.first >= 0 && Progression.IsStageUnlocked(s, x.first))
                .OrderBy(x => x.first).Select(x => x.theme).ToList();
            if (unlocked.Count == 0) return stages.FirstOrDefault()?.Id;
            int back = Math.Min(unlocked.Count - 1, Math.Max(0, consecutiveLosses) / LossesBeforeStepBack);
            return unlocked[unlocked.Count - 1 - back].Stages[0];
        }

        /// <summary>The level range the stage's enemies will spawn at for this save — the same
        /// max(authored, recommended) + NG+ rule BattleRunner.Setup applies.</summary>
        public static (int min, int max) EnemyLevels(ContentDb c, SaveData s, string stageId)
        {
            var stage = c.Stage(stageId);
            int rec = Progression.StageRecommendedLevel(c, s, stage);
            var levels = stage.Waves.SelectMany(w => w.Enemies)
                .Select(sp => stage.IsTest ? sp.Level : Math.Max(sp.Level, rec) + s.NewGamePlus * BattleConfig.NgPlusLevelStep).ToList();
            return levels.Count == 0 ? (rec, rec) : (levels.Min(), levels.Max());
        }

        /// <summary>A run of at least this many cycles that never learned an ability fails the Test Mode check
        /// (GG-T100: "learn abilities" is part of what the owner wants Test Mode to prove). The harness
        /// (tools/harness.py testmode) applies the same rule to the logged camp lines.</summary>
        public const int CyclesThatMustLearnAnAbility = 6;

        /// <summary>Whether any camp action in the run was learning an ability (see <see cref="CampRoutine.LearnedMarker"/>).</summary>
        public static bool LearnedAnAbility(IEnumerable<string> campActions) => campActions.Any(a => a.Contains(CampRoutine.LearnedMarker));

        /// <summary>Crafting materials in a battle's loot.</summary>
        public static Dictionary<string, int> Materials(ContentDb c, BattleReport r) =>
            r.Items.Where(kv => c.TryItem(kv.Key)?.Type == ItemType.CraftingMaterial).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>One cycle's facts, gathered as the loop passes each step, rendered as one log line.</summary>
    public sealed class TestCycle
    {
        public int Number;
        public string StageId;
        public string StageName;
        public int PartyLevel;
        public int EnemyMin, EnemyMax;
        public BattleReport Report;
        public int PartyLevelAfter;
        public Dictionary<string, int> Materials = new Dictionary<string, int>();
        public int GoldBeforeCamp, GoldAfterCamp;
        public List<string> Camp = new List<string>();
        /// <summary>What the story beats before this stage did — Test Mode shows no story, but its recruits still
        /// join (GG-T118: the small starting party grows at story beats), so the beats are played headlessly.</summary>
        public List<string> Story = new List<string>();

        public static TestCycle Begin(ContentDb c, SaveData s, string stageId, int number)
        {
            var story = DialogueRunner.PlayPendingBeatsHeadless(c, s, stageId);
            var (min, max) = TestLoop.EnemyLevels(c, s, stageId);
            return new TestCycle { Number = number, StageId = stageId, StageName = c.Stage(stageId).Name, PartyLevel = Progression.PartyLevel(s), EnemyMin = min, EnemyMax = max, Story = story };
        }

        public void Battle(ContentDb c, SaveData s, BattleReport r)
        {
            Report = r;
            PartyLevelAfter = Progression.PartyLevel(s);
            Materials = TestLoop.Materials(c, r);
            GoldBeforeCamp = s.Gold;
        }

        /// <summary>Runs <see cref="CampRoutine"/> on the save and records what it did.</summary>
        public void RunCamp(ContentDb c, SaveData s)
        {
            GoldBeforeCamp = s.Gold;
            Camp = CampRoutine.Run(c, s);
            GoldAfterCamp = s.Gold;
        }

        public string Result => Report == null ? "?" : Report.Victory ? "VICTORY" : Report.Retreated ? "RETREAT" : Report.Escaped ? "ESCAPED" : "DEFEAT";

        public override string ToString()
        {
            var r = Report ?? new BattleReport();
            string enemy = EnemyMin == EnemyMax ? $"{EnemyMax}" : $"{EnemyMin}-{EnemyMax}";
            string mats = Materials.Count == 0 ? "none" : string.Join(", ", Materials.Select(kv => $"{kv.Key}x{kv.Value}"));
            string camp = Camp.Count == 0 ? "nothing to do" : string.Join("; ", Camp);
            string story = Story.Count == 0 ? "" : $"story: {string.Join("; ", Story)} | ";
            return $"[TestMode] cycle {Number} | stage {StageId} ({StageName}) | {story}party Lv {PartyLevel}->{PartyLevelAfter} | enemy Lv {enemy} | {Result} | " +
                   $"+{r.XpEach} XP each | +{r.Gold} gold | mats {mats} | camp gold {GoldBeforeCamp}->{GoldAfterCamp}: {camp}";
        }
    }
}
