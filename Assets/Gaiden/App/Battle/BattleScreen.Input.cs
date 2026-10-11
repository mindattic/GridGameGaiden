using System;
using System.Collections.Generic;
using System.Linq;
using Gaiden.Core;
using Gaiden.Core.Battle;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaiden.App
{
    public sealed partial class BattleScreen
    {
        private const float DragThreshold = 18f;

        private void OnPointerDown(PointerDownEvent evt)
        {
            ClearHint();
            if (!CanCommand() || pressing) return;
            var tile = board.TileAt(evt.localPosition);
            var unit = State.UnitAt(tile);

            if (TargetingSlot >= 0)
            {
                // Show the area this pick would hit; commit on release over the same tile.
                pressing = true;
                pressPos = evt.localPosition;
                dragTile = tile;
                PreviewTarget(tile);
                board.Root.CapturePointer(evt.pointerId);
                return;
            }

            if (unit != null && unit.IsHero)
            {
                SelectHero(unit.Id);
                if (!BattleEngine.CanMove(State, unit.Id)) { ShowBanner($"{unit.Name} can't move"); return; }
                pressing = true;
                dragHeroId = unit.Id;
                pressPos = evt.localPosition;
                dragTile = unit.Pos;
                dragPath.Clear();
                preview = null;
                board.Root.CapturePointer(evt.pointerId);
            }
            else if (unit != null)
            {
                panelUnitId = unit.Id;
                SyncPanel();
                App.Audio.Sfx("ui.click");
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!pressing) return;
            Vector2 local = evt.localPosition;
            if (TargetingSlot >= 0)
            {
                var t = board.TileAt(local);
                if (t != dragTile) { dragTile = t; PreviewTarget(t); }
                return;
            }
            if (!dragging)
            {
                if ((local - pressPos).magnitude < DragThreshold) return;
                BeginDrag();
            }
            var v = board.Units[dragHeroId];
            // The hero follows the finger, lifted slightly so it isn't hidden under it.
            v.Root.style.left = Mathf.Clamp(local.x - BoardView.Tile / 2f, -BoardView.Tile * 0.3f, BoardView.Tile * (BattleConfig.Cols - 0.7f));
            v.Root.style.top = Mathf.Clamp(local.y - BoardView.Tile * 0.62f, -BoardView.Tile * 0.3f, BoardView.Tile * (BattleConfig.Rows - 0.7f));
            var over = board.TileAt(new Vector2(local.x, local.y - BoardView.Tile * 0.12f));
            ExtendPath(over);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!pressing) return;
            board.Root.ReleasePointer(evt.pointerId);
            if (TargetingSlot >= 0)
            {
                pressing = false;
                var t = board.TileAt(evt.localPosition);
                var hero = State.UnitById(SelectedHeroId);
                var a = hero == null ? null : BattleEngine.SlotAbility(State, hero, TargetingSlot);
                bool legal = hero != null && a != null && a.Mode != TargetMode.Auto && TargetResolver.IsValidPick(State, a, hero, t);
                if (legal && (!ConfirmActions || AutoPlay)) Commit(new UseSlotCommand { HeroId = hero.Id, Slot = TargetingSlot, Target = t });
                else if (legal && PendingTarget == t) ConfirmPending(); // a second tap on the same pick confirms
                else if (legal) SetPending(t);
                else
                {
                    if (a == null || a.Mode != TargetMode.Auto) App.Audio.Sfx("ui.error");
                    RedrawTargeting();
                }
                return;
            }
            if (dragging) CommitDrag();
            pressing = false;
        }

        private void BeginDrag()
        {
            dragging = true;
            dragTimeLeft = BattleConfig.DragTimerSeconds;
            var v = board.Units[dragHeroId];
            v.Root.BringToFront();
            v.Root.style.scale = new Scale(new Vector2(1.12f, 1.12f));
            v.SetGlow(new Color(1f, 0.95f, 0.6f), 0.9f);
            App.Audio.Sfx("pickup");
            ShowBanner("");
        }

        /// <summary>Turns the tile under the finger into cardinal steps from the last logical tile (one tile at a time, §1.1).</summary>
        private void ExtendPath(Coord target)
        {
            int guard = 0;
            while (dragTile != target && guard++ < 16)
            {
                int dx = target.X - dragTile.X, dy = target.Y - dragTile.Y;
                Coord step = Math.Abs(dx) >= Math.Abs(dy)
                    ? new Coord(dragTile.X + Math.Sign(dx), dragTile.Y)
                    : new Coord(dragTile.X, dragTile.Y + Math.Sign(dy));
                var candidate = new List<Coord>(dragPath) { step };
                var pv = BattleEngine.PreviewMove(State, dragHeroId, candidate);
                if (pv.StepsTaken.Count < candidate.Count)
                {
                    // Blocked (edge / boss / snake): try the other axis once, else hold position.
                    if (dx != 0 && dy != 0)
                    {
                        Coord alt = Math.Abs(dx) >= Math.Abs(dy)
                            ? new Coord(dragTile.X, dragTile.Y + Math.Sign(dy))
                            : new Coord(dragTile.X + Math.Sign(dx), dragTile.Y);
                        candidate[candidate.Count - 1] = alt;
                        pv = BattleEngine.PreviewMove(State, dragHeroId, candidate);
                        if (pv.StepsTaken.Count < candidate.Count) return;
                        step = alt;
                    }
                    else return;
                }
                dragPath.Add(step);
                dragTile = step;
                preview = pv;
                App.Audio.Sfx("move", 0.5f);
                ApplyPreview(pv, dragHeroId);
            }
        }

        /// <summary>Slide displaced actors to their preview tiles and draw the pincers this drop would make.</summary>
        private void ApplyPreview(MovePreview pv, int heroId)
        {
            foreach (var kv in pv.Positions)
            {
                if (kv.Key == heroId) continue;
                if (!board.Units.TryGetValue(kv.Key, out var v)) continue;
                if (v.Tile != kv.Value)
                {
                    App.Audio.Sfx("slide", 0.6f);
                    var from = BoardView.PosOf(v.Tile);
                    v.Tile = kv.Value;
                    App.StartCoroutine(Tween.Move(v.Root, from, BoardView.PosOf(kv.Value), 0.1f));
                }
            }
            board.Lines.ClearLines();
            board.ClearMarks();
            foreach (var u in State.Units)
                if (board.Units.TryGetValue(u.Id, out var uv) && u.Alive && u.Id != heroId) uv.SetGlow(Color.white, 0f);
            foreach (var p in pv.Pincers)
            {
                pv.Positions.TryGetValue(p.A.Id, out var a);
                pv.Positions.TryGetValue(p.B.Id, out var b);
                // Preview: the group outline and edge links the drop would make (no line across the art).
                var group = new List<(Coord, int, int)> { (a, p.A.W, p.A.H), (b, p.B.W, p.B.H) };
                foreach (var o in p.Opponents) group.Add((pv.Positions.TryGetValue(o.Id, out var op) ? op : o.Pos, o.W, o.H));
                board.Lines.Outline(BoardView.GroupRect(group), new Color(1f, 0.85f, 0.3f, 0.9f), 8f, 1f);
                foreach (var (sup, partner) in p.SupportersA.Select(x => (x, a)).Concat(p.SupportersB.Select(x => (x, b))))
                {
                    var sp = pv.Positions.TryGetValue(sup.Id, out var spp) ? spp : sup.Pos;
                    var (la, lb) = BoardView.EdgeLink(sp, partner);
                    board.Lines.Add(la, lb, new Color(0.4f, 0.8f, 1f, 0.9f), 10f);
                }
                foreach (var o in p.Opponents)
                {
                    foreach (var t in o.Tiles()) board.Mark(t, new Color(1f, 0.25f, 0.2f), 0.32f);
                    if (board.Units.TryGetValue(o.Id, out var ov)) ov.SetGlow(new Color(1f, 0.3f, 0.2f), 0.8f);
                }
                foreach (var s in p.SupportersA) if (board.Units.TryGetValue(s.Id, out var sv)) sv.SetGlow(new Color(0.4f, 0.8f, 1f), 0.7f);
                foreach (var s in p.SupportersB) if (board.Units.TryGetValue(s.Id, out var sv)) sv.SetGlow(new Color(0.4f, 0.8f, 1f), 0.7f);
            }
            if (pv.Pincers.Count > 0) ShowBanner(pv.Pincers.Count > 1 ? $"PINCER ×{pv.Pincers.Count}!" : "PINCER!");
            else ShowBanner("");
        }

        private void CommitDrag()
        {
            if (!dragging) return;
            dragging = false;
            pressing = false;
            board.Lines.ClearLines();
            board.ClearMarks();
            var v = board.Units[dragHeroId];
            v.Root.style.scale = new Scale(Vector2.one);
            if (dragPath.Count == 0)
            {
                board.Place(v, State.UnitById(dragHeroId).Pos);
                SyncAll();
                return;
            }
            float timeLeft = App.Save != null && App.Save.Settings.DragTimer ? Mathf.Max(0f, dragTimeLeft) : 0f;
            var cmd = new MoveCommand { HeroId = dragHeroId, Path = new List<Coord>(dragPath), TimeLeftSeconds = timeLeft };
            // Snap the dragged hero onto its final tile before playback.
            App.StartCoroutine(Tween.Move(v.Root, new Vector2(v.Root.resolvedStyle.left, v.Root.resolvedStyle.top), BoardView.PosOf(dragTile), 0.06f));
            Commit(cmd, true);
        }

        private void PreviewTarget(Coord tile)
        {
            var hero = State.UnitById(SelectedHeroId);
            var a = hero == null ? null : BattleEngine.SlotAbility(State, hero, TargetingSlot);
            if (a == null || a.Mode == TargetMode.Auto) return;
            board.ClearMarks();
            foreach (var vt in BattleEngine.ValidTargets(State, hero.Id, TargetingSlot)) board.Mark(vt, new Color(1f, 0.82f, 0.3f), 0.25f);
            if (!TargetResolver.IsValidPick(State, a, hero, tile)) return;
            bool harmful = a.BaseDamage > 0 || a.Filter == TargetFilter.EnemyOnly;
            var color = harmful ? new Color(1f, 0.3f, 0.25f) : new Color(0.4f, 0.9f, 0.5f);
            if (a.IsTeleport) { board.Mark(tile, new Color(0.5f, 0.8f, 1f), 0.6f); return; }
            foreach (var t in TargetResolver.Tiles(tile, a.Shape, a.Radius)) board.Mark(t, color, 0.38f);
        }
    }
}
