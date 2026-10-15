using System;
using System.Collections.Generic;
using System.Linq;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    internal static class ItemUi
    {
        public static string TypeLabel(ItemDef i)
        {
            switch (i.Type)
            {
                case ItemType.Equipment: return i.Slot == EquipmentSlot.Weapon ? "Weapon" : i.Slot == EquipmentSlot.Armor ? "Armor" : "Relic";
                case ItemType.Consumable: return "Item";
                case ItemType.CraftingMaterial: return "Material";
                case ItemType.RecipeScroll: return "Recipe Scroll";
                default: return "Key Item";
            }
        }

        public static string Glyph(ItemDef i)
        {
            switch (i.Type)
            {
                case ItemType.Equipment: return i.Slot == EquipmentSlot.Weapon ? "†" : i.Slot == EquipmentSlot.Armor ? "◆" : "✧";
                case ItemType.Consumable: return "✚";
                case ItemType.CraftingMaterial: return "◈";
                case ItemType.RecipeScroll: return "❖";
                default: return "✦";
            }
        }

        public static string StatsText(ItemDef i)
        {
            var parts = new List<string>();
            var s = i.Stats;
            void P(string n, float v) { if (Math.Abs(v) > 0.01f) parts.Add($"{n} {(v > 0 ? "+" : "")}{v:0}"); }
            P("STR", s.Strength); P("VIT", s.Vitality); P("AGI", s.Agility); P("SPD", s.Speed);
            P("INT", s.Intelligence); P("WIS", s.Wisdom); P("LCK", s.Luck); P("STA", s.Stamina);
            if (i.BaseDamage > 0) parts.Add($"ATK +{i.BaseDamage}");
            if (i.BaseHealing > 0) parts.Add($"Heals {i.BaseHealing}");
            if (i.Durability > 0) parts.Add($"Durability {i.Durability}");
            if (i.BattleStartManaOrbs > 0) parts.Add($"+{i.BattleStartManaOrbs} orbs at battle start");
            if (i.BankCapacityBonus > 0) parts.Add($"+{i.BankCapacityBonus} orb slots");
            if (i.PincerDamageBonus > 0) parts.Add($"Pincer +{i.PincerDamageBonus:P0}");
            if (i.RegenPerTurn > 0) parts.Add($"Regen {i.RegenPerTurn}/turn");
            if (i.StartsProtected) parts.Add("Starts battles Protected");
            if (i.GrantsOrbs > 0) parts.Add($"+{i.GrantsOrbs} orbs");
            if (i.Revives) parts.Add("Revives a fallen ally");
            if (i.Escape) parts.Add("Escape from battle");
            if (i.CuresAll) parts.Add("Cures all ailments");
            else if (i.CuresBuff != null) parts.Add($"Cures {i.CuresBuff}");
            foreach (var kv in i.Resistances) parts.Add($"{kv.Key} ×{kv.Value:0.##}");
            return string.Join("   ", parts);
        }

        /// <summary>Standard item row: glyph · rarity-colored name · right value. <paramref name="masterwork"/> prefixes a ★ (GG-T46).</summary>
        public static VisualElement Row(ItemDef i, string value, Action onClick, VisualElement parent, bool enabled = true, bool masterwork = false)
        {
            var icon = Art.ItemIcon(i.Id);
            string label = (icon != null ? i.Name : $"{Glyph(i)}  {i.Name}");
            if (masterwork) label = "★ " + label;
            var row = Ui.MenuRow(label, value, onClick, parent, icon, enabled);
            row.Q<Label>("label").AddToClassList(Ui.RarityClass(i.Rarity));
            return row;
        }

        public static void Detail(VisualElement w, ItemDef i)
        {
            w.Clear();
            if (i == null) { Ui.Text("Select an item.", "small dim", w); return; }
            var top = Ui.El("row", w);
            var n = Ui.Text(i.Name, "h2 grow " + Ui.RarityClass(i.Rarity), top);
            Ui.Text($"{i.Rarity} {TypeLabel(i)}", "small dim", top);
            var d = Ui.Text(string.IsNullOrEmpty(i.Description) ? "—" : i.Description, "small", w);
            d.style.whiteSpace = WhiteSpace.Normal;
            var st = StatsText(i);
            if (st.Length > 0) { var l = Ui.Text(st, "small gold", w); l.style.whiteSpace = WhiteSpace.Normal; l.style.marginTop = 8; }
        }
    }

    // ── Items (inventory) ─────────────────────────────────────────────────────────

    public sealed class ItemsScreen : HubPage
    {
        private int tab;
        private ItemDef selected;
        private bool selectedBroken;
        public override string Id => "items";
        protected override string Title => "Items";
        private static readonly string[] Tabs = { "All", "Items", "Gear", "Materials" };

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(Tabs, tab, i => { tab = i; selected = null; Refresh(); }, body);
            var w = Ui.Window(null, body);
            w.style.flexGrow = 1; w.style.marginTop = 0;
            var list = Ui.List(w);
            var items = S.Inventory.Where(kv => kv.Value > 0).Select(kv => (item: C.TryItem(kv.Key), n: kv.Value)).Where(x => x.item != null)
                .Where(x => tab == 0 || (tab == 1 && x.item.Type == ItemType.Consumable) || (tab == 2 && x.item.Type == ItemType.Equipment) || (tab == 3 && x.item.Type == ItemType.CraftingMaterial))
                .OrderBy(x => x.item.Type).ThenByDescending(x => x.item.Rarity).ThenBy(x => x.item.Name).ToList();
            // Broken gear fragments (GG-T41): shown alongside gear, but can't be equipped or repaired — salvage only.
            var broken = (tab == 0 || tab == 2)
                ? S.Broken.Where(kv => kv.Value > 0).Select(kv => (item: C.TryItem(kv.Key), n: kv.Value)).Where(x => x.item != null)
                    .OrderByDescending(x => x.item.Rarity).ThenBy(x => x.item.Name).ToList()
                : new List<(ItemDef item, int n)>();
            var detail = Ui.Window(null, body);
            detail.style.minHeight = 300;
            if (items.Count == 0 && broken.Count == 0) Ui.Text("Nothing here yet. Win battles to find loot.", "small dim", list.contentContainer);
            foreach (var (item, n) in items)
            {
                var it = item;
                var row = ItemUi.Row(it, $"×{n}", () => { selected = it; selectedBroken = false; ShowDetail(detail, it, false); }, list.contentContainer, true, Equipment.IsMasterwork(S, it.Id));
                if (selected == it && !selectedBroken) Ui.Select(row);
            }
            foreach (var (item, n) in broken)
            {
                var it = item;
                var row = Ui.MenuRow($"Broken {it.Name}", $"×{n}", () => { selected = it; selectedBroken = true; ShowDetail(detail, it, true); }, list.contentContainer, Art.ItemIcon(it.Id));
                row.Q<Label>("label").AddToClassList("dim");
                if (selected == it && selectedBroken) Ui.Select(row);
            }
            var fallback = selected;
            bool fallbackBroken = selectedBroken;
            if (fallback == null)
            {
                fallback = items.Select(x => x.item).FirstOrDefault();
                fallbackBroken = false;
                if (fallback == null) { fallback = broken.Select(x => x.item).FirstOrDefault(); fallbackBroken = fallback != null; }
            }
            ShowDetail(detail, fallback, fallbackBroken);
            if (selected == null && list.contentContainer.childCount > 0 && (items.Count > 0 || broken.Count > 0)) Ui.Select(list.contentContainer[0]);
        }

        private void ShowDetail(VisualElement detail, ItemDef it, bool broken)
        {
            ItemUi.Detail(detail, it);
            if (broken)
            {
                var note = Ui.Text("Broken. Can't be equipped or repaired — salvage it at the Blacksmith for materials.", "small bad", detail);
                note.style.whiteSpace = WhiteSpace.Normal;
                note.style.marginTop = 10;
                return;
            }
            if (it != null && it.Type == ItemType.RecipeScroll)
            {
                var learnRow = Ui.El("row", detail);
                learnRow.style.marginTop = 10;
                Ui.Button("Learn recipe", () => { selected = it; Done(RecipeDiscovery.Learn(C, S, it.Id)); }, "btn--small btn--primary", learnRow);
                return;
            }
            if (!Camp.UsableAtCamp(it)) return;
            var row = Ui.El("row", detail);
            row.style.marginTop = 10;
            if (Camp.HealsWholeParty(it))
            {
                Ui.Button("Use on party", () => Done(Camp.Use(C, S, it.Id, null)), "btn--small btn--primary", row);
                return;
            }
            foreach (var id in S.Party)
            {
                var hero = id;
                Ui.Button(C.TryClass(id)?.Name ?? id, () => { selected = it; Done(Camp.Use(C, S, it.Id, hero)); }, "btn--small", row);
            }
        }
    }

    // ── Status ────────────────────────────────────────────────────────────────────

    public abstract class HeroPage : HubPage
    {
        protected string HeroId;
        protected HeroPage(string heroId) { HeroId = heroId; }
        protected HeroSave Hero => S.Hero(HeroId) ?? S.Roster.FirstOrDefault();

        /// <summary>‹ portrait name › hero switcher across the roster.</summary>
        protected void HeroSwitcher(VisualElement parent)
        {
            var roster = S.Roster.Select(h => h.ClassId).ToList();
            int idx = Math.Max(0, roster.IndexOf(Hero.ClassId));
            var bar = Ui.Window(null, parent);
            var row = Ui.El("row", bar);
            Ui.Button("‹", () => { HeroId = roster[(idx - 1 + roster.Count) % roster.Count]; Refresh(); }, "btn--small btn--icon", row);
            Ui.Thumb(Hero.ClassId, row, 120).style.marginLeft = 16;
            var col = Ui.El("col grow", row);
            var u = Progression.Preview(C, Hero);
            Ui.Text(C.Class(Hero.ClassId).Name, "h2", col);
            Ui.Text($"Lv {u.Level}{(S.Party.Contains(Hero.ClassId) ? "" : "  (reserve)")}", "small gold", col);
            Ui.Button("›", () => { HeroId = roster[(idx + 1) % roster.Count]; Refresh(); }, "btn--small btn--icon", row);
        }
    }

    public sealed class StatusScreen : HeroPage
    {
        public StatusScreen(string heroId) : base(heroId) { }
        public override string Id => "status";
        protected override string Title => "Status";

        protected override void BuildBody(VisualElement body)
        {
            HeroSwitcher(body);
            var h = Hero;
            var def = C.Class(h.ClassId);
            var u = Progression.Preview(C, h);
            var w = Ui.Window(null, body);
            w.style.flexGrow = 1;
            var top = Ui.El("row", w);
            var art = Ui.El("", top);
            art.style.width = 420; art.style.height = 620;
            art.style.backgroundImage = new StyleBackground(Art.Portrait(h.ClassId));
            art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            var stats = Ui.El("col grow", top);
            stats.style.marginLeft = 20;
            int hp = h.Hp > 0 ? Math.Min(h.Hp, u.MaxHp) : u.MaxHp;
            var roleRow = Ui.El("row", stats);
            roleRow.style.marginBottom = 10;
            Ui.RoleBadge(def.Role, roleRow, 30).style.marginLeft = 0;
            var blurb = Ui.Text(Ui.RoleBlurb(def.Role), "tiny dim", roleRow);
            blurb.style.whiteSpace = WhiteSpace.Normal; blurb.style.flexShrink = 1;
            Ui.Text($"HP  {hp} / {u.MaxHp}", "h2", stats);
            Ui.Gauge(hp / (float)u.MaxHp, stats).style.marginBottom = 18;
            var (lvl, into) = Gaiden.Core.Rules.Formulas.LevelFromTotalXp(h.TotalXp);
            Ui.Text($"Level {lvl}", "h2 gold", stats);
            Ui.Text($"Next: {Gaiden.Core.Rules.Formulas.XpToNext(lvl) - into} XP", "small dim", stats);
            var e = u.Effective;
            void Stat(string n, float v) { var r = Ui.El("row", stats); Ui.Text(n, "small dim", r).style.width = 120; Ui.Text($"{v:0}", "h2", r); }
            Stat("STR", e.Strength); Stat("VIT", e.Vitality); Stat("AGI", e.Agility); Stat("SPD", e.Speed);
            Stat("INT", e.Intelligence); Stat("WIS", e.Wisdom); Stat("LCK", e.Luck);

            var gear = Ui.El("col", w); gear.style.marginTop = 16;
            void G(string slot, string id, int dur = -1, float mastery = 1f)
            {
                var it = C.TryItem(id);
                var r = Ui.El("row", gear);
                Ui.Text(slot, "small dim", r).style.width = 180;
                string name = it == null ? "—" : (mastery > 1f ? "★ " : "") + it.Name +
                    (dur >= 0 && it.Durability > 0 ? $"  ({dur}/{Repair.EffectiveMax(it, slot == "Weapon" ? h.WeaponRepairs : h.ArmorRepairs, mastery)})" : "");
                Ui.Text(name, "small " + (it != null ? Ui.RarityClass(it.Rarity) : "dim"), r);
            }
            G("Weapon", h.WeaponId, h.WeaponDurability, h.WeaponMastery); G("Armor", h.ArmorId, h.ArmorDurability, h.ArmorMastery);
            for (int i = 0; i < 3; i++) G($"Relic {i + 1}", h.Relics[i]);
            var lore = Ui.Text(Ui.Keywords(def.Lore ?? def.Description ?? ""), "small dim", w);
            lore.style.whiteSpace = WhiteSpace.Normal; lore.style.marginTop = 16;
        }
    }

    // ── Equip ─────────────────────────────────────────────────────────────────────

    public sealed class EquipScreen : HeroPage
    {
        private EquipmentSlot slot = EquipmentSlot.Weapon;
        private string previewId;
        public EquipScreen(string heroId) : base(heroId) { }
        public override string Id => "equip";
        protected override string Title => "Equip";
        private static readonly EquipmentSlot[] Slots = { EquipmentSlot.Weapon, EquipmentSlot.Armor, EquipmentSlot.Relic1, EquipmentSlot.Relic2, EquipmentSlot.Relic3 };

        protected override void BuildBody(VisualElement body)
        {
            HeroSwitcher(body);
            var h = Hero;
            var slotsW = Ui.Window(null, body);
            foreach (var sl in Slots)
            {
                var s2 = sl;
                var it = C.TryItem(Equipment.Equipped(h, sl));
                bool mastery = sl == EquipmentSlot.Weapon ? h.WeaponMastery > 1f : sl == EquipmentSlot.Armor && h.ArmorMastery > 1f;
                var row = Ui.MenuRow(SlotName(sl), it == null ? "—" : (mastery ? "★ " : "") + it.Name, () => { slot = s2; previewId = null; Refresh(); }, slotsW);
                if (it != null) row.Q<Label>("value").AddToClassList(Ui.RarityClass(it.Rarity));
                if (sl == slot) Ui.Select(row);
            }

            var cmp = Ui.Window(null, body);
            var (now, then) = Equipment.Compare(C, h, slot, previewId ?? Equipment.Equipped(h, slot));
            var grid = Ui.El("row", cmp);
            grid.style.flexWrap = Wrap.Wrap;
            void Cell(string n, float a, float b) { var c = Ui.El("row", grid); c.style.width = Length.Percent(25); Ui.Text(n, "small dim", c).style.width = 90; Ui.Text(Ui.Delta(a, b), "small", c); }
            Cell("STR", now.Strength, then.Strength); Cell("VIT", now.Vitality, then.Vitality); Cell("AGI", now.Agility, then.Agility); Cell("SPD", now.Speed, then.Speed);
            Cell("INT", now.Intelligence, then.Intelligence); Cell("WIS", now.Wisdom, then.Wisdom); Cell("LCK", now.Luck, then.Luck);

            var w = Ui.Window($"{SlotName(slot)} — your bag", body);
            w.style.flexGrow = 1;
            var list = Ui.List(w);
            if (Equipment.Equipped(h, slot) != null)
                Ui.MenuRow("Remove", "", () => Done(Equipment.Unequip(C, S, h, slot)), list.contentContainer);
            var cands = Equipment.Candidates(C, S, slot);
            if (cands.Count == 0) Ui.Text("Nothing in your bag fits this slot. Forge gear at the Blacksmith.", "small dim", list.contentContainer).style.whiteSpace = WhiteSpace.Normal;
            foreach (var it in cands)
            {
                var item = it;
                var row = ItemUi.Row(item, $"×{S.Count(item.Id)}", () =>
                {
                    if (previewId == item.Id) { previewId = null; Done(Equipment.Equip(C, S, h, slot, item.Id)); }
                    else { previewId = item.Id; Refresh(); App.Toast("Tap again to equip"); }
                }, list.contentContainer, true, Equipment.IsMasterwork(S, item.Id));
                if (item.Id == previewId) Ui.Select(row);
            }
        }

        private static string SlotName(EquipmentSlot s) => s == EquipmentSlot.Relic1 ? "Relic I" : s == EquipmentSlot.Relic2 ? "Relic II" : s == EquipmentSlot.Relic3 ? "Relic III" : s.ToString();
    }

    // ── Abilities: learn + slot the bar ───────────────────────────────────────────

    public sealed class AbilitiesScreen : HeroPage
    {
        private int slot;
        private int tab;
        public AbilitiesScreen(string heroId) : base(heroId) { }
        public override string Id => "abilities";
        protected override string Title => "Abilities";

        protected override void BuildBody(VisualElement body)
        {
            HeroSwitcher(body);
            var h = Hero;
            int unlocked = Progression.UnlockedSlots(S);
            var barW = Ui.Window("Battle bar", body);
            var bar = Ui.El("row", barW);
            for (int i = 0; i < Progression.MaxBarSlots; i++)
            {
                int idx = i;
                var cell = Ui.El("col center", bar);
                cell.style.width = 196; cell.style.height = 170; cell.style.marginRight = 6;
                cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius = cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 14;
                cell.style.borderTopWidth = cell.style.borderBottomWidth = cell.style.borderLeftWidth = cell.style.borderRightWidth = i == slot ? 6 : 3;
                var bc = i == slot ? new Color(1f, 0.85f, 0.4f) : new Color(0.9f, 0.92f, 1f, 0.7f);
                cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = bc;
                cell.style.backgroundColor = new Color(0.05f, 0.08f, 0.25f, 0.9f);
                if (i >= unlocked) { Ui.Text("Locked", "small dim", cell); cell.style.opacity = 0.5f; continue; }
                var entry = h.Bar[i];
                var label = EntryName(entry);
                var icon = EntryIcon(entry);
                if (icon != null) { var ic = Ui.El("menu-icon", cell); ic.style.backgroundImage = new StyleBackground(icon); ic.style.marginRight = 0; }
                var t = Ui.Text(label ?? "Empty", "tiny" + (label == null ? " dim" : ""), cell);
                t.style.unityTextAlign = TextAnchor.MiddleCenter; t.style.whiteSpace = WhiteSpace.Normal;
                cell.AddToClassList("card-tap");
                Ui.Tappable(cell, () => { slot = idx; Refresh(); });
            }
            var tools = Ui.El("row", barW);
            tools.style.marginTop = 10; tools.style.alignItems = Align.Center;
            Ui.Text("Pick a slot, then choose what goes in it — or move it along the bar.", "tiny dim grow", tools).style.whiteSpace = WhiteSpace.Normal;
            var left = Ui.Button("‹ Move", () => { var r = Abilities.Swap(S, h, slot, slot - 1); if (r.Ok) slot--; Done(r); }, "btn--small", tools);
            left.name = "bar-move-left";
            Ui.SetEnabled(left, slot > 0 && slot < unlocked);
            var right = Ui.Button("Move ›", () => { var r = Abilities.Swap(S, h, slot, slot + 1); if (r.Ok) slot++; Done(r); }, "btn--small", tools);
            right.name = "bar-move-right";
            Ui.SetEnabled(right, slot + 1 < unlocked);

            Ui.Tabs(new[] { "Known", "Items", "Learn" }, tab, i => { tab = i; Refresh(); }, body);
            var w = Ui.Window(null, body);
            w.style.flexGrow = 1; w.style.marginTop = 0;
            var list = Ui.List(w).contentContainer;
            if (slot < unlocked && h.Bar[slot] != null) Ui.MenuRow("Clear slot", "", () => Done(Abilities.Assign(C, S, h, slot, null)), list);
            if (tab == 0)
            {
                foreach (var id in h.Known)
                {
                    var a = C.TryAbility(id);
                    if (a == null) continue;
                    var row = Ui.MenuRow(a.Name, KindLabel(a), () => Done(Abilities.Assign(C, S, h, slot, a.Id)), list, Art.AbilityIcon(a), slot < unlocked);
                    if (a.Cost > 0) Ui.CostPips(a.Cost, row);
                    Describe(a, list);
                }
            }
            else if (tab == 1)
            {
                var items = S.Inventory.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key)).Where(i => i != null && (i.Type == ItemType.Consumable || i.Slot == EquipmentSlot.Weapon)).ToList();
                if (items.Count == 0) Ui.Text("No battle items. Buy potions from the Merchant.", "small dim", list);
                foreach (var it in items)
                {
                    var item = it;
                    bool weapon = item.Slot == EquipmentSlot.Weapon;
                    ItemUi.Row(item, weapon ? "swap in battle" : $"×{S.Count(item.Id)}", () => Done(Abilities.Assign(C, S, h, slot, (weapon ? "weapon:" : "item:") + item.Id)), list, slot < unlocked);
                }
            }
            else
            {
                foreach (var a in Abilities.Learnable(C, h))
                {
                    var ab = a;
                    int price = Abilities.Price(a);
                    var row = Ui.MenuRow(a.Name, Ui.Gold(price), () => Confirm.Show($"Teach {C.Class(h.ClassId).Name} {ab.Name} for {Ui.Gold(price)}?", "Learn", () => Done(Abilities.Learn(C, S, h, ab.Id))), list, Art.AbilityIcon(a), S.Gold >= price);
                    Describe(a, list);
                }
            }
        }

        private void Describe(AbilityDef a, VisualElement list)
        {
            var d = Ui.Text(a.Description ?? "", "tiny dim", list);
            d.style.whiteSpace = WhiteSpace.Normal; d.style.marginLeft = 64; d.style.marginBottom = 8;
        }

        private static string KindLabel(AbilityDef a) => a.Kind == AbilityKind.Skill ? (a.CooldownTurns > 0 ? $"Skill · CD {a.CooldownTurns}" : "Skill") : a.CastTime > 0 ? $"{a.CastTime:0.#}s" : "Instant";

        private string EntryName(string entry)
        {
            if (entry == null) return null;
            if (entry.StartsWith("item:")) { var it = C.TryItem(entry.Substring(5)); return it == null ? null : $"{it.Name} ×{S.Count(it.Id)}"; }
            if (entry.StartsWith("weapon:")) { var w = C.TryItem(entry.Substring(7)); return w == null ? null : $"⭾ {w.Name}"; }
            return C.TryAbility(entry)?.Name;
        }

        private Texture2D EntryIcon(string entry)
        {
            if (entry == null) return null;
            if (entry.StartsWith("item:")) return Art.ItemIcon(entry.Substring(5));
            if (entry.StartsWith("weapon:")) return Art.ItemIcon(entry.Substring(7));
            return Art.AbilityIcon(C.TryAbility(entry));
        }
    }

    // ── Party ─────────────────────────────────────────────────────────────────────

    public sealed class PartyScreen : HubPage
    {
        public override string Id => "party";
        protected override string Title => "Party";

        protected override void BuildBody(VisualElement body)
        {
            var w = Ui.Window($"Active party {S.Party.Count}/{Progression.MaxPartySize} — tap to swap", body);
            w.style.flexGrow = 1;
            var list = Ui.List(w).contentContainer;
            foreach (var h in S.Roster.OrderByDescending(x => S.Party.Contains(x.ClassId)).ThenBy(x => x.ClassId))
            {
                var hero = h;
                var u = Progression.Preview(C, h);
                bool active = S.Party.Contains(h.ClassId);
                var card = Ui.El("card" + (active ? " card--selected" : ""), list);
                Ui.Thumb(h.ClassId, card, 132);
                var col = Ui.El("col grow", card);
                var nameRow = Ui.El("row", col);
                Ui.Text(C.Class(h.ClassId).Name, "h2", nameRow);
                Ui.RoleBadge(C.Class(h.ClassId).Role, nameRow);
                Ui.Text($"Lv {u.Level}   HP {u.MaxHp}   {(active ? "<color=#78EC96>Active</color>" : "Reserve")}", "small", col);
                Ui.Text(Ui.Keywords(C.Class(h.ClassId).Card ?? ""), "tiny dim", col).style.whiteSpace = WhiteSpace.Normal;
                card.AddToClassList("card-tap");
                Ui.Tappable(card, () => Done(Party.Toggle(S, hero.ClassId)), "ui.select");
            }
        }
    }

    // ── Merchant (buy / sell with carts, §25.1) ──────────────────────────────────

    public sealed class ShopScreen : HubPage
    {
        private int tab;
        private string bargainSelected;
        private readonly Dictionary<string, int> buyCart = new Dictionary<string, int>();
        private readonly Dictionary<string, int> sellCart = new Dictionary<string, int>();
        public override string Id => "shop";
        protected override string Title => "Merchant";

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(new[] { "Buy", "Sell", "Bargain" }, tab, i => { tab = i; bargainSelected = null; Refresh(); }, body);
            if (tab == 2) { BuildBargain(body); return; }
            bool selling = tab == 1;
            var w = Ui.Window(null, body);
            w.style.flexGrow = 1; w.style.marginTop = 0;
            var list = Ui.List(w).contentContainer;
            var cart = selling ? sellCart : buyCart;
            var items = selling ? Shop.Sellable(C, S) : Shop.Stock(C);
            if (items.Count == 0) Ui.Text(selling ? "Nothing to sell." : "Sold out.", "small dim", list);
            var detail = Ui.Window(null, body);
            detail.style.minHeight = 220;
            ItemUi.Detail(detail, null);
            foreach (var it in items)
            {
                var item = it;
                cart.TryGetValue(item.Id, out var q);
                int price = selling ? Shop.SellPrice(item) : item.BaseCost;
                int max = selling ? S.Count(item.Id) : Shop.MaxBuy(S, item);
                var row = ItemUi.Row(item, $"{price} G", () => ItemUi.Detail(detail, item), list);
                var owned = Ui.Text($"own {S.Count(item.Id)}", "tiny dim", row);
                owned.style.marginLeft = 12; owned.style.width = 110;
                var minus = Ui.Button("−", () => { Step(cart, item.Id, -1, max); }, "btn--small btn--icon", row);
                var n = Ui.Text(q.ToString(), "h2", row);
                n.style.width = 70; n.style.unityTextAlign = TextAnchor.MiddleCenter;
                var plus = Ui.Button("+", () => { Step(cart, item.Id, +1, max); }, "btn--small btn--icon", row);
                Ui.SetEnabled(minus, q > 0);
                Ui.SetEnabled(plus, q < max);
            }
            var foot = Ui.Window(null, body);
            var fr = Ui.El("row", foot);
            int total = Shop.CartTotal(C, cart, selling);
            var tl = Ui.Text(selling ? $"Receive: {Ui.Gold(total)}" : $"Pay: {Ui.Gold(total)}", "h2 grow " + (!selling && total > S.Gold ? "bad" : "gold"), fr);
            var commit = Ui.Button(selling ? "Sell" : "Buy", () =>
            {
                var r = selling ? Shop.Sell(C, S, cart) : Shop.Buy(C, S, cart);
                if (r.Ok) cart.Clear();
                Done(r);
            }, "btn--primary", fr);
            Ui.SetEnabled(commit, total > 0 && (selling || total <= S.Gold));
        }

        private void Step(Dictionary<string, int> cart, string id, int d, int max)
        {
            cart.TryGetValue(id, out var q);
            q = Mathf.Clamp(q + d, 0, max);
            if (q == 0) cart.Remove(id); else cart[id] = q;
            Refresh();
        }

        // ── "Used / Bargain" shelf: rotating worn weapons/armor at a discount (US-246) ──

        private void BuildBargain(VisualElement body)
        {
            var w = Ui.Window(null, body);
            w.style.flexGrow = 1; w.style.marginTop = 0;
            var list = Ui.List(w).contentContainer;
            var detail = Ui.Window(null, body);
            detail.style.minHeight = 260;
            var offers = Bargains.Shelf(C, S);
            if (offers.Count == 0) Ui.Text("The shelf is bare. Check back after your next battle.", "small dim", list).style.whiteSpace = WhiteSpace.Normal;
            foreach (var o in offers)
            {
                var offer = o;
                var row = ItemUi.Row(offer.Item, $"{offer.Price} G", () => { bargainSelected = offer.Item.Id; Refresh(); }, list);
                if (bargainSelected == offer.Item.Id) Ui.Select(row);
            }
            var sel = offers.FirstOrDefault(o => o.Item.Id == bargainSelected);
            if (sel != null) ShowBargain(detail, sel);
            else Ui.Text("Pick a piece to see how worn it is.", "small dim", detail);
        }

        private void ShowBargain(VisualElement w, BargainOffer offer)
        {
            ItemUi.Detail(w, offer.Item);
            var row = Ui.El("row", w); row.style.marginTop = 12;
            Ui.Text($"Durability {offer.Durability}/{offer.Item.Durability}", "small dim grow", row);
            Ui.Gauge((float)offer.Durability / offer.Item.Durability, w, 320).style.marginBottom = 10;
            var warn = Ui.Text("Used gear: cheap now, but it can shatter mid-battle before you get a chance to repair it.", "small warn", w);
            warn.style.whiteSpace = WhiteSpace.Normal;
            bool owned = S.Count(offer.Item.Id) >= offer.Item.MaxStack;
            var btn = Ui.Button(owned ? "Already owned" : $"Buy for {Ui.Gold(offer.Price)}", () =>
            {
                bargainSelected = null;
                Done(Bargains.Buy(C, S, offer.Item.Id));
            }, "btn--primary", w);
            btn.style.marginTop = 10;
            Ui.SetEnabled(btn, !owned && S.Gold >= offer.Price);
        }
    }

    // ── Blacksmith: forge / upgrade / salvage / repair (§25.2) ───────────────────

    public sealed class BlacksmithScreen : HubPage
    {
        private int tab;
        private RecipeDef selected;
        public override string Id => "blacksmith";
        protected override string Title => "Blacksmith";
        protected override string Backdrop => "cave";
        protected override string Subtitle => StationProgress.HeaderText(S, StationKind.Blacksmith);

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(new[] { "Forge", "Upgrade", "Salvage", "Repair" }, tab, i => { tab = i; selected = null; Refresh(); }, body);
            var w = Ui.Window(null, body);
            w.style.flexGrow = 1; w.style.marginTop = 0;
            var list = Ui.List(w).contentContainer;
            var detail = Ui.Window(null, body);
            detail.style.minHeight = 330;
            if (tab <= 1) RecipeList(list, detail, C, S, CraftStation.Forge, tab == 1, ref selected, this);
            else if (tab == 2) SalvageList(list, detail);
            else RepairList(list, detail);
        }

        internal static void RecipeList(VisualElement list, VisualElement detail, ContentDb c, SaveData s, CraftStation station, bool upgrades, ref RecipeDef selected, HubPage page)
        {
            var recipes = Crafting.Recipes(c, station).Where(r => (r.BaseItem != null) == upgrades).ToList();
            if (station == CraftStation.Enchant) recipes = Crafting.Recipes(c, station).ToList();
            // Recipe discovery (§24.6.2, GG-T36): undiscovered recipes are hidden, except a "???" tease
            // for ones the player is exactly one input away from.
            var visible = recipes.Where(r => RecipeDiscovery.IsDiscovered(s, r) || RecipeDiscovery.OneAwayMissing(s, r) != null).ToList();
            if (visible.Count == 0) Ui.Text("No recipes.", "small dim", list);
            RecipeDef sel = selected;
            foreach (var r in visible)
            {
                var rec = r;
                VisualElement row;
                if (RecipeDiscovery.IsDiscovered(s, r))
                {
                    var result = c.Item(r.Result);
                    bool locked = !Crafting.IsUnlocked(s, r);
                    bool can = !locked && Crafting.HasInputs(s, r) && s.Gold >= r.Gold;
                    row = ItemUi.Row(result, locked ? $"🔒 {StationProgress.TierName(r.Tier)}" : $"{r.Gold} G", () => ShowRecipe(detail, c, s, rec, page), list, true);
                    if (!can) row.style.opacity = 0.55f;
                }
                else
                {
                    var missing = RecipeDiscovery.OneAwayMissing(s, r);
                    var missingItem = c.TryItem(missing.Item);
                    row = Ui.MenuRow("???", $"Missing {missingItem?.Name ?? missing.Item}", () => ShowRecipe(detail, c, s, rec, page), list);
                    row.style.opacity = 0.75f;
                }
                if (sel == r) Ui.Select(row);
            }
            if (sel != null) ShowRecipe(detail, c, s, sel, page);
            else Ui.Text("Pick a recipe to see what it needs.", "small dim", detail);
        }

        internal static void ShowRecipe(VisualElement w, ContentDb c, SaveData s, RecipeDef r, HubPage page)
        {
            w.Clear();
            if (!RecipeDiscovery.IsDiscovered(s, r))
            {
                Ui.Text("??? — Undiscovered Recipe", "h2 grow dim", w);
                var tease = Ui.Text("You sense you're close. Find the missing material to learn how this is made.", "small dim", w);
                tease.style.whiteSpace = WhiteSpace.Normal;
                var undiscoveredNeed = Ui.El("col", w); undiscoveredNeed.style.marginTop = 12;
                if (r.BaseItem != null)
                {
                    var b = c.TryItem(r.BaseItem);
                    int have = s.Count(r.BaseItem);
                    Ui.Text($"{(have >= 1 ? "✔" : "✘")} {b?.Name ?? r.BaseItem} ×1   (have {have})", "small " + (have >= 1 ? "good" : "bad"), undiscoveredNeed);
                }
                foreach (var ic in r.Inputs)
                {
                    var it = c.TryItem(ic.Item);
                    int have = s.Count(ic.Item);
                    Ui.Text($"{(have >= ic.Count ? "✔" : "✘")} {it?.Name ?? ic.Item} ×{ic.Count}   (have {have})", "small " + (have >= ic.Count ? "good" : "bad"), undiscoveredNeed);
                }
                return;
            }
            var result = c.Item(r.Result);
            ItemUi.Detail(w, result);
            bool locked = !Crafting.IsUnlocked(s, r);
            if (locked)
            {
                var kind = Crafting.StationFor(r.Station);
                Ui.Text($"🔒 Requires {StationProgress.TierName(r.Tier)} ({StationProgress.Id(kind)} is {StationProgress.LevelName(s, kind)})", "small bad", w).style.whiteSpace = WhiteSpace.Normal;
            }
            var need = Ui.El("col", w); need.style.marginTop = 12;
            if (r.BaseItem != null)
            {
                var b = c.TryItem(r.BaseItem);
                int have = s.Count(r.BaseItem);
                Ui.Text($"{(have >= 1 ? "✔" : "✘")} {b?.Name ?? r.BaseItem} ×1   (have {have})", "small " + (have >= 1 ? "good" : "bad"), need);
            }
            foreach (var ic in r.Inputs)
            {
                var it = c.TryItem(ic.Item);
                int have = s.Count(ic.Item);
                Ui.Text($"{(have >= ic.Count ? "✔" : "✘")} {it?.Name ?? ic.Item} ×{ic.Count}   (have {have})", "small " + (have >= ic.Count ? "good" : "bad"), need);
            }
            Ui.Text($"{(s.Gold >= r.Gold ? "✔" : "✘")} {Ui.Gold(r.Gold)}", "small " + (s.Gold >= r.Gold ? "good" : "bad"), need);
            if (r.Station == CraftStation.Forge)
                Ui.Text($"★ {Math.Round(Crafting.MasterworkChance(c, s) * 100)}% chance of a Masterwork result", "small gold", need).style.whiteSpace = WhiteSpace.Normal;
            var btn = Ui.Button(r.Station == CraftStation.Alchemy ? "Brew" : r.Station == CraftStation.Enchant ? "Enchant" : "Craft", () =>
            {
                var res = Crafting.Craft(c, s, r);
                GameApp.I.Toast(res.Message);
                GameApp.I.Audio.Sfx(res.Ok ? "ui.buy" : "ui.error");
                if (res.Ok) GameApp.I.Persist();
                page.Refresh();
            }, "btn--primary", w);
            Ui.SetEnabled(btn, !locked && Crafting.HasInputs(s, r) && s.Gold >= r.Gold);
        }

        private void SalvageList(VisualElement list, VisualElement detail)
        {
            var gear = S.Inventory.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key)).Where(i => i != null && i.Type == ItemType.Equipment).ToList();
            var broken = S.Broken.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key)).Where(i => i != null).ToList();
            if (gear.Count == 0 && broken.Count == 0) Ui.Text("No spare gear in your bag.", "small dim", list);
            Ui.Text("Break spare gear into materials — worn gear yields less; the worst copy goes first. Broken gear can only be salvaged (GG-T41).", "small dim", detail).style.whiteSpace = WhiteSpace.Normal;
            foreach (var it in gear)
            {
                var item = it;
                var yield = Crafting.SalvagePreview(C, S, item.Id);
                string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
                ItemUi.Row(item, $"×{S.Count(item.Id)}", () => Confirm.Show($"Salvage {item.Name} into {y}?", "Salvage", () => Done(Crafting.Salvage(C, S, item.Id))), list, yield.Count > 0, Equipment.IsMasterwork(S, item.Id));
            }
            foreach (var it in broken)
            {
                var item = it;
                var yield = Crafting.SalvageBrokenPreview(C, item.Id);
                string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
                var row = Ui.MenuRow($"Broken {item.Name}", $"×{S.BrokenCount(item.Id)}",
                    () => Confirm.Show($"Salvage Broken {item.Name} into {y}?", "Salvage", () => Done(Crafting.SalvageBroken(C, S, item.Id))), list, Art.ItemIcon(item.Id), yield.Count > 0);
                row.Q<Label>("label").AddToClassList("dim");
            }
        }

        private void RepairList(VisualElement list, VisualElement detail)
        {
            Ui.Text("Each repair lowers the piece's max durability by 1, and costs more every time. An uneconomical repair offers salvage instead.", "small dim", detail).style.whiteSpace = WhiteSpace.Normal;
            bool any = false;
            foreach (var h in S.Roster)
            {
                var hero = h;
                var w = C.TryItem(h.WeaponId);
                if (w != null && w.Durability > 0)
                {
                    any = true;
                    int cost = Repair.Cost(C, S, w, h.WeaponDurability, h.WeaponRepairs, h.WeaponMastery);
                    bool bad = cost > 0 && Repair.IsUneconomical(w, h.WeaponDurability, h.WeaponRepairs, h.WeaponMastery);
                    string name = (h.WeaponMastery > 1f ? "★ " : "") + w.Name;
                    var row = Ui.MenuRow($"{C.Class(h.ClassId).Name} — {name}  {h.WeaponDurability}/{Repair.EffectiveMax(w, h.WeaponRepairs, h.WeaponMastery)}", cost > 0 ? Ui.Gold(cost) : "OK",
                        () => Done(Repair.RepairWeapon(C, S, hero)), list, null, cost > 0);
                    if (bad) row.Q<Label>("value").AddToClassList("warn");
                    if (bad) SalvageInsteadRow(list, hero, EquipmentSlot.Weapon, w, h.WeaponDurability, cost);
                }
                var a = C.TryItem(h.ArmorId);
                if (a != null && a.Durability > 0)
                {
                    any = true;
                    int cost = Repair.Cost(C, S, a, h.ArmorDurability, h.ArmorRepairs, h.ArmorMastery);
                    bool bad = cost > 0 && Repair.IsUneconomical(a, h.ArmorDurability, h.ArmorRepairs, h.ArmorMastery);
                    string name = (h.ArmorMastery > 1f ? "★ " : "") + a.Name;
                    var row = Ui.MenuRow($"{C.Class(h.ClassId).Name} — {name}  {h.ArmorDurability}/{Repair.EffectiveMax(a, h.ArmorRepairs, h.ArmorMastery)}", cost > 0 ? Ui.Gold(cost) : "OK",
                        () => Done(Repair.RepairArmor(C, S, hero)), list, null, cost > 0);
                    if (bad) row.Q<Label>("value").AddToClassList("warn");
                    if (bad) SalvageInsteadRow(list, hero, EquipmentSlot.Armor, a, h.ArmorDurability, cost);
                }
            }
            if (!any) Ui.Text("Nobody has gear that wears out. Equip forged weapons first.", "small dim", list).style.whiteSpace = WhiteSpace.Normal;
        }

        /// <summary>The "repair is a ripoff — salvage it instead" offer, shown right under an uneconomical
        /// repair row so both outcomes (repair cost vs. salvage yield) sit side by side (GG-T42).</summary>
        private void SalvageInsteadRow(VisualElement list, HeroSave hero, EquipmentSlot slot, ItemDef item, int durability, int repairCost)
        {
            var yield = Crafting.SalvageYield(C, item, durability);
            string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
            var row = Ui.MenuRow("   Salvage instead", y, () =>
                Confirm.Show($"Repair costs {Ui.Gold(repairCost)}. Scrap the {item.Name} instead for {y}? This destroys it.", "Salvage",
                    () => Done(Crafting.SalvageEquipped(C, S, hero, slot))), list);
            row.Q<Label>("label").AddToClassList("small");
            row.Q<Label>("value").AddToClassList("dim");
        }
    }

    // ── Alchemist: brew / enchant / heal party (§25.3) ───────────────────────────

    public sealed class AlchemistScreen : HubPage
    {
        private int tab;
        private RecipeDef selected;
        public override string Id => "alchemist";
        protected override string Title => "Alchemist";
        protected override string Backdrop => "swamp";
        protected override string Subtitle => StationProgress.HeaderText(S, StationKind.Alchemist);

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(new[] { "Brew", "Enchant", "Heal" }, tab, i => { tab = i; selected = null; Refresh(); }, body);
            var w = Ui.Window(null, body);
            w.style.flexGrow = 1; w.style.marginTop = 0;
            var list = Ui.List(w).contentContainer;
            var detail = Ui.Window(null, body);
            detail.style.minHeight = 330;
            if (tab == 0) BlacksmithScreen.RecipeList(list, detail, C, S, CraftStation.Alchemy, false, ref selected, this);
            else if (tab == 1) BlacksmithScreen.RecipeList(list, detail, C, S, CraftStation.Enchant, true, ref selected, this);
            else
            {
                int cost = Inn.Cost(C, S);
                foreach (var h in S.Roster)
                {
                    int miss = Inn.MissingHp(C, S, h);
                    Ui.MenuRow(C.Class(h.ClassId).Name, miss > 0 ? $"−{miss} HP" : "Healthy", null, list, null, miss > 0);
                }
                var b = Ui.Button(cost > 0 ? $"Heal party — {Ui.Gold(cost)}" : "Party healthy", () => Done(Inn.Rest(C, S)), "btn--primary", detail);
                Ui.SetEnabled(b, cost > 0 && S.Gold >= cost);
            }
        }
    }

    // ── Caravan upgrades (§25.12) ────────────────────────────────────────────────

    public sealed class CaravanScreen : HubPage
    {
        public override string Id => "caravan";
        protected override string Title => "Caravan";
        protected override string Backdrop => "desert";

        protected override void BuildBody(VisualElement body)
        {
            var head = Ui.Window(null, body);
            Ui.Text("The caravan travels with the party — its wagons are the shops between battles. Upgrade it for the long road.", "small", head).style.whiteSpace = WhiteSpace.Normal;
            var w = Ui.Window(null, body);
            w.style.flexGrow = 1;
            var list = Ui.List(w).contentContainer;
            foreach (var def in C.Caravan.Values)
            {
                var d = def;
                int lv = CaravanUpgrades.Level(S, d.Id);
                var next = CaravanUpgrades.NextLevel(C, S, d.Id);
                var card = Ui.El("card", list);
                card.name = "caravan-" + d.Id;
                var ic = Ui.El("thumb", card);
                ic.style.width = ic.style.height = 120;
                ic.style.backgroundImage = new StyleBackground(Art.ItemIcon(d.Icon) ?? Art.Orb);
                ic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                var col = Ui.El("col grow", card);
                var top = Ui.El("row", col);
                Ui.Text(d.Name, "h2 grow", top);
                Ui.Text($"Lv {lv}/{d.Levels.Count}", "small gold", top);
                Ui.Text(d.Description, "tiny dim", col).style.whiteSpace = WhiteSpace.Normal;
                if (lv > 0) Ui.Text("Now: " + d.Levels[lv - 1].Text, "tiny good", col);
                if (next != null) Ui.Text("Next: " + next.Text, "tiny", col);
                var b = Ui.Button(next == null ? "Maxed" : Ui.Gold(next.Cost), () => Confirm.Show($"Buy {d.Name} ({next.Text}) for {Ui.Gold(next.Cost)}?", "Buy", () => Done(CaravanUpgrades.Buy(C, S, d.Id))), "btn--small", card);
                Ui.SetEnabled(b, next != null && S.Gold >= next.Cost);
            }
        }
    }

    // ── Summon Circle (§25.10) ───────────────────────────────────────────────────

    public sealed class SummonScreen : HubPage
    {
        public override string Id => "summon";
        protected override string Title => "Summon Circle";
        protected override string Backdrop => "cityruins";

        protected override void BuildBody(VisualElement body)
        {
            int price = Summon.Price(C, S);
            var head = Ui.Window(null, body);
            Ui.Text($"Recruit a hero for {Ui.Gold(price)}. The price rises with each recruit.", "small", head).style.whiteSpace = WhiteSpace.Normal;
            var w = Ui.Window(null, body);
            w.style.flexGrow = 1;
            var list = Ui.List(w).contentContainer;
            foreach (var d in Summon.Pool(C, S))
            {
                var def = d;
                bool owned = S.Hero(d.Id) != null;
                var card = Ui.El("card", list);
                Ui.Thumb(d.Id, card, 140);
                var col = Ui.El("col grow", card);
                Ui.Text(d.Name, "h2", col);
                Ui.Text(Ui.Keywords(d.Card ?? d.Description ?? ""), "tiny dim", col).style.whiteSpace = WhiteSpace.Normal;
                var b = Ui.Button(owned ? "Joined" : "Recruit", () => Confirm.Show($"Recruit {def.Name} for {Ui.Gold(price)}?", "Recruit", () => Done(Summon.Recruit(C, S, def.Id))), "btn--small", card);
                Ui.SetEnabled(b, !owned && S.Gold >= price);
            }
        }
    }

    // ── Bounty board (§22.4) ─────────────────────────────────────────────────────

    public sealed class BountyScreen : HubPage
    {
        public override string Id => "bounties";
        protected override string Title => "Bounties";

        protected override void BuildBody(VisualElement body)
        {
            var act = Bounties.Active(C, S);
            if (act != null)
            {
                var a = Ui.Window("Active contract", body);
                Ui.Text($"{act.Name}: defeat {act.Kills} {C.TryClass(act.TargetClass)?.Name}", "small", a);
                Ui.Text($"Progress {S.Bounty.Progress}/{act.Kills}", "h2 gold", a);
                var row = Ui.El("row", a);
                var claim = Ui.Button("Claim", () => Done(Bounties.Claim(C, S)), "btn--primary", row);
                Ui.SetEnabled(claim, Bounties.IsComplete(C, S));
                Ui.Button("Abandon", () => Confirm.Show("Abandon this bounty?", "Abandon", () => Done(Bounties.Abandon(S))), "btn--danger", row);
            }
            var w = Ui.Window("Posted", body);
            w.style.flexGrow = 1;
            var list = Ui.List(w).contentContainer;
            foreach (var b in Bounties.Posted(C, S))
            {
                var bounty = b;
                var target = C.TryClass(b.TargetClass);
                var reward = C.TryItem(b.RewardItem);
                var card = Ui.El("card", list);
                if (target != null) Ui.Thumb(target.Id, card, 120);
                var col = Ui.El("col grow", card);
                Ui.Text(b.Name, "h2", col);
                Ui.Text($"Defeat {b.Kills}× {target?.Name}.  Reward {Ui.Gold(b.RewardGold)}{(reward != null ? " + " + reward.Name : "")}", "tiny dim", col).style.whiteSpace = WhiteSpace.Normal;
                var btn = Ui.Button(S.Bounty.ActiveId == b.Id ? "Active" : "Accept", () => Done(Bounties.Accept(C, S, bounty.Id)), "btn--small", card);
                Ui.SetEnabled(btn, S.Bounty.ActiveId == null);
            }
        }
    }

    // ── Bestiary ─────────────────────────────────────────────────────────────────

    public sealed class BestiaryScreen : HubPage
    {
        private string selected;
        public override string Id => "bestiary";
        protected override string Title => "Bestiary";

        protected override void BuildBody(VisualElement body)
        {
            var enemies = C.Classes.Values.Where(c => c.Team == Team.Enemy).OrderBy(c => c.Name).ToList();
            int seen = enemies.Count(e => S.Bestiary.TryGetValue(e.Id, out var b) && b.Seen);
            var w = Ui.Window($"Seen {seen}/{enemies.Count}", body);
            w.style.flexGrow = 1;
            var grid = Ui.List(w).contentContainer;
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            var detail = Ui.Window(null, body);
            detail.style.minHeight = 260;
            foreach (var e in enemies)
            {
                var def = e;
                S.Bestiary.TryGetValue(e.Id, out var entry);
                var t = Ui.Thumb(e.Id, grid, 160);
                t.name = "beast-" + e.Id;
                t.style.marginRight = 8; t.style.marginBottom = 8;
                if (entry == null || !entry.Seen) { t.style.unityBackgroundImageTintColor = new Color(0, 0, 0, 0.92f); }
                t.AddToClassList("card-tap");
                Ui.Tappable(t, () => { selected = def.Id; Show(detail, def, entry); });
            }
            if (selected != null) Show(detail, C.Class(selected), S.Bestiary.TryGetValue(selected, out var se) ? se : null);
            else Ui.Text("Tap a creature.", "small dim", detail);
        }

        private void Show(VisualElement w, ClassDef d, BestiaryEntry e)
        {
            w.Clear();
            if (e == null || !e.Seen) { Ui.Text("??? — not yet encountered", "h2 dim", w); return; }
            var top = Ui.El("row", w);
            Ui.Text(d.Name, "h2 grow", top);
            Ui.Text($"Defeated {e.Defeated}", "small gold", top);
            var lore = Ui.Text(Ui.Keywords(d.Lore ?? d.Description ?? ""), "small dim", w);
            lore.style.whiteSpace = WhiteSpace.Normal;
            if (d.DropTable != null && C.DropTables.TryGetValue(d.DropTable, out var drops) && drops.Entries.Count > 0)
            {
                Ui.Text("Drops", "small dim", w).style.marginTop = 6;
                var row = Ui.El("row", w);
                row.style.flexWrap = Wrap.Wrap;
                foreach (var de in drops.Entries.OrderByDescending(x => x.Chance))
                {
                    var it = C.TryItem(de.Item);
                    if (it == null) continue;
                    var tag = Ui.Text(it.Name, "small " + Ui.RarityClass(it.Rarity), row);
                    tag.style.marginRight = 10;
                }
            }
            if (e.Scanned)
            {
                Ui.Text(Ui.StatLine(d.BaseStats), "small", w);
                var kit = Ui.KitTraits(C, d);
                if (kit.Length > 0) Ui.Text(kit, "small gold", w).style.whiteSpace = WhiteSpace.Normal;
                Ui.Text(Gaiden.Core.Rules.EnemyPlanner.Describe(d.Targeting != Targeting.Auto ? d.Targeting : d.BaseStats.Intelligence >= 14 ? Targeting.Weakest : d.Tags.Contains("Humanoid") ? Targeting.Threat : Targeting.Closest), "small warn", w);
                Ui.Text(Gaiden.Core.Rules.EnemyPlanner.DescribeBrain(d.Brain), "small", w).style.whiteSpace = WhiteSpace.Normal;
            }
            else Ui.Text("Scan it in battle to reveal its stats.", "tiny dim", w);
        }
    }

    // ── Settings (§31) ───────────────────────────────────────────────────────────

    public sealed class SettingsScreen : HubPage
    {
        public override string Id => "settings";
        protected override string Title => "Settings";

        protected override void BuildBody(VisualElement body)
        {
            var st = S.Settings;
            var w = Ui.Window(null, body);
            void Toggle(string label, bool value, Action<bool> set) => Ui.MenuRow(label, value ? "ON" : "OFF", () => { set(!value); App.ApplySettings(); App.Persist(); Refresh(); }, w);
            void Cycle(string label, float value, float[] opts, string fmt, Action<float> set)
            {
                int i = Array.FindIndex(opts, o => Mathf.Abs(o - value) < 0.01f);
                Ui.MenuRow(label, string.Format(fmt, value), () => { set(opts[(i + 1) % opts.Length]); App.ApplySettings(); App.Persist(); Refresh(); }, w);
            }
            void CycleTheme()
            {
                var names = Art.Themes.Select(t => t.Name).ToArray();
                int i = Array.IndexOf(names, st.MenuTheme);
                st.MenuTheme = names[(i + 1) % names.Length];
                App.ApplySettings(); App.Persist(); Refresh();
            }
            Ui.MenuRow("Menu theme", st.MenuTheme, CycleTheme, w);
            void CycleFont()
            {
                var names = Fonts.Presets.Select(p => p.Name).ToArray();
                int i = Array.IndexOf(names, st.FontName);
                st.FontName = names[(i + 1) % names.Length];
                App.ApplySettings(); App.Persist(); Refresh();
            }
            Ui.MenuRow("Font", st.FontName, CycleFont, w);
            var diff = st.Difficulty;
            void CycleDifficulty()
            {
                var presets = new[] { "Easy", "Normal", "Hard", "Custom" };
                int i = Array.IndexOf(presets, diff.Preset);
                diff.Preset = presets[(i + 1) % presets.Length];
                // Built-in presets are inert scaffolding for now (§22.9): landing on one resets every
                // multiplier to 1.0 so Custom is the only place values can actually move.
                if (diff.Preset != "Custom")
                {
                    diff.EnemyHp = diff.EnemyDamage = diff.EnemySpeed = diff.RewardGold = diff.RewardXp = diff.ItemDropRate = 1f;
                }
                App.Persist(); Refresh();
            }
            Ui.MenuRow("Difficulty", diff.Preset, CycleDifficulty, w);
            if (diff.Preset == "Custom")
            {
                var mults = new[] { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
                void CycleMult(string label, float value, Action<float> set)
                {
                    int i = Array.FindIndex(mults, o => Mathf.Abs(o - value) < 0.01f);
                    if (i < 0) i = 2; // default to the ×1 slot if the stored value doesn't land on a step
                    var row = Ui.MenuRow(label, $"×{value:0.##}", () => { set(mults[(i + 1) % mults.Length]); App.Persist(); Refresh(); }, w);
                    row.style.marginLeft = 40;
                }
                CycleMult("Enemy HP", diff.EnemyHp, v => diff.EnemyHp = v);
                CycleMult("Enemy damage", diff.EnemyDamage, v => diff.EnemyDamage = v);
                CycleMult("Enemy speed", diff.EnemySpeed, v => diff.EnemySpeed = v);
                CycleMult("Reward gold", diff.RewardGold, v => diff.RewardGold = v);
                CycleMult("Reward XP", diff.RewardXp, v => diff.RewardXp = v);
                CycleMult("Item drop rate", diff.ItemDropRate, v => diff.ItemDropRate = v);
                Ui.Text("Not wired into battles yet — these numbers don't do anything, but your picks are saved.", "tiny dim", w).style.whiteSpace = WhiteSpace.Normal;
            }
            Cycle("Music volume", st.MusicVolume, new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, "{0:P0}", v => st.MusicVolume = v);
            Cycle("Sound volume", st.SfxVolume, new[] { 0f, 0.25f, 0.5f, 0.85f, 1f }, "{0:P0}", v => st.SfxVolume = v);
            Toggle("Mute music", st.MuteMusic, v => st.MuteMusic = v);
            Toggle("Mute sound", st.MuteSfx, v => st.MuteSfx = v);
            Cycle("Battle speed", st.BattleSpeed, new[] { 1f, 1.5f, 2f, 3f }, "×{0:0.#}", v => st.BattleSpeed = v);
            Toggle("Drag timer (4 s)", st.DragTimer, v => st.DragTimer = v);
            Toggle("Colorblind palette", st.ColorblindMode, v => st.ColorblindMode = v);
            Toggle("Pincer close-ups", st.PincerCutIns, v => st.PincerCutIns = v);
            Toggle("Confirm before acting", st.ConfirmActions, v => st.ConfirmActions = v);
            Toggle("Reduce motion", st.ReduceMotion, v => st.ReduceMotion = v);
            var help = Ui.Window("How to play", body);
            var t = Ui.Text(
                "• Drag a hero tile by tile. Anyone in the way swaps into the tile you left.\n" +
                "• Trap enemies in a straight line between two heroes to PINCER them. Allies next to the attackers join in.\n" +
                "• Moving never hurts. Only pincers and spells deal damage.\n" +
                "• Time moves only when you act. Enemies attack when their icon reaches the right end of the timeline.\n" +
                "• Hit an enemy in the shaded zone to push its turn back.\n" +
                "• Pincers fill your mana orbs. Spend them on spells; skills and items cost a turn.\n" +
                "• Pick a slot, then a target, then Confirm. Spells charge on the timeline before they land.\n" +
                "• Roles need each other: Tanks Taunt so enemies leave your casters alone, Healers mend (WIS), Mages hit hardest with spells (INT), Strikers swing Cleave and War Cry (STR).\n" +
                "• Fallen enemies sometimes drop a pickup. Walk a hero over it before it fades.\n" +
                "• The shield skips to the next enemy turn and guards everyone.", "small", help);
            t.style.whiteSpace = WhiteSpace.Normal;
        }
    }
}
