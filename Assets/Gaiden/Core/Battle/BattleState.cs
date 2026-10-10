using System.Collections.Generic;
using Gaiden.Core.Content;

namespace Gaiden.Core.Battle
{
    public enum BattlePhase { PlayerInput, Victory, Defeat, Escaped }

    public sealed class Trap
    {
        public Coord At;
        public string TrapId;
        public int OwnerId;
        public Trap Clone() => (Trap)MemberwiseClone();
    }

    /// <summary>Everything a battle needs to know about one party member, supplied by the meta layer.</summary>
    public sealed class HeroSpec
    {
        public string ClassId;
        public int Level = 1;
        /// <summary>Current HP carried between battles; 0 or less = full.</summary>
        public int Hp;
        public string WeaponId;
        public int WeaponDurability;
        public string ArmorId;
        public int ArmorDurability;
        public List<string> EquippedItemIds = new List<string>();
        /// <summary>Ability bar entries: ability id, or "item:id". Null/empty = empty slot.</summary>
        public List<string> Bar = new List<string>();
        /// <summary>Item id → owned count, used to size bar item stacks.</summary>
        public Dictionary<string, int> ItemCounts = new Dictionary<string, int>();
        public Coord? Start;
    }

    public sealed class BattleSetup
    {
        public StageDef Stage;
        public List<HeroSpec> Party = new List<HeroSpec>();
        public int Seed = 1;
        public int UnlockedSlots = 2;
        /// <summary>Enemy levels are floored to this (campaign difficulty curve, §22.3).</summary>
        public int RecommendedLevel = 1;
        /// <summary>New Game+ cycle (0 = first playthrough): enemies +10 levels and coins +50% per cycle.</summary>
        public int NewGamePlus;
    }

    /// <summary>Totals the meta layer commits after the battle (XP, coins, drops, kills, bestiary).</summary>
    public sealed class BattleResults
    {
        public int Coins;
        public int Xp;
        public Dictionary<string, int> Items = new Dictionary<string, int>();
        public Dictionary<string, int> Kills = new Dictionary<string, int>();
        public HashSet<string> Seen = new HashSet<string>();
        public HashSet<string> Scanned = new HashSet<string>();
        /// <summary>Item uses that came from inventory stacks (deducted on commit).</summary>
        public Dictionary<string, int> ItemsConsumed = new Dictionary<string, int>();
        /// <summary>Hero class → weapon durability after battle (-1 = shattered/removed).</summary>
        public Dictionary<string, int> WeaponDurability = new Dictionary<string, int>();
        /// <summary>Hero class → armor durability after battle (-1 = broke).</summary>
        public Dictionary<string, int> ArmorDurability = new Dictionary<string, int>();
        /// <summary>Hero class → weapon equipped at the end (after bar swaps), and what the bar slot then held.</summary>
        public Dictionary<string, string> FinalWeapon = new Dictionary<string, string>();
        public Dictionary<string, List<string>> FinalBar = new Dictionary<string, List<string>>();
        public int ActionsTaken;
        public int TicksElapsed;

        public BattleResults Clone()
        {
            return new BattleResults
            {
                Coins = Coins, Xp = Xp, ActionsTaken = ActionsTaken, TicksElapsed = TicksElapsed,
                Items = new Dictionary<string, int>(Items),
                Kills = new Dictionary<string, int>(Kills),
                Seen = new HashSet<string>(Seen),
                Scanned = new HashSet<string>(Scanned),
                ItemsConsumed = new Dictionary<string, int>(ItemsConsumed),
                WeaponDurability = new Dictionary<string, int>(WeaponDurability),
                ArmorDurability = new Dictionary<string, int>(ArmorDurability),
                FinalWeapon = new Dictionary<string, string>(FinalWeapon),
                FinalBar = new Dictionary<string, List<string>>(FinalBar),
            };
        }
    }

    public sealed class BattleState
    {
        public ContentDb Content;
        public StageDef Stage;
        public int RecommendedLevel = 1;
        public int NewGamePlus;
        public Rng Rng;
        public List<Unit> Units = new List<Unit>();
        public List<TimelineEntry> Timeline = new List<TimelineEntry>();
        public List<Trap> Traps = new List<Trap>();
        public ManaBank Bank = new ManaBank();
        public int Wave;
        public BattlePhase Phase = BattlePhase.PlayerInput;
        public int NextUnitId = 1;
        public int NextEntryId = 1;
        public long Clock;
        /// <summary>Seconds of drag timer left at the last hero drop (time-banked orbs, §3.1.8).</summary>
        public float PendingTimeBankSeconds;
        public BattleResults Results = new BattleResults();

        public bool IsOver => Phase != BattlePhase.PlayerInput;

        public Unit UnitById(int id)
        {
            foreach (var u in Units) if (u.Id == id) return u;
            return null;
        }

        public Unit UnitAt(Coord c)
        {
            foreach (var u in Units) if (u.Alive && u.Occupies(c)) return u;
            return null;
        }

        public static bool InBounds(Coord c) => c.X >= 0 && c.X < BattleConfig.Cols && c.Y >= 0 && c.Y < BattleConfig.Rows;

        public IEnumerable<Unit> Living(Team team)
        {
            foreach (var u in Units) if (u.Alive && u.Team == team) yield return u;
        }

        public List<Unit> LivingList(Team team)
        {
            var l = new List<Unit>();
            foreach (var u in Units) if (u.Alive && u.Team == team) l.Add(u);
            return l;
        }

        public TimelineEntry TurnEntryOf(int unitId)
        {
            foreach (var e in Timeline) if (e.Kind == TimelineKind.Turn && e.OwnerId == unitId) return e;
            return null;
        }

        public TimelineEntry CastEntryOf(int unitId)
        {
            foreach (var e in Timeline) if (e.Kind == TimelineKind.Cast && e.OwnerId == unitId) return e;
            return null;
        }

        public int HeroCastsInFlight
        {
            get
            {
                int n = 0;
                foreach (var e in Timeline) if (e.Kind == TimelineKind.Cast && e.CasterTeam == Team.Hero) n++;
                return n;
            }
        }

        public Trap TrapAt(Coord c)
        {
            foreach (var t in Traps) if (t.At == c) return t;
            return null;
        }

        public BattleState Clone()
        {
            var s = (BattleState)MemberwiseClone();
            s.Rng = Rng.Clone();
            s.Units = new List<Unit>(Units.Count);
            foreach (var u in Units) s.Units.Add(u.Clone());
            s.Timeline = new List<TimelineEntry>(Timeline.Count);
            foreach (var e in Timeline) s.Timeline.Add(e.Clone());
            s.Traps = new List<Trap>(Traps.Count);
            foreach (var t in Traps) s.Traps.Add(t.Clone());
            s.Bank = Bank.Clone();
            s.Results = Results.Clone();
            return s;
        }
    }
}
