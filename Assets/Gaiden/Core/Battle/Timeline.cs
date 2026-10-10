using System;
using System.Collections.Generic;
using Gaiden.Core.Content;

namespace Gaiden.Core.Battle
{
    public enum TimelineKind { Turn, Cast }

    /// <summary>
    /// One icon on the shared timeline. Turn icons (above the line) load from IP 0 to BarLength;
    /// cast icons (below the line) count their remaining ticks down to 0. Both resolve on the
    /// same clock — whichever arrives first resolves first (bible §2.4 / §2.6).
    /// </summary>
    public sealed class TimelineEntry
    {
        public int Id;
        public TimelineKind Kind;
        public int OwnerId;

        // Turn icons
        public int Ip;
        public int StunTicks;

        // Cast icons
        public string AbilityId;
        public Team CasterTeam;
        public int TargetUnitId = -1;
        public Coord TargetTile;
        public int TotalCastTicks;
        public int RemainingTicks;
        public int AccumulatedDelayTicks;
        public bool IsCharge;
        public ChargeShape ChargeShape;
        public List<Coord> LineTiles;

        /// <summary>Normalized position on the shared u-axis (0 = spawn/left, 1 = trigger/right).</summary>
        public float U => Kind == TimelineKind.Turn
            ? Math.Max(0f, Math.Min(1f, Ip / (float)BattleConfig.BarLength))
            : Math.Max(0f, 1f - RemainingTicks / (float)Math.Max(1, TotalCastTicks + AccumulatedDelayTicks)); // 0 at cast start, 1 = lands

        public bool InZone => Kind == TimelineKind.Turn && Ip >= BattleConfig.ZoneStart;

        public TimelineEntry Clone()
        {
            var e = (TimelineEntry)MemberwiseClone();
            if (LineTiles != null) e.LineTiles = new List<Coord>(LineTiles);
            return e;
        }
    }
}
