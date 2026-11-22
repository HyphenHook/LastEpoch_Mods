using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Collects the stat rows whose buff is live.</summary>
public static class HeadhunterLiveRows
{
    public static void Fill(
        IReadOnlyList<HeadhunterBuffStat> stats,
        Func<string, bool> isLive,
        HashSet<int> rows
    )
    {
        rows.Clear();
        for (int i = 0; i < stats.Count; i++)
        {
            if (isLive(stats[i].BuffName))
            {
                rows.Add(i);
            }
        }
    }
}
