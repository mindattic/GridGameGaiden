using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Content;
using Gaiden.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    /// <summary>Results: victory/defeat/retreat, gold, loot, unlocks (bible §22). Level-ups are shown
    /// first, by LevelUpScreen — route through <see cref="RouteFrom"/> rather than constructing this directly.</summary>
    public sealed class PostBattleScreen : GameScreen
    {
        private readonly BattleReport report;
        private readonly string stageId;
        private readonly List<Coroutine> reveals = new List<Coroutine>();
        public PostBattleScreen(BattleReport report, string stageId) { this.report = report; this.stageId = stageId; }
        public override string Id => "postbattle";
        public override string Music => report.Victory ? "HeroicAge" : "Heartbreaking";

        public override void OnHidden()
        {
            foreach (var co in reveals) if (co != null) App.StopCoroutine(co);
            reveals.Clear();
        }

        /// <summary>Battle-end routing: a level-up fanfare first if anyone leveled, else straight to loot.</summary>
        public static void RouteFrom(BattleReport report, string stageId) =>
            App.Router.Replace(report.LevelUps.Count > 0 ? (GameScreen)new LevelUpScreen(report, stageId) : new PostBattleScreen(report, stageId));

        protected override void Build(VisualElement root)
        {
            var c = App.Content;
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background(c.Stage(stageId).Theme, 3));
            bg.style.unityBackgroundImageTintColor = new Color(0.45f, 0.45f, 0.55f);
            var col = Ui.El("fill col", root);
            col.style.paddingTop = 140; col.style.paddingLeft = col.style.paddingRight = 30; col.style.paddingBottom = 40;

            var title = Ui.Text(report.Victory ? "VICTORY" : report.Retreated ? "RETREATED" : report.Escaped ? "ESCAPED" : "DEFEAT", "title shadow", col);
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
                if (report.Items.Count == 0) Ui.Text("No items dropped.", "small dim", list);
                RevealItems(report.Items, c, list);
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
                var t = Ui.Text(report.Retreated ? "You fall back with part of your spoils — safer than risking the whole party. The stage is yours to retry." :
                    report.Escaped ? "The party slips away in the smoke. Wounds remain; no rewards." :
                    report.ExpeditionFailed ? $"The {report.ExpeditionName} expedition has failed. The party falls back to the caravan and recovers — the expedition restarts from battle 1." :
                    "The party retreats to camp and recovers. The gold and loot are lost — but every hero keeps the experience they earned. The stage is yours to retry.", "h2", list);
                t.style.whiteSpace = WhiteSpace.Normal;
                if (report.Retreated)
                {
                    Ui.MenuRow("Gold", "+" + Ui.Gold(report.Gold), null, list).Q<Label>("value").AddToClassList("gold");
                    RevealItems(report.Items, c, list);
                }
                if (!report.Escaped && report.XpEach > 0)
                    Ui.MenuRow("Hard-won experience", $"+{report.XpEach} each", null, list).name = "defeat-xp";
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
            {
                Ui.Button("Retreat early", () => Confirm.Show(
                    "Stop the expedition here? You keep everything you've earned so far — you just give up the completion bonus for pushing on.",
                    "Retreat early", () => { Expeditions.Abandon(App.Save); App.Persist(); App.Router.ResetTo(new HubScreen()); }), null, row);
                Ui.Button("Next battle ›", () => StageLauncher.Launch(report.NextStageId, true), "btn--primary", row).name = "next";
            }
            else if (report.Victory)
            {
                var stages = c.CampaignStages();
                int idx = stages.FindIndex(s => s.Id == stageId);
                if (idx >= 0 && idx + 1 < stages.Count && Progression.IsStageUnlocked(App.Save, idx + 1) && Expeditions.CanLaunch(c, App.Save, stages[idx + 1].Id, out _))
                    Ui.Button("Next expedition ›", () => StageLauncher.Launch(stages[idx + 1].Id, true), null, row).name = "next";
            }

            if (DemoMode.Active) root.schedule.Execute(() => DemoMode.AfterBattle(report, stageId)).ExecuteLater(1500);
        }

        /// <summary>Items pop in one at a time instead of dumping the whole list at once (owner direction
        /// 2026-10-11: the same tempo and pause that makes a results screen enjoyable to read).</summary>
        private void RevealItems(Dictionary<string, int> items, ContentDb c, VisualElement list)
        {
            int i = 0;
            foreach (var kv in items)
            {
                var it = c.TryItem(kv.Key);
                if (it == null) continue;
                var row = ItemUi.Row(it, $"×{kv.Value}", null, list);
                row.style.opacity = 0f;
                reveals.Add(App.StartCoroutine(RevealRow(row, i++ * 0.08f)));
            }
        }

        private IEnumerator RevealRow(VisualElement row, float delay)
        {
            yield return Tween.Wait(delay);
            row.style.opacity = 1f;
            yield return Tween.Pop(row, 1.12f, 0.18f);
        }
    }
}
