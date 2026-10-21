using System.Collections.Generic;

namespace GridGame.Core.Battle
{
    /// <summary>A player decision. Exactly one command is applied per committed action.</summary>
    public abstract class BattleCommand { }

    /// <summary>Drag a hero along a path of cardinal one-tile steps (the first entry is the next tile, not the start).</summary>
    public sealed class MoveCommand : BattleCommand
    {
        public int HeroId;
        public List<Coord> Path = new List<Coord>();
        /// <summary>Drag-timer seconds left at drop (0 when the timer is off).</summary>
        public float TimeLeftSeconds;
        public override string ToString() => $"Move {HeroId} [{string.Join(" ", Path)}]";
    }

    /// <summary>Use the ability/item in a bar slot. Target is the picked tile (actor or tile picks); ignored for Auto.</summary>
    public sealed class UseSlotCommand : BattleCommand
    {
        public int HeroId;
        public int Slot;
        public Coord Target;
        public override string ToString() => $"Use {HeroId} slot{Slot} @{Target}";
    }

    /// <summary>Raise shields: every hero gains Protection and the clock runs to the next enemy turn (§2.5).</summary>
    public sealed class ShieldCommand : BattleCommand
    {
        public override string ToString() => "Shield";
    }
}
