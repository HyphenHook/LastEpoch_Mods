using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>Turns a parsed config into game-ready flasks: names to ids, percents to fractions, buff names. Runs once per config load.</summary>
public static class MagebloodConfigResolver
{
    public const string BuffPrefix = "MB_";
    private const float PercentPerFraction = 100f;

    public static IReadOnlyList<MagebloodFlask> Resolve(
        MagebloodConfig config,
        IReadOnlyDictionary<string, int> statIds,
        IReadOnlyDictionary<string, int> tagIds,
        ICollection<MagebloodConfigProblem> problems
    )
    {
        var flasks = new List<MagebloodFlask>();
        foreach (MagebloodFlaskEntry entry in config.Flasks)
        {
            MagebloodFlask flask = ResolveFlask(entry, statIds, tagIds, problems);
            if (flask != null)
            {
                flasks.Add(flask);
            }
        }
        return flasks;
    }

    private static MagebloodFlask ResolveFlask(
        MagebloodFlaskEntry entry,
        IReadOnlyDictionary<string, int> statIds,
        IReadOnlyDictionary<string, int> tagIds,
        ICollection<MagebloodConfigProblem> problems
    )
    {
        var rows = new List<MagebloodBuffStat>();
        var keptRows = new List<MagebloodStatEntry>();
        foreach (MagebloodStatEntry row in entry.Stats)
        {
            if (
                TryResolveRow(
                    entry.Name,
                    row,
                    statIds,
                    tagIds,
                    problems,
                    out MagebloodBuffStat stat
                )
            )
            {
                rows.Add(stat);
                keptRows.Add(row);
            }
        }
        if (rows.Count > 0)
        {
            return new MagebloodFlask(entry.Name, entry.Icon ?? entry.Name, rows, keptRows);
        }

        problems.Add(
            new MagebloodConfigProblem(
                MagebloodConfigProblemCode.NoStats,
                MagebloodConfigKeys.Flasks + "[" + entry.Name + "]",
                "Flask has no usable stat row and is dropped."
            )
        );
        return null;
    }

    private static bool TryResolveRow(
        string flaskName,
        MagebloodStatEntry row,
        IReadOnlyDictionary<string, int> statIds,
        IReadOnlyDictionary<string, int> tagIds,
        ICollection<MagebloodConfigProblem> problems,
        out MagebloodBuffStat stat
    )
    {
        stat = default;
        string rowPath =
            MagebloodConfigKeys.Flasks
            + "["
            + flaskName
            + "]."
            + MagebloodConfigKeys.Stats
            + "["
            + row.RowText
            + "].";
        if (!statIds.TryGetValue(row.Stat, out int statId))
        {
            Report(
                problems,
                MagebloodConfigProblemCode.UnknownStat,
                rowPath + MagebloodConfigKeys.Stat,
                "Unknown stat: " + row.Stat
            );
            return false;
        }
        int tags = 0;
        if (row.Tag != null && !tagIds.TryGetValue(row.Tag, out tags))
        {
            Report(
                problems,
                MagebloodConfigProblemCode.UnknownTag,
                rowPath + MagebloodConfigKeys.Tag,
                "Unknown tag: " + row.Tag
            );
            return false;
        }

        stat = new MagebloodBuffStat(
            statId,
            tags,
            BuffName(flaskName, row),
            row.Added,
            row.Increased / PercentPerFraction,
            row.More / PercentPerFraction
        );
        return true;
    }

    private static void Report(
        ICollection<MagebloodConfigProblem> problems,
        MagebloodConfigProblemCode code,
        string path,
        string message
    )
    {
        problems.Add(new MagebloodConfigProblem(code, path, message));
    }

    private static string BuffName(string flaskName, MagebloodStatEntry row)
    {
        return BuffPrefix + flaskName + "_" + row.RowText;
    }
}
