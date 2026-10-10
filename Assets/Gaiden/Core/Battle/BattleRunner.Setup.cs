using System;
using System.Collections.Generic;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;

namespace Gaiden.Core.Battle
{
    internal sealed partial class BattleRunner
    {
        private static readonly Coord[] DefaultHeroStarts =
        {
            new Coord(1, 6), new Coord(4, 6), new Coord(2, 7), new Coord(3, 7),
            new Coord(0, 7), new Coord(5, 7),
        };

        public void Setup(BattleSetup setup)
        {
            int i = 0;
            foreach (var spec in setup.Party)
            {
                var start = spec.Start ?? DefaultHeroStarts[Math.Min(i, DefaultHeroStarts.Length - 1)];
                if (S.UnitAt(start) != null) start = RandomFreeTile(1, 1, 4, BattleConfig.Rows - 1) ?? start;
                SpawnHero(spec, start, setup.UnlockedSlots);
                i++;
            }
            if (pendingBankBonus > 0) S.Bank = new ManaBank(ManaBank.DefaultCapacity + System.Math.Min(BattleConfig.MaxBankCapacityBonus, pendingBankBonus));
            ApplyBattleStartOrbs();
            var prot = C.TryBuff(BattleConfig.ShieldBuffId);
            if (prot != null)
                foreach (var id in startProtected) { var h = S.UnitById(id); if (h != null) ApplyBuff(h, prot, prot.Duration); }
            SpawnWave(0);
        }

        private Unit SpawnHero(HeroSpec spec, Coord at, int unlockedSlots)
        {
            var def = C.Class(spec.ClassId);
            var u = new Unit
            {
                Id = S.NextUnitId++,
                Def = def,
                Team = Team.Hero,
                Level = Math.Max(1, spec.Level),
                Pos = at,
                UnlockedSlots = Math.Max(0, Math.Min(MaxSlots, unlockedSlots)),
            };
            u.LevelStats = Formulas.StatsAtLevel(def, u.Level);
            foreach (var itemId in spec.EquippedItemIds)
            {
                var item = C.TryItem(itemId);
                if (item == null) continue;
                u.GearStats = u.GearStats + item.Stats;
                pendingStartOrbs += Math.Max(0, item.BattleStartManaOrbs);
                pendingBankBonus += Math.Max(0, item.BankCapacityBonus);
                u.PincerDamageBonus += item.PincerDamageBonus;
                u.RegenPerTurn += item.RegenPerTurn;
                if (item.StartsProtected && !startProtected.Contains(u.Id)) startProtected.Add(u.Id);
                foreach (var kv in item.Resistances)
                {
                    u.GearResist.TryGetValue(kv.Key, out var cur);
                    u.GearResist[kv.Key] = (cur == 0f ? 1f : cur) * kv.Value;
                }
            }
            var weapon = C.TryItem(spec.WeaponId);
            if (weapon != null)
            {
                u.WeaponId = weapon.Id;
                u.WeaponPower = weapon.BaseDamage;
                u.WeaponDurability = weapon.Durability > 0 ? Math.Max(0, spec.WeaponDurability > 0 ? spec.WeaponDurability : weapon.Durability) : 0;
                if (!spec.EquippedItemIds.Contains(weapon.Id))
                {
                    u.GearStats = u.GearStats + weapon.Stats;
                }
            }
            var armor = C.TryItem(spec.ArmorId);
            if (armor != null && armor.Durability > 0)
            {
                u.ArmorId = armor.Id;
                u.ArmorDurability = spec.ArmorDurability > 0 ? spec.ArmorDurability : armor.Durability;
            }
            u.MaxHp = Formulas.MaxHp(u.Effective, u.Level);
            u.Hp = spec.Hp > 0 ? Math.Min(spec.Hp, u.MaxHp) : u.MaxHp;

            for (int s = 0; s < MaxSlots; s++)
            {
                string entry = s < spec.Bar.Count ? spec.Bar[s] : null;
                u.Bar.Add(MakeSlot(entry, spec));
            }
            S.Units.Add(u);
            return u;
        }

        private AbilitySlot MakeSlot(string entry, HeroSpec spec)
        {
            var slot = new AbilitySlot();
            if (string.IsNullOrEmpty(entry)) return slot;
            if (entry.StartsWith("weapon:", StringComparison.Ordinal))
            {
                var w = C.TryItem(entry.Substring(7));
                spec.ItemCounts.TryGetValue(entry.Substring(7), out var have);
                if (w != null && w.Slot == EquipmentSlot.Weapon && have > 0) { slot.WeaponId = w.Id; spec.ItemCounts[w.Id] = have - 1; }
                return slot;
            }
            if (entry.StartsWith("item:", StringComparison.Ordinal))
            {
                // "item:<id>" draws charges from the owned count; "item:<id>:<n>" is a preset stack.
                var parts = entry.Split(':');
                var item = C.TryItem(parts[1]);
                if (item == null) return slot;
                slot.ItemId = item.Id;
                if (parts.Length >= 3 && int.TryParse(parts[2], out var preset))
                {
                    slot.MaxCharges = slot.Charges = preset;
                }
                else
                {
                    spec.ItemCounts.TryGetValue(item.Id, out var owned);
                    slot.MaxCharges = item.MaxStack;
                    slot.Charges = Math.Min(owned, item.MaxStack);
                    slot.FromInventory = true;
                    // Two slots of the same item split the owned stack rather than duplicating it.
                    spec.ItemCounts[item.Id] = Math.Max(0, owned - slot.Charges);
                }
                return slot;
            }
            if (C.TryAbility(entry) != null) slot.AbilityId = entry;
            return slot;
        }

        private int pendingStartOrbs;
        private int pendingBankBonus;
        private readonly List<int> startProtected = new List<int>();

        /// <summary>Gear with BattleStartManaOrbs (Mage/Wizard Robe) seeds random WUBRG orbs, capped by the bank (§24.8).</summary>
        private void ApplyBattleStartOrbs()
        {
            for (int i = 0; i < pendingStartOrbs; i++) MintOrb(RandomColor(), -1, "battle start");
            pendingStartOrbs = 0;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Waves
        // ─────────────────────────────────────────────────────────────────────────────

        public void SpawnWave(int index)
        {
            var wave = S.Stage.Waves[index];
            var started = new WaveStartedEvent { Wave = index, TotalWaves = S.Stage.Waves.Count };
            foreach (var sp in wave.Enemies)
            {
                var def = C.TryClass(sp.Class);
                if (def == null) continue;
                int level = S.Stage.IsTest ? sp.Level : Math.Max(sp.Level, S.RecommendedLevel);
                var u = SpawnEnemy(def, level, sp.X, sp.Y);
                if (u == null) continue;
                started.Spawned.Add(u.Id);
                if (def.SnakeSegments > 0) GrowSnake(u, def.SnakeSegments, started.Spawned);
            }
            Emit(started);
        }

        private Unit SpawnEnemy(ClassDef def, int level, int x, int y)
        {
            int w = Math.Max(1, def.Footprint?.W ?? 1), h = Math.Max(1, def.Footprint?.H ?? 1);
            Coord? at = null;
            if (x >= 0 && y >= 0 && FootprintFree(new Coord(x, y), w, h)) at = new Coord(x, y);
            if (at == null) at = RandomFreeTile(w, h, 0, 4) ?? RandomFreeTile(w, h, 0, BattleConfig.Rows - 1);
            if (at == null) return null;
            var u = new Unit
            {
                Id = S.NextUnitId++,
                Def = def,
                Team = Team.Enemy,
                Level = Math.Max(1, level),
                Pos = at.Value,
                W = w,
                H = h,
            };
            u.LevelStats = Formulas.StatsAtLevel(def, u.Level);
            u.MaxHp = Formulas.MaxHp(u.Effective, u.Level);
            u.Hp = u.MaxHp;
            S.Units.Add(u);
            S.Timeline.Add(new TimelineEntry
            {
                Id = S.NextEntryId++,
                Kind = TimelineKind.Turn,
                OwnerId = u.Id,
                Ip = S.Rng.Range(0, BattleConfig.SpawnStaggerIp + 1),
            });
            S.Results.Seen.Add(def.Id);
            return u;
        }

        /// <summary>Segmented snake (§14.2): body segments trail the head on free adjacent tiles; no timeline icon.</summary>
        private void GrowSnake(Unit head, int segments, List<int> spawned)
        {
            head.SnakeHeadId = head.Id;
            head.SnakeIndex = 0;
            var prev = head.Pos;
            for (int i = 1; i <= segments; i++)
            {
                Coord? next = null;
                foreach (var d in new[] { Coord.Up, Coord.Left, Coord.Right, Coord.Down })
                {
                    var c = prev + d;
                    if (BattleState.InBounds(c) && S.UnitAt(c) == null) { next = c; break; }
                }
                if (next == null) break;
                var seg = new Unit
                {
                    Id = S.NextUnitId++,
                    Def = head.Def,
                    Team = Team.Enemy,
                    Level = head.Level,
                    Pos = next.Value,
                    SnakeHeadId = head.Id,
                    SnakeIndex = i,
                    LevelStats = head.LevelStats.Clone(),
                };
                seg.MaxHp = seg.Hp = Math.Max(1, head.MaxHp / 2);
                S.Units.Add(seg);
                spawned.Add(seg.Id);
                prev = next.Value;
            }
        }

        private bool FootprintFree(Coord anchor, int w, int h)
        {
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    var c = new Coord(anchor.X + dx, anchor.Y + dy);
                    if (!BattleState.InBounds(c) || S.UnitAt(c) != null || S.TrapAt(c) != null) return false;
                }
            return true;
        }

        private Coord? RandomFreeTile(int w, int h, int minRow, int maxRow)
        {
            var free = new List<Coord>();
            for (int y = minRow; y <= maxRow; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                    if (FootprintFree(new Coord(x, y), w, h)) free.Add(new Coord(x, y));
            if (free.Count == 0) return null;
            return free[S.Rng.Range(0, free.Count)];
        }
    }
}
