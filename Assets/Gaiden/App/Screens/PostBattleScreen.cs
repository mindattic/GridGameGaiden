using System.Linq;
using Gaiden.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    /// <summary>Results: victory/defeat, gold, XP, level-ups, loot, unlocks (bible §22).</summary>
    public sealed class PostBattleScreen : GameScreen
    {
        private readonly BattleReport report;
        private readonly string stageId;
        public PostBattleScreen(BattleReport report, string stageId) { this.report = report; this.stageId = stageId; }
        public override string Id => "postbattle";
        public override string Music => report.Victory ? "HeroicAge" : "Heartbreaking";

        protected override void Build(VisualElement root)
        {
            var c = App.Content;
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background(c.Stage(stageId).Theme, 3));
            bg.style.unityBackgroundImageTintColor = new Color(0.45f, 0.45f, 0.55f);
            var col = Ui.El("fill col", root);
            col.style.paddingTop = 140; col.style.paddingLeft = col.style.paddingRight = 30; col.style.paddingBottom = 40;

            var title = Ui.Text(report.Victory ? "VICTORY" : report.Escaped ? "ESCAPED" : "DEFEAT", "title shadow", col);
            Fonts.UseDisplay(title);
            title.style.color = report.Victory ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.45f, 0.45f);
            Ui.Text(c.Stage(stageId).Name, "h2 shadow dim", col).style.unityTextAlign = TextAnchor.MiddleCenter;

            var w = Ui.Window(null, col);
            w.style.marginTop = 40;
            w.style.flexGrow = 1;
            var list = Ui.List(w).contentContainer;
            if (report.Victory)
            {
                Ui.MenuRow("Gold", "+" + Ui.Gold(report.Gold), null, list).Q<Label>("value").AddToClassList("gold");
                Ui.MenuRow("Experience", $"+{report.XpEach} each", null, list);
                foreach (var (cls, from, to) in report.LevelUps)
                    Ui.MenuRow($"{c.Class(cls).Name} — LEVEL UP!", $"Lv {from} → {to}", null, list).Q<Label>("label").AddToClassList("good");
                if (report.Items.Count == 0) Ui.Text("No items dropped.", "small dim", list);
                foreach (var kv in report.Items)
                {
                    var it = c.TryItem(kv.Key);
                    if (it != null) ItemUi.Row(it, $"×{kv.Value}", null, list);
                }
                if (report.UnlockedStage != null) Ui.MenuRow("New stage unlocked", report.UnlockedStage, null, list).Q<Label>("value").AddToClassList("gold");
                if (report.NewSlots > 0) Ui.MenuRow("Ability slot unlocked!", $"+{report.NewSlots}", null, list).Q<Label>("label").AddToClassList("good");
                if (report.BountyProgress != null) Ui.MenuRow("Bounty", report.BountyProgress, null, list);
                if (report.AutoSold > 0) Ui.MenuRow("Bag full — sold extras", $"{report.AutoSold} items", null, list);
                foreach (var g in report.GuestsJoined)
                    Ui.MenuRow($"{c.Class(g).Name} joins the caravan!", "New hero", null, list).Q<Label>("label").AddToClassList("good");
                if (report.CampaignComplete) Ui.MenuRow("Campaign complete!", "New Game+ unlocked at camp", null, list).Q<Label>("label").AddToClassList("gold");
                if (report.ExpeditionComplete)
                {
                    Ui.MenuRow($"{report.ExpeditionName} expedition complete!", "+" + Ui.Gold(report.ExpeditionBonus), null, list).Q<Label>("label").AddToClassList("gold");
                    Ui.Text("The caravan makes camp — everyone rests.", "small good", list);
                }
                else if (report.ExpeditionName != null)
                {
                    var er = Ui.MenuRow($"Expedition: battle {report.ExpeditionStep} of {report.ExpeditionLength} won", $"{report.CarriedOrbs} orbs carry over", null, list);
                    er.name = "expedition-progress";
                    Ui.Text("Wounds carry to the next battle; the inn stays closed until the expedition ends.", "tiny dim", list).style.whiteSpace = WhiteSpace.Normal;
                }
            }
            else
            {
                var t = Ui.Text(report.Escaped ? "The party slips away in the smoke. Wounds remain; no rewards." :
                    report.ExpeditionFailed ? $"The {report.ExpeditionName} expedition has failed. The party falls back to the caravan and recovers — the expedition restarts from battle 1." :
                    "The party retreats to camp and recovers. No rewards this time — but the stage is yours to retry.", "h2", list);
                t.style.whiteSpace = WhiteSpace.Normal;
            }

            foreach (var g in report.GuestsLost)
                Ui.MenuRow($"{c.Class(g).Name} fell in battle", "Lost forever", null, list).Q<Label>("label").AddToClassList("bad");
            var row = Ui.El("row", col);
            row.style.justifyContent = Justify.Center;
            if (!report.Victory)
            {
                // A failed expedition restarts from its first battle.
                var theme = c.ThemeOf(stageId);
                string retryId = report.ExpeditionFailed && theme != null ? theme.Stages[0] : stageId;
                Ui.Button("Retry", () => StageLauncher.Launch(retryId, true), null, row);
            }
            var cont = Ui.Button("Continue", () => App.Router.ResetTo(new HubScreen()), "btn--primary", row);
            cont.name = "continue";
            if (report.Victory && report.NextStageId != null)
                Ui.Button("Next battle ›", () => StageLauncher.Launch(report.NextStageId, true), "btn--primary", row).name = "next";
            else if (report.Victory)
            {
                var stages = c.CampaignStages();
                int idx = stages.FindIndex(s => s.Id == stageId);
                if (idx >= 0 && idx + 1 < stages.Count && Progression.IsStageUnlocked(App.Save, idx + 1) && Expeditions.CanLaunch(c, App.Save, stages[idx + 1].Id, out _))
                    Ui.Button("Next expedition ›", () => StageLauncher.Launch(stages[idx + 1].Id, true), null, row).name = "next";
            }
        }
    }
}
