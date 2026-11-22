using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LastEpoch_Hud.Scripts.Core.IO;

namespace LastEpoch_Hud.Scripts.Core.Jewels;

/// <summary>One Jewel affix as it is written in JewelAffixes.csv: identity, wording, weight and the T1..T7 value ranges.</summary>
public sealed class JewelAffixRow
{
    public const int TierCount = 7;

    public int Id;
    /// <summary>"Inherited" copies an Idol affix (SourceId is the native id); "Exclusive" is defined by the row itself.</summary>
    public string Origin = "Exclusive";
    public int SourceId;
    public string Name = "";
    /// <summary>The word the game builds the item's own name from (the name on the ground and in the loot filter).
    /// Empty keeps the game's wording for an Inherited row, and uses Name for an Exclusive one.</summary>
    public string Title = "";
    /// <summary>How the title is used: Custom, Class or Experimental. Empty keeps the default.</summary>
    public string TitleType = "";
    /// <summary>"Prefix" or "Suffix".</summary>
    public string Side = "Prefix";
    public string Property = "";
    public string ModType = "";
    public string Tags = "";
    public int Weight;
    /// <summary>The lowest display tier (1..7) this affix can be generated on.</summary>
    public int MinTier = 1;
    /// <summary>Roll granularity for the custom-effect affixes (Damage as Extra, Mana Leech): 0.01 rolls only in whole 1%
    /// steps. 0 keeps the continuous roll. Affixes the game itself calculates ignore it.</summary>
    public float Step;
    public float[] Min = new float[TierCount];
    public float[] Max = new float[TierCount];

    public bool IsInherited => string.Equals(Origin, "Inherited", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Reads and writes the Jewel affix table. Engine-independent so it can be tested without the game.</summary>
public static class JewelAffixTable
{
    public static readonly string[] Columns = BuildColumns();

    private static string[] BuildColumns()
    {
        var columns = new List<string> { "Id", "Origin", "SourceId", "Name", "Title", "TitleType", "Side", "Property", "ModType", "Tags", "Weight", "MinTier", "Step" };
        for (int tier = 1; tier <= JewelAffixRow.TierCount; tier++)
        {
            columns.Add("T" + tier + "Min");
            columns.Add("T" + tier + "Max");
        }
        return columns.ToArray();
    }

    public static string Write(IEnumerable<JewelAffixRow> rows)
    {
        var text = new StringBuilder();
        text.AppendLine("# Jewel affixes. Edit the numbers here in the source, then rebuild: this file is embedded in the mod at compile time.");
        text.AppendLine("# Weight 0 stops an affix being generated. MinTier is the lowest tier (1-7) it can appear on. T1..T7 are the min/max roll per tier.");
        text.AppendLine("# Step: 0.01 makes the roll land only on whole 1% steps from the minimum (Damage as Extra, Mana Leech only). 0 keeps it continuous.");
        text.AppendLine("# Inherited rows copy an Idol affix (SourceId) with its own ranges. Exclusive rows may be added with a new Id from 4000 to 4095.");
        text.AppendLine("# Title is the word the item's own name is built from (Custom is the usual TitleType). Leave it empty to keep the game's wording.");
        text.AppendLine(string.Join(",", Columns));
        foreach (var row in rows)
        {
            var fields = new List<string>
            {
                row.Id.ToString(CultureInfo.InvariantCulture), CsvText.Quote(row.Origin), row.SourceId.ToString(CultureInfo.InvariantCulture),
                CsvText.Quote(row.Name), CsvText.Quote(row.Title), CsvText.Quote(row.TitleType), CsvText.Quote(row.Side), CsvText.Quote(row.Property), CsvText.Quote(row.ModType), CsvText.Quote(row.Tags),
                row.Weight.ToString(CultureInfo.InvariantCulture), row.MinTier.ToString(CultureInfo.InvariantCulture),
                row.Step.ToString("R", CultureInfo.InvariantCulture),
            };
            for (int tier = 0; tier < JewelAffixRow.TierCount; tier++)
            {
                fields.Add(row.Min[tier].ToString("R", CultureInfo.InvariantCulture));
                fields.Add(row.Max[tier].ToString("R", CultureInfo.InvariantCulture));
            }
            text.AppendLine(string.Join(",", fields));
        }
        return text.ToString();
    }

    /// <summary>Parses the table. A bad row is reported in <paramref name="errors" /> and skipped; the rest still load.</summary>
    public static List<JewelAffixRow> Parse(string text, List<string> errors)
    {
        var rows = new List<JewelAffixRow>();
        if (string.IsNullOrWhiteSpace(text)) return rows;
        var seen = new HashSet<int>();
        int lineNumber = 0;
        bool headerSeen = false;
        foreach (string rawLine in text.Split('\n'))
        {
            lineNumber++;
            string line = rawLine.TrimEnd('\r');
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
            var fields = CsvText.SplitFields(line);
            if (!headerSeen)
            {
                headerSeen = true;
                if (fields.Count != Columns.Length || !string.Equals(fields[0], "Id", StringComparison.OrdinalIgnoreCase))
                {
                    errors?.Add("Line " + lineNumber + ": the header row is missing or has the wrong columns.");
                    return rows;
                }
                continue;
            }
            if (fields.Count != Columns.Length)
            {
                errors?.Add("Line " + lineNumber + ": expected " + Columns.Length + " columns but found " + fields.Count + ".");
                continue;
            }
            var row = new JewelAffixRow();
            string problem = Fill(row, fields);
            if (problem == null && !seen.Add(row.Id)) problem = "id " + row.Id + " appears twice";
            if (problem != null)
            {
                errors?.Add("Line " + lineNumber + ": " + problem + ".");
                continue;
            }
            rows.Add(row);
        }
        return rows;
    }

    private static string Fill(JewelAffixRow row, List<string> f)
    {
        if (!int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out row.Id)) return "Id is not a number";
        row.Origin = f[1].Trim();
        if (!row.IsInherited && !string.Equals(row.Origin, "Exclusive", StringComparison.OrdinalIgnoreCase))
            return "Origin must be Inherited or Exclusive";
        if (!int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out row.SourceId)) return "SourceId is not a number";
        row.Name = f[3].Trim();
        row.Title = f[4].Trim();
        row.TitleType = f[5].Trim();
        row.Side = f[6].Trim();
        if (!string.Equals(row.Side, "Prefix", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(row.Side, "Suffix", StringComparison.OrdinalIgnoreCase)) return "Side must be Prefix or Suffix";
        row.Property = f[7].Trim();
        row.ModType = f[8].Trim();
        row.Tags = f[9].Trim();
        if (!int.TryParse(f[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out row.Weight) || row.Weight < 0)
            return "Weight must be a whole number, 0 or more";
        if (!int.TryParse(f[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out row.MinTier) || row.MinTier < 1 || row.MinTier > 7)
            return "MinTier must be 1 to 7";
        if (!float.TryParse(f[12], NumberStyles.Float, CultureInfo.InvariantCulture, out row.Step) || row.Step < 0 || !float.IsFinite(row.Step))
            return "Step must be 0 or a positive number";
        for (int tier = 0; tier < JewelAffixRow.TierCount; tier++)
        {
            if (!float.TryParse(f[13 + tier * 2], NumberStyles.Float, CultureInfo.InvariantCulture, out float min)
                || !float.TryParse(f[14 + tier * 2], NumberStyles.Float, CultureInfo.InvariantCulture, out float max))
                return "T" + (tier + 1) + " range is not a number";
            if (max < min) return "T" + (tier + 1) + "Max is below T" + (tier + 1) + "Min";
            row.Min[tier] = min;
            row.Max[tier] = max;
        }
        return null;
    }
}
