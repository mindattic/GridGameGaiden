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

        private HashSet<string> excludedGuests = new HashSet<string>();
        private int unlockedSlotsForGuests = 2;

        public void Setup(BattleSetup setup)
        {
            excludedGuests = setup.ExcludedGuests ?? new HashSet<string>();
            unlockedSlotsForGuests = setup.UnlockedSlots;
            carriedOrbs = Math.Max(0, setup.StartOrbs);
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

        /// <summary>Guest allies of a wave join the hero team on their tile (or the nearest free one).</summary>
        private void SpawnGuests(WaveDef wave)
        {
            if (wave.Guests == null) return;
            foreach (var g in wave.Guests)
            {
                var def = C.TryClass(g.Class);
                if (def == null || def.Team != Team.Hero || excludedGuests.Contains(def.Id)) continue;
                bool already = false;
                foreach (var u in S.Units) if (u.Alive && u.IsHero && u.Def.Id == def.Id) already = true;
                if (already) continue;
                var at = new Coord(g.X, g.Y);
                if (!S.FreeForSpawn(at)) { var free = RandomFreeTile(1, 1, 3, BattleConfig.Rows - 1); if (free == null) continue; at = free.Value; }
                var spec = new HeroSpec { ClassId = def.Id, Level = Math.Max(g.Level, S.RecommendedLevel), Bar = new List<string>(def.DefaultLoadout) };
                var unit = SpawnHero(spec, at, unlockedSlotsForGuests);
                unit.IsGuest = true;
                Emit(new GuestJoinedEvent { UnitId = unit.Id, At = at });
                Announce($"{unit.Name} joins the fight!", unit.Id);
            }
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
                if (w != null && w.Slot == EquipmentSlot.Weapon && have > 0) { slot.WeaponId = w.Id; slot.WeaponDurability = w.Durability; spec.ItemCounts[w.Id] = have - 1; }
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
        private int carriedOrbs;
        private int pendingBankBonus;
        private readonly List<int> startProtected = new List<int>();

        /// <summary>Gear with BattleStartManaOrbs (Mage/Wizard Robe) seeds orbs, capped by the bank (§24.8).</summary>
        private void ApplyBattleStartOrbs()
        {
            for (int i = 0; i < pendingStartOrbs; i++) MintOrb(-1, "battle start");
            pendingStartOrbs = 0;
            for (int i = 0; i < carriedOrbs; i++) MintOrb(-1, "carried");
            carriedOrbs = 0;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Waves
        // ─────────────────────────────────────────────────────────────────────────────

        public void SpawnWave(int index)
        {
            var wave = S.Stage.Waves[index];
            var started = new WaveStartedEvent { Wave = index, TotalWaves = S.Stage.Waves.Count };
            SpawnGuests(wave);
            foreach (var sp in wave.Enemies)
            {
                if (sp.DelayTicks > 0)
                {
                    S.PendingSpawns.Add(new PendingSpawn { Wave = index, Spawn = sp, TriggerClock = S.Clock + sp.DelayTicks });
                    continue;
                }
                var def = C.TryClass(sp.Class);
                if (def == null) continue;
                int level = S.Stage.IsTest ? sp.Level : Math.Max(sp.Level, S.RecommendedLevel) + S.NewGamePlus * BattleConfig.NgPlusLevelStep;
                var u = SpawnEnemy(def, level, sp.X, sp.Y);
                if (u == null) continue;
                started.Spawned.Add(u.Id);
                if (def.SnakeSegments > 0) GrowSnake(u, def.SnakeSegments, started.Spawned);
            }
            Emit(started);
        }

        /// <summary>Resolves any delayed reinforcements (§14.8) for the current wave whose trigger tick has arrived.</summary>
        private void ResolvePendingSpawns()
        {
            if (S.PendingSpawns.Count == 0) return;
            List<int> spawned = null;
            string speakerClassId = null, line = null;
            foreach (var pending in S.PendingSpawns)
            {
                if (pending.Consumed || pending.Wave != S.Wave || S.Clock < pending.TriggerClock) continue;
                pending.Consumed = true;
                var def = C.TryClass(pending.Spawn.Class);
                if (def == null) continue;
                int level = S.Stage.IsTest ? pending.Spawn.Level : Math.Max(pending.Spawn.Level, S.RecommendedLevel) + S.NewGamePlus * BattleConfig.NgPlusLevelStep;
                var u = SpawnEnemy(def, level, pending.Spawn.X, pending.Spawn.Y);
                if (u == null) continue;
                spawned ??= new List<int>();
                spawned.Add(u.Id);
                if (def.SnakeSegments > 0) GrowSnake(u, def.SnakeSegments, spawned);
                if (speakerClassId == null)
                {
                    var wave = S.Stage.Waves[pending.Wave];
                    if (!string.IsNullOrEmpty(wave.SurpriseLine)) { speakerClassId = wave.SurpriseSpeaker; line = wave.SurpriseLine; }
                }
            }
            if (spawned != null) Emit(new ReinforcementsArrivedEvent { Wave = S.Wave, Spawned = spawned, SpeakerClassId = speakerClassId, Line = line });
            S.PendingSpawns.RemoveAll(p => p.Consumed);
        }

        internal Unit SpawnEnemy(ClassDef def, int level, int x, int y)
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
            if (def.ArmorPlate > 0) u.PlateHp = u.PlateMax = (int)Math.Round(def.ArmorPlate * (1f + BattleConfig.PlatePerLevel * (u.Level - 1)));
            S.Units.Add(u);
            if (u.PlateMax > 0) FaceTarget(u, false);
            S.Timeline.Add(new TimelineEntry
            {
                Id = S.NextEntryId++,
                Kind = TimelineKind.Turn,
                OwnerId = u.Id,
                Ip = S.Rng.Range(0, BattleConfig.SpawnStaggerIp + 1),
            });
            if (!def.HasTag("Remains")) S.Results.Seen.Add(def.Id);
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
                    if (S.FreeForSpawn(c)) { next = c; break; }
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
                    if (!S.FreeForSpawn(c) || S.TrapAt(c) != null) return false;
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
