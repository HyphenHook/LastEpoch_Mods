using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;

/// <summary>Turns affix map row texts into stat row indices. Runs once per config load.</summary>
internal static class HeadhunterAffixMapResolver
{
    public static Dictionary<int, int[]> Resolve(
        IReadOnlyList<HeadhunterAffixEntry> map,
        IReadOnlyList<HeadhunterStatEntry> entries,
        IReadOnlyDictionary<string, int> rowByText,
        ICollection<HeadhunterConfigProblem> problems
    )
    {
        var knownRows = new HashSet<string>(StringComparer.Ordinal);
        foreach (HeadhunterStatEntry entry in entries)
        {
            knownRows.Add(entry.RowText);
        }

        var result = new Dictionary<int, int[]>();
        for (int i = 0; i < map.Count; i++)
        {
            result[map[i].ModKey] = ResolveRows(map[i], i, knownRows, rowByText, problems);
        }
        return result;
    }

    private static int[] ResolveRows(
        HeadhunterAffixEntry entry,
        int index,
        HashSet<string> knownRows,
        IReadOnlyDictionary<string, int> rowByText,
        ICollection<HeadhunterConfigProblem> problems
    )
    {
        var rows = new List<int>();
        for (int j = 0; j < entry.Rows.Count; j++)
        {
            string text = entry.Rows[j];
            if (rowByText.TryGetValue(text, out int row))
            {
                rows.Add(row);
                continue;
            }
            if (knownRows.Contains(text))
            {
                continue;
            }
            string path = HeadhunterConfigKeys.AffixMap + "[" + index + "].rows[" + j + "]";
            string message = "Mod " + entry.ModKey + " names no stat row: " + text;
            problems.Add(
                new HeadhunterConfigProblem(HeadhunterConfigProblemCode.UnknownRow, path, message)
            );
        }
        return rows.ToArray();
    }
}
