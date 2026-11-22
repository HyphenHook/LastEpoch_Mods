namespace LastEpoch_Hud.Scripts.Core.Monoliths;

/// <summary>One editor timeline button.</summary>
public readonly record struct MonolithTimelineOption(MonolithTimeline Timeline, string Label)
{
    /// <summary>The game's timeline id.</summary>
    public int TimelineId => (int)Timeline;
}
