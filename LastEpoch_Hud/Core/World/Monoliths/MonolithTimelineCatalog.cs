using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Monoliths;

/// <summary>Timelines the editor offers, in id order.</summary>
public static class MonolithTimelineCatalog
{
    /// <summary>One option per timeline, ascending id.</summary>
    public static IReadOnlyList<MonolithTimelineOption> All { get; } =
        new MonolithTimelineOption[]
        {
            new(MonolithTimeline.UndeadAbom, "Fall Of The Outcast"),
            new(MonolithTimeline.OsprixWithLance, "The Stolen Lance"),
            new(MonolithTimeline.VoidRahyeh, "The Black Sun"),
            new(MonolithTimeline.FrostLich, "Blood Frost And Death"),
            new(MonolithTimeline.Lagon, "Ending The Storm"),
            new(MonolithTimeline.UndeadVsVoid, "Fall Of The Empire"),
            new(MonolithTimeline.Dragons, "Reign Of Dragon"),
            new(MonolithTimeline.Gaspar, "The Last Ruins"),
            new(MonolithTimeline.Heorot, "The Age Of Winter"),
            new(MonolithTimeline.Volcano, "Spirits Of Fire"),
        };

    /// <summary>Finds the option with the id; false for an unknown id.</summary>
    public static bool TryGet(int timelineId, out MonolithTimelineOption option)
    {
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i].TimelineId != timelineId)
            {
                continue;
            }

            option = All[i];
            return true;
        }

        option = default;
        return false;
    }
}
