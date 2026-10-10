using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Bot;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;
using Gaiden.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    /// <summary>
    /// The battle. Layout follows the bible HUD rows (§9): top bar · timeline + shield · action banner ·
    /// 6×8 board · ability bar · mana orbs · actor panel. The simulation (BattleState) is the only truth;
    /// this screen turns input into commands and plays the resulting events back.
    /// </summary>
    public sealed partial class BattleScreen : GameScreen
    {
        public override string Id => "battle";
        public override string Music => "Crusade";

        // Layout (design pixels)
        private const float BoardLeft = 45f, BoardTop = 450f;
        private const float TimelineLeft = 40f, TimelineWidth = 940f, TimelineY = 250f;

        public BattleState State { get; private set; }
        public bool Busy => playing || dragging;
        public int SelectedHeroId { get; private set; } = -1;
        public int TargetingSlot { get; private set; } = -1;
        public bool AutoPlay { get; set; }

        private readonly string stageId;
        private readonly BattleSetup setupOverride;
        private BoardView board;
        private VisualElement timelineBar, timelineIcons, zone;
        private readonly Dictionary<int, VisualElement> timelineIconViews = new Dictionary<int, VisualElement>();
        private readonly List<VisualElement> slotViews = new List<VisualElement>();
        private readonly List<VisualElement> orbViews = new List<VisualElement>();
        private VisualElement panel;
        private VisualElement feed;
        private Label banner, waveLabel, coinLabel, timerLabel;
        private VisualElement shake;
        private bool playing;
        private bool ended;
        private HeroBot autoBot;

        // First-battles hint: after a pause, show the move the bot would make (§29 tutorial question → contextual hints).
        private float idle;
        private bool hintShown;
        private const float HintDelay = 5f;
        private bool HintsEnabled => App.Save == null || App.Save.HighestClearedStage < 2;

        private int slotPress = -1;
        private float slotPressTime;
        private bool slotInfoShown;

        // drag state
        private bool pressing, dragging;
        private int dragHeroId = -1;
        private Vector2 pressPos;
        private Coord dragTile;
        private readonly List<Coord> dragPath = new List<Coord>();
        private MovePreview preview;
        private float dragTimeLeft;

        public BattleScreen(string stageId) { this.stageId = stageId; }
        public BattleScreen(BattleSetup setup) { setupOverride = setup; stageId = setup.Stage.Id; }

        private bool Colorblind => App.Save?.Settings.ColorblindMode ?? false;
        private bool ReduceMotion => App.Save?.Settings.ReduceMotion ?? false;

        protected override void Build(VisualElement root)
        {
            var initial = new List<BattleEvent>();
            if (State == null)
            {
                var setup = setupOverride ?? BattleFactory.Build(App.Content, App.Save, stageId, Environment.TickCount);
                State = BattleEngine.Create(App.Content, setup, initial);
            }

            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background(State.Stage.Theme, Math.Abs(stageId.GetHashCode()) % 4));
            bg.style.unityBackgroundImageTintColor = new Color(0.55f, 0.55f, 0.65f);
            shake = Ui.El("fill", root);

            BuildTopBar(shake);
            BuildTimeline(shake);
            banner = Ui.Text("", "h2 shadow", shake);
            banner.style.position = Position.Absolute;
            banner.style.left = 0; banner.style.right = 0; banner.style.top = 368; banner.style.height = 70;
            banner.style.unityTextAlign = TextAnchor.MiddleCenter;

            var boardHost = Ui.El("", shake);
            boardHost.style.position = Position.Absolute;
            boardHost.style.left = BoardLeft; boardHost.style.top = BoardTop;
            board = new BoardView(boardHost);
            board.Root.pickingMode = PickingMode.Position;
            board.Root.RegisterCallback<PointerDownEvent>(OnPointerDown);
            board.Root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            board.Root.RegisterCallback<PointerUpEvent>(OnPointerUp);
            board.Root.RegisterCallback<PointerCaptureOutEvent>(_ => { if (dragging) CommitDrag(); pressing = false; });

            feed = Ui.El("col", shake);
            feed.name = "feed";
            feed.pickingMode = PickingMode.Ignore;
            feed.style.position = Position.Absolute;
            feed.style.left = BoardLeft + 14; feed.style.top = BoardTop + BoardView.Tile * BattleConfig.Rows - 140;
            feed.style.width = 640;
            feed.style.backgroundColor = new Color(0f, 0f, 0.05f, 0.35f);
            feed.style.borderTopRightRadius = 12;
            feed.style.justifyContent = Justify.FlexEnd;

            BuildAbilityBar(shake);
            BuildOrbs(shake);
            panel = Ui.Window(null, shake, "window--compact");
            panel.style.position = Position.Absolute;
            panel.style.left = 20; panel.style.right = 20; panel.style.top = 2250; panel.style.bottom = 20;
            panel.style.marginTop = panel.style.marginBottom = 0;
            panel.style.paddingTop = panel.style.paddingBottom = 14;

            foreach (var u in State.Units) if (u.Alive) board.Add(u);
            SelectedHeroId = State.Units.FirstOrDefault(u => u.IsHero && u.Alive)?.Id ?? -1;
            SyncAll();
            App.StartCoroutine(Play(initial, false));
            if (CommandLine.Has("-autoplay")) AutoPlay = true;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // HUD construction
        // ─────────────────────────────────────────────────────────────────────────

        private void BuildTopBar(VisualElement root)
        {
            var bar = Ui.El("row", root);
            bar.style.position = Position.Absolute;
            bar.style.left = 20; bar.style.right = 20; bar.style.top = 30; bar.style.height = 110;
            Ui.Button("II", OpenMenu, "btn--small btn--icon", bar).name = "menu";
            var mid = Ui.El("col grow", bar);
            mid.style.alignItems = Align.Center;
            Ui.Text(State.Stage.Name, "h2 shadow", mid);
            waveLabel = Ui.Text("", "small shadow dim", mid);
            coinLabel = Ui.Text("", "h2 gold shadow", bar);
            coinLabel.style.width = 200; coinLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            timerLabel = Ui.Text("", "h1 shadow warn", root);
            timerLabel.style.position = Position.Absolute;
            timerLabel.style.right = 40; timerLabel.style.top = 368;
        }

        private void BuildTimeline(VisualElement root)
        {
            var frame = Ui.El("", root);
            frame.style.position = Position.Absolute;
            frame.style.left = TimelineLeft; frame.style.top = TimelineY - 100; frame.style.width = TimelineWidth; frame.style.height = 200;
            timelineBar = Ui.El("", frame);
            timelineBar.style.position = Position.Absolute;
            timelineBar.style.left = 0; timelineBar.style.right = 0; timelineBar.style.top = 92; timelineBar.style.height = 16;
            timelineBar.style.backgroundColor = new Color(0.9f, 0.92f, 1f, 0.85f);
            timelineBar.style.borderTopLeftRadius = timelineBar.style.borderTopRightRadius = timelineBar.style.borderBottomLeftRadius = timelineBar.style.borderBottomRightRadius = 8;
            zone = Ui.El("", frame);
            zone.style.position = Position.Absolute;
            zone.style.left = Length.Percent((1f - BattleConfig.ZoneU) * 100f); zone.style.right = 0;
            zone.style.top = 8; zone.style.bottom = 8;
            zone.style.backgroundColor = new Color(1f, 0.55f, 0.2f, 0.18f);
            zone.style.borderLeftWidth = 4; zone.style.borderLeftColor = new Color(1f, 0.6f, 0.25f, 0.9f);
            var act = Ui.Text("ACT", "tiny warn shadow", frame);
            act.style.position = Position.Absolute; act.style.right = -6; act.style.top = -22;
            timelineIcons = Ui.El("", frame);
            timelineIcons.style.position = Position.Absolute;
            timelineIcons.style.left = timelineIcons.style.top = timelineIcons.style.right = timelineIcons.style.bottom = 0;

            var shield = Ui.Button("GUARD", () => { if (CanCommand()) Commit(new ShieldCommand()); }, "btn--icon", root);
            shield.name = "shield";
            shield.style.position = Position.Absolute;
            shield.style.right = 18; shield.style.top = TimelineY - 64; shield.style.width = 128; shield.style.height = 128;
            shield.style.fontSize = 30;
        }

        private void BuildAbilityBar(VisualElement root)
        {
            var bar = Ui.El("row", root);
            bar.name = "abilitybar";
            bar.style.position = Position.Absolute;
            bar.style.left = 25; bar.style.right = 25; bar.style.top = BoardTop + BoardView.Tile * BattleConfig.Rows + 20; bar.style.height = 196;
            bar.style.justifyContent = Justify.SpaceBetween;
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var slot = Ui.El("col center", bar);
                slot.name = "slot" + i;
                slot.style.width = 216; slot.style.height = 196;
                slot.style.borderTopLeftRadius = slot.style.borderTopRightRadius = slot.style.borderBottomLeftRadius = slot.style.borderBottomRightRadius = 18;
                slot.style.borderTopWidth = slot.style.borderBottomWidth = slot.style.borderLeftWidth = slot.style.borderRightWidth = 4;
                slot.style.backgroundImage = new StyleBackground(Art.Window);
                slot.RegisterCallback<PointerDownEvent>(_ => { slotPress = idx; slotPressTime = Time.unscaledTime; slotInfoShown = false; });
                slot.RegisterCallback<PointerLeaveEvent>(_ => { if (slotPress == idx) slotPress = -1; });
                slot.RegisterCallback<ClickEvent>(_ =>
                {
                    bool wasInfo = slotInfoShown;
                    slotPress = -1; slotInfoShown = false;
                    if (wasInfo) { SyncPanel(); return; }
                    OnSlot(idx);
                });
                var icon = Ui.El("menu-icon", slot); icon.name = "icon"; icon.style.marginRight = 0; icon.style.width = icon.style.height = 76;
                var name = Ui.Text("", "tiny", slot); name.name = "name"; name.style.unityTextAlign = TextAnchor.MiddleCenter; name.style.whiteSpace = WhiteSpace.Normal; name.style.maxWidth = 200;
                var cost = Ui.El("row", slot); cost.name = "cost"; cost.style.height = 36;
                slotViews.Add(slot);
            }
        }

        private void BuildOrbs(VisualElement root)
        {
            var row = Ui.El("row", root);
            row.name = "orbs";
            row.style.position = Position.Absolute;
            row.style.left = 40; row.style.right = 40; row.style.top = BoardTop + BoardView.Tile * BattleConfig.Rows + 228; row.style.height = 76;
            row.style.justifyContent = Justify.SpaceBetween;
            int cap = Mathf.Clamp(State.Bank.Capacity, ManaBank.DefaultCapacity, 16);
            float size = cap <= 12 ? 74 : Mathf.Floor(1080f / cap) - 6;
            for (int i = 0; i < cap; i++)
            {
                var o = Ui.Text("", "", row);
                o.name = "orb" + i;
                o.style.width = o.style.height = size;
                o.style.backgroundImage = new StyleBackground(Art.Orb);
                o.style.unityTextAlign = TextAnchor.MiddleCenter;
                o.style.fontSize = 30; o.style.unityFontStyleAndWeight = FontStyle.Bold;
                o.style.color = new Color(0.08f, 0.08f, 0.12f);
                orbViews.Add(o);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Sync views ← state (the safety net after every playback)
        // ─────────────────────────────────────────────────────────────────────────

        public void SyncAll()
        {
            foreach (var u in State.Units)
            {
                if (!board.Units.TryGetValue(u.Id, out var v))
                {
                    if (!u.Alive) continue;
                    v = board.Add(u);
                }
                if (!u.Alive)
                {
                    if (!v.Dead) { v.Root.RemoveFromHierarchy(); v.Dead = true; }
                    continue;
                }
                if (v.Dead) { board.UnitLayer.Add(v.Root); v.Dead = false; }
                board.Place(v, u.Pos);
                v.Root.style.opacity = 1; v.Root.style.scale = new Scale(Vector2.one); v.Root.style.translate = new Translate(0, 0);
                v.SetHp(u.Hp, u.MaxHp);
                v.SetBuffs(u, Colorblind);
                v.SetCountdown(u.IsHero ? -1 : BattleEngine.ActionsUntilTurn(State, u.Id));
                bool sel = u.Id == SelectedHeroId;
                v.SetGlow(sel ? new Color(1f, 0.9f, 0.5f) : Color.white, sel ? 0.65f : 0f);
            }
            board.SyncTraps(State);
            SyncTimeline(null);
            SyncOrbs();
            SyncBar();
            SyncPanel();
            int wave = Math.Min(State.Wave + 1, State.Stage.Waves.Count);
            waveLabel.text = $"Wave {wave}/{State.Stage.Waves.Count}";
            coinLabel.text = $"{State.Results.Coins} G";
        }

        private void SyncTimeline(Dictionary<int, float> uOverride)
        {
            var live = new HashSet<int>();
            foreach (var e in State.Timeline)
            {
                live.Add(e.Id);
                if (!timelineIconViews.TryGetValue(e.Id, out var icon))
                {
                    icon = MakeTimelineIcon(e);
                    timelineIconViews[e.Id] = icon;
                    timelineIcons.Add(icon);
                }
                float u = uOverride != null && uOverride.TryGetValue(e.Id, out var ou) ? ou : e.U;
                PlaceIcon(icon, e.Kind, u);
            }
            StaggerIcons();
            foreach (var kv in timelineIconViews.ToList())
                if (!live.Contains(kv.Key)) { kv.Value.RemoveFromHierarchy(); timelineIconViews.Remove(kv.Key); }
        }

        private VisualElement MakeTimelineIcon(TimelineEntry e)
        {
            var owner = State.UnitById(e.OwnerId);
            var icon = new VisualElement { name = "tl-" + e.Id, pickingMode = PickingMode.Ignore };
            icon.style.position = Position.Absolute;
            if (e.Kind == TimelineKind.Turn)
            {
                icon.style.width = icon.style.height = 84;
                icon.style.top = 2;
                icon.style.backgroundImage = new StyleBackground(Art.Thumb(owner?.Def.Id));
                icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                SetBorder(icon, 42, 4, owner != null && (owner.Def.HasTag("Boss") || owner.IsMultiTile) ? new Color(0.86f, 0.4f, 1f) : new Color(0.92f, 0.3f, 0.3f));
            }
            else
            {
                var a = App.Content.TryAbility(e.AbilityId);
                icon.style.width = icon.style.height = 56;
                icon.style.top = 126;
                var c = e.CasterTeam == Team.Hero ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.45f, 0.35f);
                SetBorder(icon, 28, 4, c);
                icon.style.backgroundColor = new Color(0.08f, 0.1f, 0.3f);
                var tex = Art.Icon(a?.Icon);
                if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain); }
            }
            return icon;
        }

        private void PlaceIcon(VisualElement icon, TimelineKind kind, float u)
        {
            float w = kind == TimelineKind.Turn ? 84 : 56;
            icon.style.left = Mathf.Clamp01(u) * TimelineWidth - w / 2f;
        }

        /// <summary>Icons that would overlap step upward (turns) / downward (casts) so every one stays readable.</summary>
        private void StaggerIcons()
        {
            var placed = new List<(float x, int lane, TimelineKind kind)>();
            foreach (var e in State.Timeline.OrderBy(x => x.U))
            {
                if (!timelineIconViews.TryGetValue(e.Id, out var icon)) continue;
                float x = e.U * TimelineWidth;
                int lane = 0;
                while (placed.Any(p => p.kind == e.Kind && p.lane == lane && Mathf.Abs(p.x - x) < (e.Kind == TimelineKind.Turn ? 70 : 46))) lane++;
                placed.Add((x, lane, e.Kind));
                // Alternate small offsets around the lane (up, down, further up…) so the stack never reaches the title.
                int offset = lane == 0 ? 0 : (lane % 2 == 1 ? -1 : 1) * ((lane + 1) / 2) * 20;
                icon.style.top = e.Kind == TimelineKind.Turn ? 2 + offset : 126 + lane * 22;
                icon.style.left = Mathf.Clamp01(e.U) * TimelineWidth - (e.Kind == TimelineKind.Turn ? 42 : 28) + (lane > 0 ? (lane % 2 == 1 ? 14 : -14) : 0);
            }
        }

        private static void SetBorder(VisualElement e, float radius, float width, Color c)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        }

        private void SyncOrbs()
        {
            var orbs = State.Bank.Orbs;
            for (int i = 0; i < orbViews.Count; i++)
            {
                var o = (Label)orbViews[i];
                if (i < orbs.Count)
                {
                    o.style.unityBackgroundImageTintColor = Art.ManaColorOf(orbs[i], Colorblind);
                    o.text = ManaBank.Letter(orbs[i]).ToString();
                    o.style.opacity = 1f;
                }
                else
                {
                    o.style.unityBackgroundImageTintColor = new Color(0.22f, 0.25f, 0.38f, 0.85f);
                    o.text = "";
                    o.style.opacity = 1f;
                }
            }
        }

        private void SyncBar()
        {
            var hero = State.UnitById(SelectedHeroId);
            for (int i = 0; i < slotViews.Count; i++)
            {
                var v = slotViews[i];
                var icon = v.Q("icon");
                var name = v.Q<Label>("name");
                var cost = v.Q("cost");
                cost.Clear();
                icon.style.backgroundImage = StyleKeyword.None;
                var status = hero == null ? SlotStatus.CannotAct : BattleEngine.GetSlotStatus(State, hero.Id, i);
                var border = new Color(0.9f, 0.92f, 1f, 0.75f);
                float alpha = 1f;
                if (hero == null || i >= hero.UnlockedSlots)
                {
                    name.text = hero == null ? "" : "Locked";
                    alpha = 0.35f;
                }
                else if (i >= hero.Bar.Count || hero.Bar[i].IsEmpty)
                {
                    name.text = "—";
                    alpha = 0.5f;
                }
                else
                {
                    var sl = hero.Bar[i];
                    var a = BattleEngine.SlotAbility(State, hero, i);
                    if (sl.IsWeapon)
                    {
                        var w = App.Content.TryItem(sl.WeaponId);
                        name.text = $"⭾ {w?.Name}";
                        border = new Color(0.8f, 0.8f, 0.9f);
                        icon.style.backgroundImage = new StyleBackground(Art.Orb);
                    }
                    else if (sl.IsItem)
                    {
                        var item = App.Content.TryItem(sl.ItemId);
                        name.text = $"{item?.Name}\n{sl.Charges}/{sl.MaxCharges}";
                        border = new Color(0.86f, 0.62f, 0.36f);
                        icon.style.backgroundImage = new StyleBackground(Art.Icon(a?.Icon) ?? Art.Orb);
                    }
                    else if (a != null)
                    {
                        name.text = a.Name;
                        icon.style.backgroundImage = new StyleBackground(Art.Icon(a.Icon) ?? Art.Orb);
                        if (a.Kind == AbilityKind.Skill)
                        {
                            border = new Color(0.4f, 0.9f, 0.5f);
                            if (status == SlotStatus.Cooldown) name.text = $"{a.Name}\nCD {hero.Cooldowns[a.Id]}";
                        }
                        else
                        {
                            border = new Color(0.45f, 0.75f, 1f);
                            Ui.CostPips(a.Cost, cost, 30);
                        }
                    }
                    if (status == SlotStatus.Silenced) { border = new Color(1f, 0.25f, 0.25f); name.text += "\nSilenced"; }
                    if (status != SlotStatus.Ready) alpha = 0.45f;
                }
                if (i == TargetingSlot) { border = new Color(1f, 0.85f, 0.3f); v.style.scale = new Scale(new Vector2(1.06f, 1.06f)); }
                else v.style.scale = new Scale(Vector2.one);
                v.style.borderTopColor = v.style.borderBottomColor = v.style.borderLeftColor = v.style.borderRightColor = border;
                v.style.opacity = alpha;
            }
        }

        private int panelUnitId = -1;

        private void SyncPanel()
        {
            panel.Clear();
            if (TargetingSlot >= 0 && State.UnitById(SelectedHeroId) is Unit caster)
            {
                var ab = BattleEngine.SlotAbility(State, caster, TargetingSlot);
                if (ab != null) { AbilityInfo(ab); return; }
            }
            var u = State.UnitById(panelUnitId >= 0 ? panelUnitId : SelectedHeroId);
            if (u == null || !u.Alive) u = State.UnitById(SelectedHeroId);
            if (u == null) return;
            var row = Ui.El("row", panel);
            Ui.Thumb(u.Def.Id, row, 200);
            var col = Ui.El("col grow", row);
            var top = Ui.El("row", col);
            Ui.Text(u.Name, "h2 grow", top);
            Ui.Text($"Lv {u.Level}", "h2 gold", top);
            bool reveal = u.IsHero || State.Results.Scanned.Contains(u.Def.Id) || (App.Save?.Bestiary.TryGetValue(u.Def.Id, out var be) == true && be.Scanned);
            var hpRow = Ui.El("row", col);
            Ui.Gauge(u.HpFraction, hpRow, 460);
            Ui.Text(reveal ? $"  {u.Hp}/{u.MaxHp}" : "  ???", "small", hpRow);
            if (reveal) Ui.Text(Ui.StatLine(u.Effective), "tiny dim", col);
            if (!u.IsHero)
            {
                int n = BattleEngine.ActionsUntilTurn(State, u.Id);
                var cast = State.CastEntryOf(u.Id);
                Ui.Text(cast != null ? $"Charging {App.Content.TryAbility(cast.AbilityId)?.Name}!" : n < 0 ? "Frozen in time" : n == 0 ? "Acts after your next move!" : $"Acts in {n} moves", "small warn", col);
            }
            if (u.Buffs.Count > 0)
                Ui.Text(string.Join("  ", u.Buffs.Select(b => $"{b.Def.Name} {b.Remaining}")), "tiny", col);
        }

        private void AbilityInfo(AbilityDef a)
        {
            var row = Ui.El("row", panel);
            var icon = Ui.El("menu-icon", row);
            icon.style.width = icon.style.height = 120;
            icon.style.backgroundImage = new StyleBackground(Art.Icon(a.Icon) ?? Art.Orb);
            var col = Ui.El("col grow", row);
            var top = Ui.El("row", col);
            Ui.Text(a.Name, "h2 grow", top);
            if (!string.IsNullOrEmpty(a.Cost)) Ui.CostPips(a.Cost, top, 40);
            string timing = a.Kind == AbilityKind.Skill ? (a.CooldownTurns > 0 ? $"Skill · recharges in {a.CooldownTurns} turns" : "Skill") : a.CastTime > 0 ? $"Cast time {a.CastTime:0.#}s — rides the timeline" : "Instant";
            Ui.Text(timing, "small gold", col);
            var d = Ui.Text(a.Description ?? "", "small", col);
            d.style.whiteSpace = WhiteSpace.Normal;
            Ui.Text("Tap a highlighted tile. Tap the slot again to cancel.", "tiny dim", col);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Commands
        // ─────────────────────────────────────────────────────────────────────────

        public bool CanCommand() => !playing && !ended && !State.IsOver;

        /// <summary>Dev/automation helper: add orbs to the bank (cheat — development builds only call this).</summary>
        public void DevGiveMana(string letters)
        {
            foreach (var c in ManaBank.ParseCost(letters)) State.Bank.Add(c);
            SyncOrbs();
            SyncBar();
        }

        public string BannerText => banner?.text;

        /// <summary>Dev/automation helper: put a buff on a unit and refresh its view.</summary>
        public bool DevBuff(int unitId, string buffId)
        {
            var u = State.UnitById(unitId);
            var def = App.Content.TryBuff(buffId);
            if (u == null || def == null) return false;
            u.Buffs.Add(new BuffInstance { Def = def, Remaining = def.Duration });
            SyncAll();
            return true;
        }

        /// <summary>Apply a command to the simulation and play its consequences.</summary>
        public void Commit(BattleCommand cmd, bool skipMovePrefix = false)
        {
            if (!CanCommand()) return;
            CancelTargeting();
            var events = BattleEngine.Apply(State, cmd);
            Debug.Log($"[Battle] {cmd} → {events.Count} events, clock {State.Clock}");
            App.StartCoroutine(Play(events, skipMovePrefix));
        }

        private void OnSlot(int slot)
        {
            if (!CanCommand()) return;
            var hero = State.UnitById(SelectedHeroId);
            if (hero == null) return;
            if (TargetingSlot == slot) { CancelTargeting(); SyncBar(); SyncPanel(); return; }
            var status = BattleEngine.GetSlotStatus(State, hero.Id, slot);
            if (status != SlotStatus.Ready)
            {
                App.Audio.Sfx("ui.error");
                ShowBanner(StatusText(status));
                return;
            }
            var a = BattleEngine.SlotAbility(State, hero, slot);
            if (a == null) return;
            App.Audio.Sfx("ui.select");
            if (a.Mode == TargetMode.Auto)
            {
                Commit(new UseSlotCommand { HeroId = hero.Id, Slot = slot, Target = hero.Pos });
                return;
            }
            var targets = BattleEngine.ValidTargets(State, hero.Id, slot);
            if (targets.Count == 0) { ShowBanner("No valid target"); App.Audio.Sfx("ui.error"); return; }
            TargetingSlot = slot;
            board.ClearMarks();
            foreach (var t in targets) board.Mark(t, new Color(1f, 0.82f, 0.3f), 0.35f);
            ShowBanner($"{a.Name}: choose a target");
            SyncBar();
            SyncPanel();
        }

        private static string StatusText(SlotStatus s)
        {
            switch (s)
            {
                case SlotStatus.Unaffordable: return "Not enough mana";
                case SlotStatus.Cooldown: return "Recharging";
                case SlotStatus.OutOfCharges: return "None left";
                case SlotStatus.Silenced: return "Silenced!";
                case SlotStatus.CastCapReached: return "Too many spells!";
                case SlotStatus.Locked: return "Unlocks as you progress";
                case SlotStatus.CannotAct: return "Can't act";
                default: return "Empty slot";
            }
        }

        public void CancelTargeting()
        {
            if (TargetingSlot < 0) return;
            TargetingSlot = -1;
            board.ClearMarks();
            ShowBanner("");
        }

        public override bool OnBack()
        {
            if (TargetingSlot >= 0) { CancelTargeting(); SyncBar(); return false; }
            OpenMenu();
            return false;
        }

        private void OpenMenu()
        {
            if (ended) return;
            var overlay = App.Overlay;
            var veil = Ui.El("fill veil col center", overlay);
            veil.pickingMode = PickingMode.Position;
            var w = Ui.Window("Paused", veil);
            w.style.width = 800;
            Ui.MenuRow("Resume", "", () => veil.RemoveFromHierarchy(), w);
            Ui.MenuRow(AutoPlay ? "Auto battle: ON" : "Auto battle: OFF", "", () => { AutoPlay = !AutoPlay; veil.RemoveFromHierarchy(); }, w);
            Ui.MenuRow("How to play", "", () => { veil.RemoveFromHierarchy(); App.Router.Push(new SettingsScreen()); }, w);
            Ui.MenuRow("Retreat", "(no rewards)", () => { veil.RemoveFromHierarchy(); Confirm.Show("Retreat from battle? You keep nothing.", "Retreat", Retreat); }, w);
            Ui.Select(w[1]);
        }

        private void Retreat()
        {
            ended = true;
            if (App.Save != null && setupOverride == null)
            {
                State.Phase = BattlePhase.Defeat;
                var rep = BattleFactory.Commit(App.Content, App.Save, State);
                App.Persist();
                App.Router.Replace(new PostBattleScreen(rep, State.Stage.Id));
            }
            else App.Router.Back();
        }

        public override void Tick(float dt)
        {
            // Watchdog: playback can never hold input hostage.
            if (playing && Time.unscaledTime - playStarted > 20f)
            {
                Debug.LogError("[Battle] playback watchdog fired; resyncing");
                playing = false;
                SyncAll();
            }
            if (dragging && App.Save != null && App.Save.Settings.DragTimer)
            {
                dragTimeLeft -= dt;
                timerLabel.text = $"{Mathf.Max(0, dragTimeLeft):0.0}";
                if (dragTimeLeft <= 0f) CommitDrag();
            }
            else if (timerLabel != null) timerLabel.text = "";

            if (slotPress >= 0 && !slotInfoShown && Time.unscaledTime - slotPressTime > 0.45f)
            {
                var hero = State.UnitById(SelectedHeroId);
                var ab = hero == null ? null : BattleEngine.SlotAbility(State, hero, slotPress);
                if (ab != null) { panel.Clear(); AbilityInfo(ab); slotInfoShown = true; App.Audio.Sfx("ui.select", 0.5f); }
            }

            if (!AutoPlay && CanCommand() && !pressing && TargetingSlot < 0 && HintsEnabled)
            {
                idle += dt;
                if (idle > HintDelay && !hintShown) ShowHint();
            }
            else ClearHint();

            if (AutoPlay && CanCommand() && !pressing)
            {
                autoBot ??= new HeroBot(Environment.TickCount);
                var cmd = autoBot.Decide(State);
                if (cmd is MoveCommand m && m.Path.Count > 0) App.StartCoroutine(AutoDrag(m));
                else Commit(cmd);
            }
        }

        /// <summary>Plays a bot move as a visible drag (so autoplay looks like a person playing).</summary>
        private IEnumerator AutoDrag(MoveCommand m)
        {
            playing = true;
            playStarted = Time.unscaledTime;
            var v = board.Units[m.HeroId];
            SelectHero(m.HeroId);
            var path = new List<Coord>();
            foreach (var step in m.Path)
            {
                path.Add(step);
                var pv = BattleEngine.PreviewMove(State, m.HeroId, path);
                ApplyPreview(pv, m.HeroId);
                var from = new Vector2(v.Root.resolvedStyle.left, v.Root.resolvedStyle.top);
                App.Audio.Sfx("move", 0.5f);
                yield return Tween.Move(v.Root, from, BoardView.PosOf(step), 0.09f);
            }
            playing = false;
            Commit(m, true);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Selection / banner helpers
        // ─────────────────────────────────────────────────────────────────────────

        private void ShowHint()
        {
            hintShown = true;
            var decision = new HeroBot(7).Decide(State);
            if (decision is UseSlotCommand use)
            {
                var caster = State.UnitById(use.HeroId);
                var ab = caster == null ? null : BattleEngine.SlotAbility(State, caster, use.Slot);
                if (caster != null && ab != null)
                {
                    SelectHero(caster.Id);
                    if (use.Slot < slotViews.Count) App.StartCoroutine(Tween.Pop(slotViews[use.Slot], 1.12f, 0.35f));
                    ShowBanner($"Hint: {caster.Name} can use {ab.Name}");
                }
                return;
            }
            if (decision is ShieldCommand) { ShowBanner("Hint: nothing good to do? Press GUARD"); return; }
            var cmd = decision as MoveCommand;
            if (cmd == null || cmd.Path.Count == 0) return;
            var pv = BattleEngine.PreviewMove(State, cmd.HeroId, cmd.Path);
            if (pv.Pincers.Count == 0) { ShowBanner("Tip: trap an enemy between two heroes"); return; }
            var hero = State.UnitById(cmd.HeroId);
            var dest = cmd.Path[cmd.Path.Count - 1];
            board.ClearMarks();
            board.Mark(dest, new Color(1f, 0.9f, 0.35f), 0.5f);
            if (board.Units.TryGetValue(hero.Id, out var hv)) { hv.SetGlow(new Color(1f, 0.9f, 0.35f), 0.95f); App.StartCoroutine(Tween.Pop(hv.Root, 1.12f, 0.35f)); }
            board.Lines.ClearLines();
            var from = BoardView.CenterOf(hero.Pos);
            foreach (var step in cmd.Path) { var to = BoardView.CenterOf(step); board.Lines.Add(from, to, new Color(1f, 0.9f, 0.35f, 0.55f), 10f); from = to; }
            ShowBanner($"Hint: drag {hero.Name} to the glowing tile");
        }

        private void ClearHint()
        {
            idle = 0f;
            if (!hintShown) return;
            hintShown = false;
            if (TargetingSlot < 0 && !dragging) { board.ClearMarks(); board.Lines.ClearLines(); }
        }

        public void SelectHero(int id)
        {
            if (SelectedHeroId == id) return;
            CancelTargeting();
            SelectedHeroId = id;
            panelUnitId = -1;
            foreach (var u in State.Units)
                if (board.Units.TryGetValue(u.Id, out var v) && u.Alive)
                    v.SetGlow(new Color(1f, 0.9f, 0.5f), u.Id == id ? 0.65f : 0f);
            SyncBar();
            SyncPanel();
        }

        /// <summary>Combat feed (bible §9 row 4): the last few events as fading lines over the board's corner.</summary>
        public void Feed(string text, Color color)
        {
            if (feed == null || string.IsNullOrEmpty(text)) return;
            var l = Ui.Text(text, "tiny shadow", feed);
            l.style.color = color;
            l.style.fontSize = 30;
            while (feed.childCount > 3) feed.RemoveAt(0);
            App.StartCoroutine(FeedFade(l));
        }

        private IEnumerator FeedFade(Label l)
        {
            yield return new WaitForSecondsRealtime(4f);
            float t = 0;
            while (t < 0.6f && l.parent != null) { t += Time.unscaledDeltaTime; l.style.opacity = 1f - t / 0.6f; yield return null; }
            l.RemoveFromHierarchy();
        }

        /// <summary>Action/status banner above the board; long text steps the font down so it never runs off-stage.</summary>
        private void ShowBanner(string text)
        {
            if (banner == null) return;
            text ??= "";
            banner.text = text;
            banner.style.fontSize = text.Length > 44 ? 32 : text.Length > 32 ? 38 : 48;
        }
    }
}
