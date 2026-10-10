using System;
using System.Collections;
using System.Linq;
using Gaiden.Core.Content;
using Gaiden.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    /// <summary>Base for hub sub-screens: background art, header with back + gold, body column.</summary>
    public abstract class HubPage : GameScreen
    {
        protected Label GoldLabel;
        protected VisualElement Body;
        protected abstract string Title { get; }
        protected virtual string Backdrop => "greenvalley";

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background(Backdrop, 1));
            bg.style.unityBackgroundImageTintColor = new Color(0.45f, 0.5f, 0.7f, 1f);
            var col = Ui.El("fill col", root);
            Ui.Header(Title, () => App.Router.Back(), col, out GoldLabel);
            Body = Ui.El("col grow", col);
            Body.style.paddingLeft = Body.style.paddingRight = 10;
            Body.style.paddingBottom = 30;
            BuildBody(Body);
            UpdateGold();
        }

        protected abstract void BuildBody(VisualElement body);

        protected void UpdateGold()
        {
            if (GoldLabel != null && App.Save != null) GoldLabel.text = Ui.Gold(App.Save.Gold);
        }

        protected void Done(Result r, bool persist = true)
        {
            App.Toast(r.Message ?? (r.Ok ? "Done" : "Can't do that"));
            App.Audio.Sfx(r.Ok ? "ui.buy" : "ui.error");
            if (r.Ok && persist) App.Persist();
            Refresh();
        }

        protected ContentDb C => App.Content;
        protected SaveData S => App.Save;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Title
    // ─────────────────────────────────────────────────────────────────────────────

    public sealed class TitleScreen : GameScreen
    {
        public override string Id => "title";
        public override string Music => "TellerOfTheTales";

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background("cityruins", 0));
            var shade = Ui.El("fill", root);
            shade.style.backgroundColor = new Color(0, 0, 0.05f, 0.45f);

            var col = Ui.El("fill col center", root);
            var title = Ui.Text("GRIDGAME", "title shadow", col);
            Fonts.UseDisplay(title);
            var sub = Ui.Text("GAIDEN", "title shadow", col);
            Fonts.UseDisplay(sub);
            sub.style.fontSize = 150;
            sub.style.color = new Color(1f, 0.82f, 0.36f);
            var tag = Ui.Text("Slide. Flank. Strike.", "h2 shadow dim", col);
            tag.style.marginTop = 10;
            tag.style.marginBottom = 160;

            var menu = Ui.Window(null, col);
            menu.style.width = 760;
            int recent = App.Saves.MostRecent();
            Debug.Log($"[Title] most recent save slot: {recent}");
            if (recent >= 0) Ui.MenuRow("Continue", "", () => Continue(recent), menu);
            Ui.MenuRow("New Game", "", () => App.Router.Push(new SaveSlotScreen(true)), menu);
            Ui.MenuRow("Load Game", "", () => App.Router.Push(new SaveSlotScreen(false)), menu, null, recent >= 0);
            Ui.MenuRow("Credits", "", () => App.Router.Push(new CreditsScreen()), menu);
            Ui.SelectFirst(menu);

            var ver = Ui.Text($"v{Application.version}", "tiny dim", root);
            ver.style.position = Position.Absolute;
            ver.style.right = 30; ver.style.bottom = 24;
        }

        private void Continue(int slot)
        {
            Debug.Log($"[Title] continue slot {slot}");
            var s = App.Saves.Load(slot);
            if (s == null) { App.Toast("That save is damaged"); return; }
            App.Save = s; App.Slot = slot;
            App.ApplySettings();
            App.Router.ResetTo(new HubScreen());
        }
    }

    public sealed class SaveSlotScreen : GameScreen
    {
        private readonly bool newGame;
        public SaveSlotScreen(bool newGame) { this.newGame = newGame; }
        public override string Id => newGame ? "newgame" : "load";

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background("cityruins", 0));
            bg.style.unityBackgroundImageTintColor = new Color(0.4f, 0.4f, 0.55f);
            var col = Ui.El("fill col", root);
            Ui.Header(newGame ? "New Game" : "Load Game", () => App.Router.Back(), col, out _);
            var w = Ui.Window(newGame ? "Choose a slot" : "Choose a save", col);
            for (int i = 0; i < SaveStore.Slots; i++)
            {
                int slot = i;
                var s = App.Saves.Load(i);
                string label = s == null ? $"Slot {i + 1} — Empty" : $"Slot {i + 1} — {s.Name}";
                string value = s == null ? "" : $"Stage {s.HighestClearedStage + 2}  {Ui.Gold(s.Gold)}";
                var row = Ui.MenuRow(label, value, () => Pick(slot, s), w, null, newGame || s != null);
                if (s != null)
                {
                    var info = Ui.Text($"{s.Roster.Count} heroes · {TimeSpan.FromSeconds(s.PlaySeconds):h\\:mm} played · {s.UpdatedUtc.ToLocalTime():g}", "small dim", w);
                    info.style.marginLeft = 64; info.style.marginBottom = 14;
                }
            }
        }

        private void Pick(int slot, SaveData existing)
        {
            if (!newGame)
            {
                App.Save = existing; App.Slot = slot;
                App.ApplySettings();
                App.Router.ResetTo(new HubScreen());
                return;
            }
            if (existing != null)
            {
                Confirm.Show($"Overwrite slot {slot + 1}?", "Overwrite", () => Start(slot));
                return;
            }
            Start(slot);
        }

        private void Start(int slot)
        {
            App.Save = Progression.NewGame(App.Content, "Player");
            App.Slot = slot;
            App.ApplySettings();
            App.Persist();
            App.Router.ResetTo(new HubScreen());
        }
    }

    /// <summary>Modal confirm dialog on the overlay layer.</summary>
    public static class Confirm
    {
        public static void Show(string question, string yes, Action onYes, Action onNo = null)
        {
            var overlay = GameApp.I.Overlay;
            var veil = Ui.El("fill veil col center", overlay);
            veil.pickingMode = PickingMode.Position;
            var w = Ui.Window(null, veil);
            w.style.width = 860;
            var q = Ui.Text(question, "h2", w);
            q.style.whiteSpace = WhiteSpace.Normal;
            q.style.marginBottom = 24;
            var row = Ui.El("row", w);
            row.style.justifyContent = Justify.FlexEnd;
            Ui.Button("Cancel", () => { veil.RemoveFromHierarchy(); onNo?.Invoke(); }, null, row);
            Ui.Button(yes, () => { veil.RemoveFromHierarchy(); onYes?.Invoke(); }, "btn--primary", row);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Camp hub — the FF-style main menu (merged hub, bible §25.9)
    // ─────────────────────────────────────────────────────────────────────────────

    public sealed class HubScreen : GameScreen
    {
        public override string Id => "hub";
        public override string Music => "MinstrelGuild";

        protected override void Build(VisualElement root)
        {
            var c = App.Content; var s = App.Save;
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background(ThemeFor(c, s), 2));
            bg.style.unityBackgroundImageTintColor = new Color(0.55f, 0.6f, 0.75f);

            var col = Ui.El("fill col", root);
            col.style.paddingTop = 40; col.style.paddingLeft = col.style.paddingRight = 16; col.style.paddingBottom = 30;

            // Party window (portraits, level, HP): a 2-column grid so a full party of six fits above the menu.
            var party = Ui.Window(null, col);
            party.name = "camp-party";
            party.style.flexDirection = FlexDirection.Row;
            party.style.flexWrap = Wrap.Wrap;
            party.style.justifyContent = Justify.SpaceBetween;
            foreach (var id in s.Party)
            {
                var h = s.Hero(id);
                if (h == null) continue;
                var u = Progression.Preview(c, h);
                var card = Ui.El("card", party);
                card.name = "camp-hero-" + id;
                card.style.width = Length.Percent(49);
                card.style.marginTop = card.style.marginBottom = 5;
                card.style.paddingTop = card.style.paddingBottom = card.style.paddingLeft = card.style.paddingRight = 8;
                var thumb = Ui.Thumb(id, card, 104);
                thumb.style.marginRight = 12;
                var info = Ui.El("col grow", card);
                info.style.flexShrink = 1; info.style.minWidth = 0;
                var top = Ui.El("row", info);
                var name = Ui.Text(c.Class(id).Name, "small grow", top);
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis;
                Ui.Text($"Lv {u.Level}", "small gold", top);
                int hp = h.Hp > 0 ? Math.Min(h.Hp, u.MaxHp) : u.MaxHp;
                Ui.Gauge(hp / (float)u.MaxHp, info, 300).style.marginTop = 6;
                Ui.Text($"{hp}/{u.MaxHp}", "tiny dim", info);
                card.RegisterCallback<ClickEvent>(_ => App.Router.Push(new StatusScreen(id)));
            }

            var lower = Ui.El("row grow", col);
            lower.style.alignItems = Align.Stretch;

            var menu = Ui.Window(null, lower);
            menu.style.flexGrow = 1;
            var list = Ui.List(menu);
            void Item(string label, Func<GameScreen> screen, string value = "") => Ui.MenuRow(label, value, () => App.Router.Push(screen()), list.contentContainer);
            Item("Adventure", () => new StageSelectScreen(), $"Stage {Math.Min(s.HighestClearedStage + 2, c.CampaignStages().Count)}");
            Item("Items", () => new ItemsScreen());
            Item("Status", () => new StatusScreen(s.Party.FirstOrDefault()));
            Item("Equip", () => new EquipScreen(s.Party.FirstOrDefault()));
            Item("Abilities", () => new AbilitiesScreen(s.Party.FirstOrDefault()));
            Item("Party", () => new PartyScreen());
            Item("Merchant", () => new ShopScreen());
            Item("Blacksmith", () => new BlacksmithScreen());
            Item("Alchemist", () => new AlchemistScreen());
            Ui.MenuRow("Inn", Inn.Cost(c, s) > 0 ? Ui.Gold(Inn.Cost(c, s)) : "Rested", Rest, list.contentContainer);
            Item("Summon Circle", () => new SummonScreen());
            Item("Bounties", () => new BountyScreen(), Bounties.Active(c, s) != null ? $"{s.Bounty.Progress}/{Bounties.Active(c, s).Kills}" : "");
            Item("Bestiary", () => new BestiaryScreen());
            Item("Settings", () => new SettingsScreen());
            if (Progression.CanStartNewGamePlus(c, s))
                Ui.MenuRow($"New Game+ {s.NewGamePlus + 1}", "", () => Confirm.Show("Start New Game+? Keep your heroes, gear and gold; the campaign restarts with enemies 10 levels stronger.", "Begin", () =>
                {
                    var r = Progression.StartNewGamePlus(App.Content, App.Save);
                    App.Toast(r.Message);
                    if (r.Ok) { App.Persist(); Refresh(); }
                }), list.contentContainer).Q<Label>("label").AddToClassList("gold");
            Ui.MenuRow("Save & Quit", "", () => { App.Persist(); App.Save = null; App.Slot = -1; App.Router.ResetTo(new TitleScreen()); }, list.contentContainer);
            Ui.Select(list.contentContainer[0]);

            var side = Ui.El("col", lower);
            side.style.width = 330;
            var goldW = Ui.Window(null, side);
            Ui.Text("Gold", "small dim", goldW);
            Ui.Text(Ui.Gold(s.Gold), "h2 gold num", goldW);
            Ui.Text("Time", "small dim", goldW).style.marginTop = 16;
            Ui.Text(TimeSpan.FromSeconds(s.PlaySeconds).ToString(@"h\:mm\:ss"), "h2 num", goldW);
            if (s.NewGamePlus > 0) Ui.Text($"New Game+ {s.NewGamePlus}", "h2 gold", goldW).style.marginTop = 16;
            Ui.Text("Cleared", "small dim", goldW).style.marginTop = 16;
            Ui.Text($"{s.ClearedStages.Count(id => c.Stages.ContainsKey(id) && !c.Stage(id).IsTest)}/{c.CampaignStages().Count}", "h2 num", goldW);
        }

        private void Rest()
        {
            var r = Inn.Rest(App.Content, App.Save);
            App.Toast(r.Message);
            App.Audio.Sfx(r.Ok ? "heal" : "ui.error");
            if (r.Ok) { App.Persist(); Refresh(); }
        }

        public static string ThemeFor(ContentDb c, SaveData s)
        {
            var list = c.CampaignStages();
            if (list.Count == 0) return "greenvalley";
            var st = list[Mathf.Clamp(s.HighestClearedStage + 1, 0, list.Count - 1)];
            return st.Theme ?? "greenvalley";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Stage select (newest-on-top, replayable, drops hint — §22.3) + story crawl (§27)
    // ─────────────────────────────────────────────────────────────────────────────

    public sealed class StageSelectScreen : HubPage
    {
        public override string Id => "stages";
        protected override string Title => "Adventure";
        protected override string Backdrop => HubScreen.ThemeFor(C, S);

        protected override void BuildBody(VisualElement body)
        {
            var active = Bounties.Active(C, S);
            if (active != null)
            {
                var b = Ui.Window(null, body);
                Ui.Text($"Bounty: {active.Name} — {S.Bounty.Progress}/{active.Kills}", "small gold", b);
            }
            var w = Ui.Window(null, body);
            w.style.flexGrow = 1;
            var list = Ui.List(w);
            var stages = C.CampaignStages();
            int locked = 0;
            for (int i = stages.Count - 1; i >= 0; i--)
            {
                if (!Progression.IsStageUnlocked(S, i)) { locked++; continue; }
                var st = stages[i];
                int index = i;
                bool unlocked = Progression.IsStageUnlocked(S, i);
                bool cleared = S.ClearedStages.Contains(st.Id);
                var theme = C.ThemeOf(st.Id);
                string label = (cleared ? "★ " : unlocked ? "☆ " : "◇ ") + st.Name;
                var row = Ui.MenuRow(label, $"Lv {Progression.RecommendedLevel(i)}", () => Launch(st, index), list.contentContainer, null, unlocked);
                var drops = DropsHint(st);
                if (unlocked && drops.Length > 0)
                {
                    var d = Ui.Text($"{theme?.Name}  ·  {st.Waves.Count} waves  ·  drops: {drops}", "tiny dim", list.contentContainer);
                    d.style.marginLeft = 70; d.style.marginBottom = 8;
                }
            }
            if (locked > 0) Ui.MenuRow($"◇ {locked} more stage{(locked == 1 ? "" : "s")} ahead…", "", null, list.contentContainer, null, false);
            Ui.SelectFirst(list.contentContainer);
        }

        private string DropsHint(StageDef st)
        {
            var names = st.Waves.SelectMany(w => w.Enemies).Select(e => C.TryClass(e.Class)?.DropTable).Where(t => t != null && C.DropTables.ContainsKey(t))
                .SelectMany(t => C.DropTables[t].Entries).Select(e => C.TryItem(e.Item)?.Name).Where(n => n != null).Distinct().Take(3);
            return string.Join(", ", names);
        }

        private void Launch(StageDef st, int index) => StageLauncher.Launch(st.Id, false);
    }

    /// <summary>The one way into a battle: plays the theme's story crawl on first entry (§27), then the stage.</summary>
    public static class StageLauncher
    {
        public static void Launch(string stageId, bool replaceCurrent)
        {
            var app = GameApp.I;
            var theme = app.Content.ThemeOf(stageId);
            GameScreen next = new BattleScreen(stageId);
            if (theme != null && !string.IsNullOrEmpty(theme.Crawl) && app.Save != null && !app.Save.SeenCrawls.Contains(theme.Id))
            {
                app.Save.SeenCrawls.Add(theme.Id);
                app.Persist();
                next = new StoryCrawlScreen(theme, next);
            }
            if (replaceCurrent) app.Router.Replace(next); else app.Router.Push(next);
        }
    }

    public sealed class StoryCrawlScreen : GameScreen
    {
        private readonly ThemeDef theme;
        private readonly GameScreen next;
        private VisualElement text;
        private float y;
        private bool done;
        public StoryCrawlScreen(ThemeDef theme, GameScreen next) { this.theme = theme; this.next = next; }
        public override string Id => "crawl";
        public override string Music => "TellerOfTheTales";

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background(theme.Id, 0));
            bg.style.unityBackgroundImageTintColor = new Color(0.35f, 0.35f, 0.45f);
            var clip = Ui.El("fill", root);
            clip.style.overflow = Overflow.Hidden;
            text = Ui.El("col", clip);
            text.style.position = Position.Absolute;
            text.style.left = 90; text.style.right = 90;
            var h = Ui.Text(theme.Name, "h1 shadow gold", text);
            Fonts.UseDisplay(h);
            h.style.unityTextAlign = TextAnchor.MiddleCenter;
            h.style.marginBottom = 60;
            var body = Ui.Text(theme.Crawl, "h2 shadow", text);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.unityTextAlign = TextAnchor.UpperCenter;
            y = GameApp.DesignHeight;
            text.style.top = y;
            var skip = Ui.Button("Skip ›", Finish, "btn--small", root);
            skip.style.position = Position.Absolute; skip.style.right = 30; skip.style.bottom = 40;
            root.RegisterCallback<ClickEvent>(_ => Finish());
        }

        public override void Tick(float dt)
        {
            if (done || text == null) return;
            y -= dt * 95f;
            text.style.top = y;
            if (y < -text.layout.height - 100) Finish();
        }

        private void Finish()
        {
            if (done) return;
            done = true;
            App.Router.Replace(next);
        }
    }

    public sealed class CreditsScreen : GameScreen
    {
        public override string Id => "credits";
        protected override void Build(VisualElement root)
        {
            var col = Ui.El("fill col", root);
            Ui.Header("Credits", () => App.Router.Back(), col, out _);
            var w = Ui.Window(null, col);
            w.style.flexGrow = 1;
            var list = Ui.List(w);
            var text = Resources.Load<TextAsset>("Credits")?.text ?? "GridGame Gaiden — MindAttic";
            var l = Ui.Text(text, "small", list.contentContainer);
            l.style.whiteSpace = WhiteSpace.Normal;
        }
    }
}
