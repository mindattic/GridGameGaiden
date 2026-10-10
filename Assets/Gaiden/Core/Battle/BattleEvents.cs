using System.Collections.Generic;
using Gaiden.Core.Content;

namespace Gaiden.Core.Battle
{
    /// <summary>
    /// Everything that happens in a battle is reported as an ordered stream of these. The
    /// simulation has already finished by the time the presentation sees them; the view
    /// only plays them back (one awaited animation each).
    /// </summary>
    public abstract class BattleEvent
    {
        public override string ToString() => GetType().Name;
    }

    public enum HitKind { Normal, Critical, Miss }

    public sealed class ActionBeganEvent : BattleEvent
    {
        public int ActorId; public string Label;
        public override string ToString() => $"ActionBegan {ActorId} '{Label}'";
    }

    public sealed class SteppedEvent : BattleEvent
    {
        public int UnitId; public Coord From; public Coord To;
        public override string ToString() => $"Stepped {UnitId} {From}->{To}";
    }

    public sealed class DisplacedEvent : BattleEvent
    {
        public int UnitId; public Coord From; public Coord To; public int ByUnitId;
        public override string ToString() => $"Displaced {UnitId} {From}->{To} by {ByUnitId}";
    }

    public sealed class StepBlockedEvent : BattleEvent
    {
        public int UnitId; public Coord At; public string Reason;
        public override string ToString() => $"StepBlocked {UnitId} at {At} ({Reason})";
    }

    public sealed class TeleportedEvent : BattleEvent
    {
        public int UnitId; public Coord From; public Coord To;
        public override string ToString() => $"Teleported {UnitId} {From}->{To}";
    }

    public sealed class PincerFormedEvent : BattleEvent
    {
        public Team Team; public int Attacker1; public int Attacker2;
        public List<int> Opponents = new List<int>();
        public List<int> Supporters = new List<int>();
        public int ChainIndex;
        public override string ToString() => $"Pincer {Team} {Attacker1}+{Attacker2} vs [{string.Join(",", Opponents)}] sup[{string.Join(",", Supporters)}] #{ChainIndex}";
    }

    /// <summary>A chained pincer whose whole line already fell: the attackers spin in triumph (cosmetic, §13.1.1).</summary>
    public sealed class RespiteEvent : BattleEvent
    {
        public int Attacker1; public int Attacker2;
        public override string ToString() => $"Respite {Attacker1}+{Attacker2}";
    }

    public sealed class AttackEvent : BattleEvent
    {
        public int AttackerId; public int TargetId; public bool IsSupport; public bool IsMelee; public bool IsRanged;
        public override string ToString() => $"Attack {AttackerId}->{TargetId}{(IsSupport ? " (support)" : "")}{(IsMelee ? " (melee)" : "")}";
    }

    public sealed class DamagedEvent : BattleEvent
    {
        public int TargetId; public int SourceId; public int Amount; public HitKind Hit; public DamageType Type;
        public int HpAfter; public string Note;
        public override string ToString() => $"Damaged {TargetId} -{Amount} {Hit} {Type} hp={HpAfter}{(Note != null ? " " + Note : "")}";
    }

    public sealed class HealedEvent : BattleEvent
    {
        public int TargetId; public int SourceId; public int Amount; public int HpAfter;
        public override string ToString() => $"Healed {TargetId} +{Amount} hp={HpAfter}";
    }

    public sealed class DiedEvent : BattleEvent
    {
        public int UnitId; public Team Team; public Coord At;
        public override string ToString() => $"Died {UnitId}";
    }

    public sealed class LootEvent : BattleEvent
    {
        public int FromUnitId; public int Coins; public List<ItemCount> Items = new List<ItemCount>(); public int Xp;
        public override string ToString() => $"Loot from {FromUnitId}: {Coins}c {Xp}xp items={Items.Count}";
    }

    public sealed class OrbMintedEvent : BattleEvent
    {
        public int SourceUnitId; public bool Landed; public string Reason;
        public override string ToString() => $"Orb from {SourceUnitId} {(Landed ? "landed" : "overflow")} ({Reason})";
    }

    public sealed class OrbsSpentEvent : BattleEvent
    {
        public int Count;
        public override string ToString() => $"OrbsSpent {Count}";
    }

    public sealed class TimelineAdvancedEvent : BattleEvent
    {
        public int Ticks;
        /// <summary>Snapshot of (entry id → u) after the advance segment, for smooth playback.</summary>
        public Dictionary<int, float> U = new Dictionary<int, float>();
        public override string ToString() => $"TimelineAdvanced {Ticks}t";
    }

    public sealed class PushedBackEvent : BattleEvent
    {
        public int UnitId; public float FromU; public float ToU; public int StunTicks;
        public override string ToString() => $"PushedBack {UnitId} {FromU:F2}->{ToU:F2} stun={StunTicks}";
    }

    public sealed class HastenedEvent : BattleEvent
    {
        public int EntryId; public int UnitId; public float FromU; public float ToU;
        public override string ToString() => $"Hastened {UnitId} {FromU:F2}->{ToU:F2}";
    }

    public sealed class TurnStartedEvent : BattleEvent
    {
        public int UnitId;
        public override string ToString() => $"TurnStarted {UnitId}";
    }

    public sealed class TurnSkippedEvent : BattleEvent
    {
        public int UnitId; public string Reason;
        public override string ToString() => $"TurnSkipped {UnitId} ({Reason})";
    }

    public sealed class PlayerControlEvent : BattleEvent
    {
        public override string ToString() => "PlayerControl";
    }

    public sealed class CastQueuedEvent : BattleEvent
    {
        public int EntryId; public int CasterId; public string AbilityId; public int Ticks; public bool IsCharge;
        public List<Coord> LineTiles;
        public override string ToString() => $"CastQueued {CasterId} {AbilityId} {Ticks}t{(IsCharge ? " charge" : "")}";
    }

    public sealed class CastStaggeredEvent : BattleEvent
    {
        public int EntryId; public int CasterId; public int DelayTicks; public bool Resisted;
        public override string ToString() => $"CastStaggered {CasterId} +{DelayTicks}t{(Resisted ? " resisted" : "")}";
    }

    public sealed class CastCancelledEvent : BattleEvent
    {
        public int EntryId; public int CasterId; public string AbilityId; public string Reason;
        public override string ToString() => $"CastCancelled {CasterId} {AbilityId} ({Reason})";
    }

    public sealed class ClutchEvent : BattleEvent
    {
        public int EntryId; public int CasterId;
        public override string ToString() => $"Clutch {CasterId}";
    }

    public sealed class AbilityResolvedEvent : BattleEvent
    {
        public int CasterId; public string AbilityId; public Coord Anchor; public List<int> Targets = new List<int>();
        public List<Coord> Tiles = new List<Coord>();
        public override string ToString() => $"AbilityResolved {CasterId} {AbilityId} @{Anchor} -> [{string.Join(",", Targets)}]";
    }

    public sealed class AbilityRefusedEvent : BattleEvent
    {
        public int CasterId; public string Reason;
        public override string ToString() => $"AbilityRefused {CasterId} ({Reason})";
    }

    public sealed class ItemUsedEvent : BattleEvent
    {
        public int UserId; public string ItemId; public int ChargesLeft;
        public override string ToString() => $"ItemUsed {UserId} {ItemId} left={ChargesLeft}";
    }

    public sealed class BuffAppliedEvent : BattleEvent
    {
        public int UnitId; public string BuffId; public int Duration;
        public override string ToString() => $"BuffApplied {UnitId} {BuffId} {Duration}";
    }

    public sealed class BuffTickedEvent : BattleEvent
    {
        public int UnitId; public string BuffId; public int Remaining;
        public override string ToString() => $"BuffTicked {UnitId} {BuffId} {Remaining}";
    }

    public sealed class BuffRemovedEvent : BattleEvent
    {
        public int UnitId; public string BuffId; public string Reason;
        public override string ToString() => $"BuffRemoved {UnitId} {BuffId} ({Reason})";
    }

    public sealed class AnnounceEvent : BattleEvent
    {
        public string Text; public int UnitId = -1;
        public override string ToString() => $"Announce '{Text}'";
    }

    public sealed class ScannedEvent : BattleEvent
    {
        public int UnitId; public Stats Stats; public int Hp; public int MaxHp;
        public override string ToString() => $"Scanned {UnitId}";
    }

    public sealed class TrapPlacedEvent : BattleEvent
    {
        public int ById; public Coord At; public string TrapId;
        public override string ToString() => $"TrapPlaced {TrapId} at {At}";
    }

    public sealed class TrapTriggeredEvent : BattleEvent
    {
        public int UnitId; public Coord At; public string TrapId;
        public override string ToString() => $"TrapTriggered {TrapId} on {UnitId} at {At}";
    }

    public sealed class WeaponWornEvent : BattleEvent
    {
        public int UnitId; public string WeaponId; public int Durability;
        public override string ToString() => $"WeaponWorn {UnitId} {WeaponId} {Durability}";
    }

    public sealed class ArmorBrokenEvent : BattleEvent
    {
        public int UnitId; public string ArmorId;
        public override string ToString() => $"ArmorBroken {UnitId} {ArmorId}";
    }

    public sealed class WeaponSwappedEvent : BattleEvent
    {
        public int UnitId; public string FromWeapon; public string ToWeapon;
        public override string ToString() => $"WeaponSwapped {UnitId} {FromWeapon}->{ToWeapon}";
    }

    public sealed class WeaponShatteredEvent : BattleEvent
    {
        public int UnitId; public string WeaponId; public int SelfDamage;
        public override string ToString() => $"WeaponShattered {UnitId} {WeaponId}";
    }

    public sealed class BossPhaseEvent : BattleEvent
    {
        public int UnitId; public int Phase; public string Text;
        public override string ToString() => $"BossPhase {UnitId} -> {Phase}";
    }

    public sealed class WaveStartedEvent : BattleEvent
    {
        public int Wave; public int TotalWaves; public List<int> Spawned = new List<int>();
        public override string ToString() => $"WaveStarted {Wave + 1}/{TotalWaves} [{string.Join(",", Spawned)}]";
    }

    public sealed class WaveClearedEvent : BattleEvent
    {
        public int Wave;
        public override string ToString() => $"WaveCleared {Wave + 1}";
    }

    public sealed class BattleEndedEvent : BattleEvent
    {
        public bool Victory;
        public bool Escaped;
        public override string ToString() => Victory ? "Victory" : Escaped ? "Escaped" : "Defeat";
    }

    public sealed class RevivedEvent : BattleEvent
    {
        public int UnitId; public Coord At; public int Hp;
        public override string ToString() => $"Revived {UnitId} at {At} hp={Hp}";
    }

    public sealed class AmplifierSpawnedEvent : BattleEvent
    {
        public Coord At;
        public override string ToString() => $"Amplifier appears at {At}";
    }

    public sealed class AmplifierMovedEvent : BattleEvent
    {
        public Coord From, To; public int ByUnitId;
        public override string ToString() => $"Amplifier shoved {From}->{To} by {ByUnitId}";
    }

    public sealed class AmplifierUsedEvent : BattleEvent
    {
        public Coord At; public int HeroId;
        public override string ToString() => $"Amplifier used by {HeroId} at {At}";
    }

    public sealed class RemainsLeftEvent : BattleEvent
    {
        public int RemainsId; public int FromUnitId; public Coord At;
        public override string ToString() => $"Remains {RemainsId} left by {FromUnitId} at {At}";
    }

    public sealed class RoseAgainEvent : BattleEvent
    {
        public int RemainsId; public int UnitId; public Coord At;
        public override string ToString() => $"Unit {UnitId} rose from remains {RemainsId} at {At}";
    }
}
