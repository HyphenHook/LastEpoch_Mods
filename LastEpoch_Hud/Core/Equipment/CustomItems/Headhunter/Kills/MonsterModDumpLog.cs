using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;

/// <summary>Builds the one-line summary logged after a monster mod dump.</summary>
public static class MonsterModDumpLog
{
    public static string Line(IReadOnlyList<MonsterModRow> rows, string path)
    {
        int prefixes = 0;
        int suffixes = 0;
        int withStats = 0;
        foreach (MonsterModRow row in rows)
        {
            prefixes += row.List == MonsterModRow.PrefixList ? 1 : 0;
            suffixes += row.List == MonsterModRow.SuffixList ? 1 : 0;
            withStats += row.Stats.Count > 0 ? 1 : 0;
        }

        return $"Monster mods dumped: {prefixes} prefixes, {suffixes} suffixes, {withStats} with stats -> {path}";
    }
}
