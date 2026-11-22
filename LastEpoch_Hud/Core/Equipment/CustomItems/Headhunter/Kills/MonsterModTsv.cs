using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;

/// <summary>Formats monster mod rows as tab-separated text.</summary>
public static class MonsterModTsv
{
    public const string Header =
        "list\tmodKey\ttitle\tmodType\trarityRequirement\tdisplayGroup\tclass\trareModifier\tscalingType\tstats";

    public static string Build(IReadOnlyList<MonsterModRow> rows)
    {
        StringBuilder text = new StringBuilder(Header).Append('\n');
        foreach (MonsterModRow row in rows)
        {
            text.Append(Row(row)).Append('\n');
        }

        return text.ToString();
    }

    public static string Row(MonsterModRow row)
    {
        string rare = row.RareModifier.HasValue ? Number(row.RareModifier.Value) : "";
        string scaling = row.RareModifier.HasValue ? Clean(row.ScalingType) : "";
        return string.Join(
            "\t",
            Clean(row.List),
            row.ModKey.ToString(CultureInfo.InvariantCulture),
            Clean(row.Title),
            Clean(row.ModType),
            Clean(row.RarityRequirement),
            Clean(row.DisplayGroup),
            Clean(row.ClassName),
            rare,
            scaling,
            Clean(Stats(row.Stats))
        );
    }

    public static string Stats(IReadOnlyList<MonsterModStat> stats)
    {
        var parts = new List<string>(stats.Count);
        foreach (MonsterModStat stat in stats)
        {
            parts.Add(Stat(stat));
        }

        return string.Join(";", parts);
    }

    public static string Clean(string text)
    {
        if (text == null)
        {
            return "";
        }

        return text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }

    private static string Stat(MonsterModStat stat)
    {
        return $"{stat.Property}:{Number(stat.Added)}/{Number(stat.Increased)}/{More(stat.More)}[{stat.Tags}]";
    }

    private static string More(IReadOnlyList<float> more)
    {
        if (more.Count == 0)
        {
            return "0";
        }

        var parts = new List<string>(more.Count);
        foreach (float value in more)
        {
            parts.Add(Number(value));
        }

        return string.Join(",", parts);
    }

    private static string Number(float value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
