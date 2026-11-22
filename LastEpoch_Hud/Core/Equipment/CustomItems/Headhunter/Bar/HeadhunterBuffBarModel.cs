using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Decides which Headhunter buffs the bar shows.</summary>
public sealed class HeadhunterBuffBarModel
{
    private readonly List<HeadhunterBarEntry> _entries = new();

    /// <summary>Live stats in table order. The list is reused and valid until the next call.</summary>
    public IReadOnlyList<HeadhunterBarEntry> Build(
        IReadOnlyList<HeadhunterBuffStat> stats,
        IReadOnlyList<float> remaining,
        HeadhunterStackState stacks,
        float durationSeconds
    )
    {
        _entries.Clear();
        if (stats == null || remaining == null)
        {
            return _entries;
        }

        int count = Math.Min(stats.Count, remaining.Count);
        for (int i = 0; i < count; i++)
        {
            AddIfLive(stats[i], i, ShownStacks(stacks, i), remaining[i], durationSeconds);
        }

        return _entries;
    }

    private void AddIfLive(
        HeadhunterBuffStat stat,
        int row,
        int stacks,
        float remaining,
        float duration
    )
    {
        if (remaining <= 0f)
        {
            return;
        }

        _entries.Add(
            new HeadhunterBarEntry(
                stat.StatId,
                (int)Math.Ceiling(remaining),
                Elapsed(remaining, duration),
                row,
                stacks,
                stat.Tags
            )
        );
    }

    private static int ShownStacks(HeadhunterStackState stacks, int row)
    {
        return stacks == null ? 1 : Math.Max(1, stacks.Get(row));
    }

    private static float Elapsed(float remaining, float duration)
    {
        if (duration <= 0f)
        {
            return 0f;
        }

        return Math.Min(1f, Math.Max(0f, 1f - (remaining / duration)));
    }
}
