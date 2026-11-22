using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Monoliths;

/// <summary>Picks the first timeline the editor selects.</summary>
public static class MonolithTimelineSelection
{
    /// <summary>The first saved run's timeline that the catalog offers, else the first option.</summary>
    public static int Initial(IReadOnlyList<MonolithRunSnapshot> runs)
    {
        for (int i = 0; i < runs.Count; i++)
        {
            if (MonolithTimelineCatalog.TryGet(runs[i].TimelineId, out MonolithTimelineOption _))
            {
                return runs[i].TimelineId;
            }
        }

        return MonolithTimelineCatalog.All[0].TimelineId;
    }
}
