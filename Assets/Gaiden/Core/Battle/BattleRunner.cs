using System;
using System.Collections.Generic;
using Gaiden.Core.Content;
using Gaiden.Core.Rules;

namespace Gaiden.Core.Battle
{
    /// <summary>Executes one command against a state, appending events. Split across partial files by concern.</summary>
    internal sealed partial class BattleRunner
    {
        public const int MaxSlots = 5;

        public readonly BattleState S;
        private readonly List<BattleEvent> ev;
        public bool PreviewOnly;

        private ContentDb C => S.Content;

        public BattleRunner(BattleState s, List<BattleEvent> events)
        {
            S = s;
            ev = events;
        }

        private void Emit(BattleEvent e)
        {
            if (!PreviewOnly) ev.Add(e);
        }

        private void Announce(string text, int unitId = -1) => Emit(new AnnounceEvent { Text = text, UnitId = unitId });

        // ─────────────────────────────────────────────────────────────────────────────
        // Command dispatch
        // ─────────────────────────────────────────────────────────────────────────────

        public void Apply(BattleCommand cmd)
        {
            if (S.IsOver) { Emit(new AbilityRefusedEvent { CasterId = -1, Reason = "Battle is over" }); return; }

            bool acted;
            bool shield = false;
            switch (cmd)
            {
                case MoveCommand m: acted = DoMove(m); break;
                case UseSlotCommand u: acted = DoUseSlot(u); break;
                case ShieldCommand _: acted = DoShield(); shield = true; break;
                default: acted = false; break;
            }
            if (!acted) return;

            S.Results.ActionsTaken++;
            CheckEnd();
            if (!S.IsOver)
            {
                if (shield) AdvanceUntilEnemyTurn();
                else AdvanceClock(BattleConfig.ActionTicks);
            }
            if (!S.IsOver) CheckEnd();
            if (!S.IsOver) ReturnControl();
            S.Results.TicksElapsed = (int)S.Clock;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Shield (§2.5)
        // ─────────────────────────────────────────────────────────────────────────────

        private bool DoShield()
        {
            Emit(new ActionBeganEvent { ActorId = -1, Label = "Shield" });
            var prot = C.TryBuff(BattleConfig.ShieldBuffId);
            if (prot != null)
                foreach (var h in S.LivingList(Team.Hero)) ApplyBuff(h, prot, prot.Duration);
            S.PendingTimeBankSeconds = 0f;
            return true;
        }

        /// <summary>Power Amplifier (§1.2.6): with none on the board, roll the luck-scaled chance to place one on a free tile.</summary>
        private void MaybeSpawnAmplifier()
        {
            if (S.Amplifier.HasValue || S.IsOver) return;
            float luck = 0f;
            foreach (var h in S.Living(Team.Hero)) luck += h.Effective.Luck;
            if (!S.Rng.Chance(Formulas.AmplifierChance(luck))) return;
            var free = new List<Coord>();
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    if (S.FreeForSpawn(c) && S.TrapAt(c) == null) free.Add(c);
                }
            if (free.Count == 0) return;
            var at = free[S.Rng.Range(0, free.Count)];
            S.Amplifier = at;
            Emit(new AmplifierSpawnedEvent { At = at });
            Announce("Power Amplifier!");
        }

        /// <summary>Control returns to the player: hero turn-unit buffs and skill cooldowns tick (§8.3, §4.1.1).</summary>
        private void ReturnControl()
        {
            MaybeSpawnAmplifier();
            foreach (var h in S.LivingList(Team.Hero))
            {
                if (h.RegenPerTurn > 0 && h.Hp < h.MaxHp) Heal(h, h.RegenPerTurn, h);
                TickTurnBuffs(h, false);
                var keys = new List<string>(h.Cooldowns.Keys);
                foreach (var k in keys)
                    if (h.Cooldowns[k] > 0) h.Cooldowns[k]--;
            }
            // Timeline-halting debuffs on enemies (Frozen) count down on the shared turn cycle,
            // since a halted icon never reaches its own turn boundary.
            foreach (var e in S.LivingList(Team.Enemy)) TickTurnBuffs(e, true);
            Emit(new PlayerControlEvent());
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Battle end / waves
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Wave/victory/defeat bookkeeping; call after anything that can kill. Returns true if the battle ended.</summary>
        private bool CheckEnd()
        {
            if (S.IsOver) return true;
            bool heroesAlive = false, enemiesAlive = false;
            foreach (var u in S.Units)
            {
                if (!u.Alive) continue;
                if (u.Team == Team.Hero) heroesAlive = true; else enemiesAlive = true;
            }
            if (!heroesAlive)
            {
                S.Phase = BattlePhase.Defeat;
                Emit(new BattleEndedEvent { Victory = false });
                return true;
            }
            if (!enemiesAlive)
            {
                Emit(new WaveClearedEvent { Wave = S.Wave });
                if (S.Wave + 1 < S.Stage.Waves.Count)
                {
                    S.Wave++;
                    SpawnWave(S.Wave);
                    return false;
                }
                S.Phase = BattlePhase.Victory;
                foreach (var h in S.Units)
                {
                    if (!h.IsHero) continue;
                    S.Results.WeaponDurability[h.Def.Id] = h.WeaponId != null ? h.WeaponDurability : -1;
                    S.Results.ArmorDurability[h.Def.Id] = h.ArmorId != null ? h.ArmorDurability : -1;
                    S.Results.FinalWeapon[h.Def.Id] = h.WeaponId;
                    S.Results.FinalBar[h.Def.Id] = h.Bar.ConvertAll(sl => sl.WeaponId != null ? "weapon:" + sl.WeaponId : null);
                }
                Emit(new BattleEndedEvent { Victory = true });
                return true;
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Mana
        // ─────────────────────────────────────────────────────────────────────────────

        private void MintOrb(int sourceId, string reason)
        {
            bool landed = S.Bank.Add(1) == 1;
            Emit(new OrbMintedEvent { SourceUnitId = sourceId, Landed = landed, Reason = reason });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Damage / heal / death (the single choke-points)
        // ─────────────────────────────────────────────────────────────────────────────

        private bool IsArmored(Unit u) => BattleEngine.IsArmored(S, u);

        /// <summary>
        /// Applies damage, handles Sleep-break, threat, pushback (hero→enemy in Zone), cast stagger and death.
        /// <paramref name="source"/> null = environmental (DOT, trap): no pushback, no stagger.
        /// </summary>
        private void Damage(Unit target, int amount, Unit source, HitKind hit, DamageType type, string note = null, bool canPush = true)
        {
            if (!target.Alive) return;
            if (hit != HitKind.Miss && amount > 0 && IsArmored(target)) { amount = 0; note = "Armored!"; }
            // Tough hide kit (§14.4): physical blows land at 70%.
            if (hit != HitKind.Miss && amount > 0 && type == DamageType.Physical && !target.IsHero && HasPassive(target, "tough_hide")) amount = Math.Max(1, amount * 7 / 10);
            if (hit != HitKind.Miss && amount > 0)
                RemoveBuffsWhere(target, b => b.Def.BreaksOnDamage, "woke");
            target.Hp = Math.Max(0, target.Hp - amount);
            Emit(new DamagedEvent { TargetId = target.Id, SourceId = source?.Id ?? -1, Amount = amount, Hit = hit, Type = type, HpAfter = target.Hp, Note = note });
            if (source != null && source.IsHero && target.Team == Team.Enemy) source.DamageDealt += amount;
            if (source != null && hit != HitKind.Miss && amount > 0 && target.IsHero && source.Team == Team.Enemy) WearArmor(target);

            if (target.Hp <= 0) { Kill(target, source); ResolveDeathBursts(); return; }
            if (source == null || hit == HitKind.Miss) return;

            if (canPush && source.IsHero && target.Team == Team.Enemy) Pushback(target, source);
            StaggerCasts(target, source);
        }

        /// <summary>Enemies whose death burst is still to go off (resolved after the blow that killed them).</summary>
        private readonly List<Unit> DeathBurstPending = new List<Unit>();
        private bool bursting;

        /// <summary>Self-destruct kit (§14.4): damage every hero adjacent to the fallen enemy.</summary>
        private void ResolveDeathBursts()
        {
            if (bursting) return;
            bursting = true;
            while (DeathBurstPending.Count > 0 && !S.IsOver)
            {
                var u = DeathBurstPending[0];
                DeathBurstPending.RemoveAt(0);
                Announce($"{u.Name} self-destructs!", u.Id);
                var victims = new List<Unit>();
                foreach (var h in S.Living(Team.Hero)) if (u.IsAdjacentTo(h)) victims.Add(h);
                Emit(new AbilityResolvedEvent { CasterId = u.Id, AbilityId = "death_burst", Anchor = u.Pos });
                foreach (var h in victims) if (h.Alive) Damage(h, u.Def.DeathBurst, null, HitKind.Normal, DamageType.Fire, "Blast", canPush: false);
            }
            bursting = false;
        }

        private void Heal(Unit target, int amount, Unit source)
        {
            if (!target.Alive || amount <= 0) return;
            int before = target.Hp;
            target.Hp = Math.Min(target.MaxHp, target.Hp + amount);
            Emit(new HealedEvent { TargetId = target.Id, SourceId = source?.Id ?? -1, Amount = target.Hp - before, HpAfter = target.Hp });
        }

        private void Kill(Unit u, Unit killer)
        {
            var at = u.Pos;
            if (!u.IsHero && u.Def.DeathBurst > 0) DeathBurstPending.Add(u);
            u.Hp = 0;
            u.OnBoard = false;
            u.Buffs.Clear();
            S.Timeline.RemoveAll(e => e.OwnerId == u.Id);
            Emit(new DiedEvent { UnitId = u.Id, Team = u.Team, At = at });
            if (u.IsRemains) { Announce("Laid to rest", u.Id); return; }
            if (u.Team == Team.Enemy && !u.NoLoot) AwardLoot(u);
            if (u.Team == Team.Enemy) LeaveRemains(u, at);
        }

        /// <summary>Undead leave a solid grave marker on their tile (§14.5); destroy it or they rise again.</summary>
        private void LeaveRemains(Unit u, Coord at)
        {
            var def = C.TryClass(u.Def.Remains);
            if (def == null || u.IsMultiTile) return;
            var r = SpawnEnemy(def, u.Level, at.X, at.Y);
            if (r == null || r.Pos != at) { if (r != null) { r.OnBoard = false; r.Hp = 0; S.Timeline.RemoveAll(e => e.OwnerId == r.Id); } return; }
            r.RisesAs = u.Def.Id;
            r.RisesLevel = u.Level;
            Emit(new RemainsLeftEvent { RemainsId = r.Id, FromUnitId = u.Id, At = at });
            Announce($"{u.Name} leaves {def.Name.ToLowerInvariant()} — destroy it!", r.Id);
        }

        private void AwardLoot(Unit enemy)
        {
            var loot = new LootEvent { FromUnitId = enemy.Id, Xp = Formulas.XpReward(enemy) };
            C.DropTables.TryGetValue(enemy.Def.DropTable ?? "", out var table);
            if (table != null && table.CoinsMax > 0) loot.Coins = S.Rng.Range(table.CoinsMin, table.CoinsMax + 1);
            else if (!enemy.IsSnakeSegment) loot.Coins = CoinsFor(loot.Xp);
            if (table != null)
            {
                foreach (var entry in table.Entries)
                    if (S.Rng.Chance(entry.Chance))
                        loot.Items.Add(new ItemCount { Item = entry.Item, Count = S.Rng.Range(entry.Min, entry.Max + 1) });
            }
            if (S.NewGamePlus > 0) loot.Coins = (int)Math.Round(loot.Coins * (1f + BattleConfig.NgPlusCoinBonus * S.NewGamePlus));
            S.Results.Coins += loot.Coins;
            S.Results.Xp += loot.Xp;
            foreach (var ic in loot.Items) Add(S.Results.Items, ic.Item, ic.Count);
            if (!enemy.IsSnakeSegment) Add(S.Results.Kills, enemy.Def.Id, 1);
            Emit(loot);
        }

        /// <summary>Default coin drop when a table doesn't author one: about half the XP value, ±20%.</summary>
        private int CoinsFor(int xp)
        {
            float baseCoins = Math.Max(1f, xp * 0.5f);
            return Math.Max(1, (int)Math.Round(baseCoins * S.Rng.Range(0.8f, 1.2f)));
        }

        private static void Add(Dictionary<string, int> map, string key, int n)
        {
            map.TryGetValue(key, out var v);
            map[key] = v + n;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Buffs (§8)
        // ─────────────────────────────────────────────────────────────────────────────

        private void ApplyBuff(Unit u, BuffDef def, int duration)
        {
            if (!u.Alive || def == null) return;
            duration = Math.Max(1, duration);
            if (u.Def.ImmuneBuffs != null && u.Def.ImmuneBuffs.Contains(def.Id)) { Announce("Immune!", u.Id); return; }
            var existing = u.Buffs.Find(b => b.Def.Id == def.Id);
            if (existing != null)
            {
                switch (def.Stacking)
                {
                    case BuffStacking.Ignore: return;
                    case BuffStacking.Upgrade:
                        if (existing.Remaining >= duration) return;
                        u.Buffs.Remove(existing);
                        break;
                    case BuffStacking.Stack:
                        // Keep every stack (ticks and effects apply per stack) up to MaxStacks; at the cap the
                        // oldest stack is replaced, so re-applying still refreshes the timer.
                        var stacks = u.Buffs.FindAll(b => b.Def.Id == def.Id);
                        if (def.MaxStacks > 0 && stacks.Count >= def.MaxStacks) u.Buffs.Remove(stacks[0]);
                        break;
                    default: u.Buffs.RemoveAll(b => b.Def.Id == def.Id); break; // Refresh (§8.6 default)
                }
            }
            u.Buffs.Add(new BuffInstance { Def = def, Remaining = duration, Fresh = true });
            Emit(new BuffAppliedEvent { UnitId = u.Id, BuffId = def.Id, Duration = duration });
        }

        private void RemoveBuffsWhere(Unit u, Predicate<BuffInstance> pred, string reason)
        {
            for (int i = u.Buffs.Count - 1; i >= 0; i--)
            {
                if (!pred(u.Buffs[i])) continue;
                var id = u.Buffs[i].Def.Id;
                u.Buffs.RemoveAt(i);
                Emit(new BuffRemovedEvent { UnitId = u.Id, BuffId = id, Reason = reason });
            }
        }

        private void Expire(Unit u, BuffInstance b)
        {
            u.Buffs.Remove(b);
            Emit(new BuffRemovedEvent { UnitId = u.Id, BuffId = b.Def.Id, Reason = "expired" });
            var chain = C.TryBuff(b.Def.OnExpireApply);
            if (chain != null && u.Alive) ApplyBuff(u, chain, chain.Duration);
        }

        /// <summary>Removes a buff as if it expired (runs its expire chain) — e.g. Fire melting Frozen.</summary>
        private void ForceExpire(Unit u, string buffId)
        {
            var b = u.Buffs.Find(x => x.Def.Id == buffId);
            if (b != null) Expire(u, b);
        }

        /// <param name="haltingOnly">Only tick timeline-halting turn buffs (used for enemies at the shared turn boundary).</param>
        private void TickTurnBuffs(Unit u, bool haltingOnly)
        {
            for (int i = u.Buffs.Count - 1; i >= 0 && i < u.Buffs.Count; i--)
            {
                var b = u.Buffs[i];
                if (b.Def.Unit != DurationUnit.Turns) continue;
                if (haltingOnly != b.Def.HaltsTimeline) continue;
                if (b.Def.HaltsTimeline && b.Fresh) { b.Fresh = false; continue; }
                b.Remaining--;
                if (b.Remaining <= 0) Expire(u, b);
                else Emit(new BuffTickedEvent { UnitId = u.Id, BuffId = b.Def.Id, Remaining = b.Remaining });
            }
        }

        /// <summary>Tick-unit buffs: one step (and one damage tick) per BuffTickInterval of timeline time (§8.3).</summary>
        private void TickTimedBuffs(int ticks)
        {
            foreach (var u in S.Units.ToArray())
            {
                if (!u.Alive) continue;
                for (int i = u.Buffs.Count - 1; i >= 0 && i < u.Buffs.Count; i--)
                {
                    var b = u.Buffs[i];
                    if (b.Def.Unit != DurationUnit.Ticks) continue;
                    b.TickAccumulator += ticks;
                    while (b.TickAccumulator >= BattleConfig.BuffTickInterval && u.Alive && u.Buffs.Contains(b))
                    {
                        b.TickAccumulator -= BattleConfig.BuffTickInterval;
                        if (b.Def.DamagePerTick > 0)
                            Damage(u, b.Def.DamagePerTick, null, HitKind.Normal, DamageType.Physical, b.Def.Name);
                        if (b.Def.HealPerTick > 0 && u.Alive && u.Hp < u.MaxHp) Heal(u, b.Def.HealPerTick, null);
                        if (!u.Alive) break;
                        b.Remaining--;
                        if (b.Remaining <= 0) { Expire(u, b); break; }
                        Emit(new BuffTickedEvent { UnitId = u.Id, BuffId = b.Def.Id, Remaining = b.Remaining });
                    }
                    if (!u.Alive) break;
                }
                if (!u.Alive && CheckEnd()) return;
            }
        }
    }
}
