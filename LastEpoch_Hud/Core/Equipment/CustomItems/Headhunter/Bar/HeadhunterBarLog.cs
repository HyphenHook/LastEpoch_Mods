using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Debug line for a bar change.</summary>
public static class HeadhunterBarLog
{
    public static string Format(
        IReadOnlyList<HeadhunterBarEntry> entries,
        Func<int, string> statName,
        Func<int, string> tagName
    )
    {
        string[] names = new string[entries.Count];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = EntryName(entries[i], statName, tagName);
        }

        return $"Headhunter bar: count={entries.Count} [{string.Join(", ", names)}]";
    }

    private static string EntryName(
        HeadhunterBarEntry entry,
        Func<int, string> statName,
        Func<int, string> tagName
    )
    {
        string stat = statName(entry.StatId);
        return entry.Tags == 0 ? stat : stat + "[" + tagName(entry.Tags) + "]";
    }
}
