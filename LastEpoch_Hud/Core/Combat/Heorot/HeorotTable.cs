using System;
using System.Collections.Generic;
using System.Globalization;
using LastEpoch_Hud.Scripts.Core.IO;

namespace LastEpoch_Hud.Scripts.Core.Heorot;

/// <summary>One native stat a Heorot buff grants, as written in HeorotStats.csv.</summary>
public sealed class HeorotStatRow
{
    public string Buff = "";
    /// <summary>"Added", "Increased", or "AilmentChance" (Property is then an ailment name such as Chill).</summary>
    public string Kind = "";
    public string Property = "";
    public string Tags = "";
    public float Value;
}

/// <summary>Reads HeorotStats.csv and HeorotValues.csv. Engine-independent, so it can be tested without the game.</summary>
public static class HeorotTable
{
    public static readonly string[] StatColumns = { "Buff", "Kind", "Property", "Tags", "Value" };
    public static readonly string[] ValueColumns = { "Name", "Value", "Note" };

    public static List<HeorotStatRow> ParseStats(string text, List<string> errors)
    {
        var rows = new List<HeorotStatRow>();
        bool header = false;
        foreach (var (number, line) in CsvText.DataLines(text))
        {
            var f = CsvText.SplitFields(line);
            if (!header)
            {
                header = true;
                if (f.Count != StatColumns.Length || !string.Equals(f[0], "Buff", StringComparison.OrdinalIgnoreCase))
                {
                    errors?.Add("HeorotStats.csv line " + number + ": the header row is missing or has the wrong columns.");
                    return rows;
                }
                continue;
            }
            if (f.Count != StatColumns.Length) { errors?.Add("HeorotStats.csv line " + number + ": expected " + StatColumns.Length + " columns."); continue; }
            string kind = f[1].Trim();
            if (kind != "Added" && kind != "Increased" && kind != "AilmentChance")
            {
                errors?.Add("HeorotStats.csv line " + number + ": Kind must be Added, Increased or AilmentChance.");
                continue;
            }
            if (!float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value))
            {
                errors?.Add("HeorotStats.csv line " + number + ": Value is not a number.");
                continue;
            }
            rows.Add(new HeorotStatRow { Buff = f[0].Trim(), Kind = kind, Property = f[2].Trim(), Tags = f[3].Trim(), Value = value });
        }
        return rows;
    }

    /// <summary>Name,Value,Note rows. Unknown names are kept, so a new buff can add its own numbers without code changes here.</summary>
    public static Dictionary<string, float> ParseValues(string text, List<string> errors)
    {
        var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        bool header = false;
        foreach (var (number, line) in CsvText.DataLines(text))
        {
            var f = CsvText.SplitFields(line);
            if (!header)
            {
                header = true;
                if (f.Count != ValueColumns.Length || !string.Equals(f[0], "Name", StringComparison.OrdinalIgnoreCase))
                {
                    errors?.Add("HeorotValues.csv line " + number + ": the header row is missing or has the wrong columns.");
                    return values;
                }
                continue;
            }
            if (f.Count != ValueColumns.Length) { errors?.Add("HeorotValues.csv line " + number + ": expected " + ValueColumns.Length + " columns."); continue; }
            string name = f[0].Trim();
            if (name.Length == 0 || !float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value))
            {
                errors?.Add("HeorotValues.csv line " + number + ": needs a Name and a numeric Value.");
                continue;
            }
            if (!values.TryAdd(name, value)) errors?.Add("HeorotValues.csv line " + number + ": " + name + " appears twice; the first one is used.");
        }
        return values;
    }
}

/// <summary>The named numbers from HeorotValues.csv. Anything not in the file keeps its built-in default.</summary>
public static class HeorotValues
{
    public const string IncreasedColdPerChillName = "IncreasedColdPerChill";
    public const string AdditionalChillStacksName = "AdditionalChillStacks";

    private static IReadOnlyDictionary<string, float> current;

    public static void Set(IReadOnlyDictionary<string, float> values) => current = values;

    public static float Get(string name, float fallback) =>
        current != null && current.TryGetValue(name, out float value) ? value : fallback;
}
