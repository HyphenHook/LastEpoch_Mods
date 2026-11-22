using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;

/// <summary>Turns a parsed config into the per-kill form. Runs once per config load.</summary>
public static class HeadhunterConfigResolver
{
    private const string BuffPrefix = "HH_";
    private const float PercentPerFraction = 100f;

    public static HeadhunterResolvedConfig Resolve(
        HeadhunterConfig config,
        IReadOnlyDictionary<string, int> statIds,
        IReadOnlyDictionary<string, int> tagIds,
        ICollection<HeadhunterConfigProblem> problems
    )
    {
        var rowByText = new Dictionary<string, int>(StringComparer.Ordinal);
        List<HeadhunterBuffStat> stats = ResolveStats(
            config.Stats,
            statIds,
            tagIds,
            rowByText,
            problems
        );
        return new HeadhunterResolvedConfig(
            config.DurationSeconds,
            config.MaxStacks,
            config.Triggers,
            stats,
            HeadhunterAffixMapResolver.Resolve(config.AffixMap, config.Stats, rowByText, problems)
        );
    }

    private static List<HeadhunterBuffStat> ResolveStats(
        IReadOnlyList<HeadhunterStatEntry> entries,
        IReadOnlyDictionary<string, int> statIds,
        IReadOnlyDictionary<string, int> tagIds,
        Dictionary<string, int> rowByText,
        ICollection<HeadhunterConfigProblem> problems
    )
    {
        var result = new List<HeadhunterBuffStat>();
        var seen = new HashSet<HeadhunterStatKey>();
        for (int i = 0; i < entries.Count; i++)
        {
            HeadhunterStatEntry entry = entries[i];
            if (!entry.Enabled)
            {
                continue;
            }

            if (!statIds.TryGetValue(entry.Stat, out int statId))
            {
                AddProblem(
                    problems,
                    HeadhunterConfigProblemCode.UnknownStat,
                    i,
                    HeadhunterConfigKeys.Stat,
                    "Unknown stat: " + entry.Stat
                );
                continue;
            }

            if (!TryResolveTag(entry, tagIds, out int tags))
            {
                AddProblem(
                    problems,
                    HeadhunterConfigProblemCode.UnknownTag,
                    i,
                    HeadhunterConfigKeys.Tag,
                    "Unknown tag: " + entry.Tag
                );
                continue;
            }

            if (!seen.Add(new HeadhunterStatKey(statId, tags)))
            {
                AddProblem(
                    problems,
                    HeadhunterConfigProblemCode.DuplicateStatId,
                    i,
                    HeadhunterConfigKeys.Stat,
                    "Stat and tag share a game id with an earlier row: " + entry.Stat
                );
                continue;
            }

            rowByText.TryAdd(entry.RowText, result.Count);
            result.Add(ToBuffStat(entry, statId, tags));
        }

        return result;
    }

    private static bool TryResolveTag(
        HeadhunterStatEntry entry,
        IReadOnlyDictionary<string, int> tagIds,
        out int tags
    )
    {
        tags = 0;
        return entry.Tag == null || tagIds.TryGetValue(entry.Tag, out tags);
    }

    private static void AddProblem(
        ICollection<HeadhunterConfigProblem> problems,
        HeadhunterConfigProblemCode code,
        int index,
        string field,
        string message
    )
    {
        string path = HeadhunterConfigKeys.Stats + "[" + index + "]." + field;
        problems.Add(new HeadhunterConfigProblem(code, path, message));
    }

    private static HeadhunterBuffStat ToBuffStat(HeadhunterStatEntry entry, int statId, int tags)
    {
        return new HeadhunterBuffStat(
            statId,
            BuffName(entry, tags),
            entry.Added,
            entry.Increased / PercentPerFraction,
            tags
        );
    }

    private static string BuffName(HeadhunterStatEntry entry, int tags)
    {
        return tags == 0 ? BuffPrefix + entry.Stat : BuffPrefix + entry.Stat + "_" + entry.Tag;
    }
}
