using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LastEpoch_Hud.Scripts.Core.IO;

namespace LastEpoch_Hud.Scripts.Core.Jewels;

/// <summary>One implicit of a Jewel subtype, as written in JewelImplicits.csv.</summary>
public sealed class JewelImplicitRow
{
    /// <summary>0 Cobalt, 1 Ruby, 2 Emerald, 3 Prismatic.</summary>
    public int Subtype;
    public string Property = "";
    /// <summary>ADDED or INCREASED (a fraction: 0.12 is 12%).</summary>
    public string ModType = "";
    public string Tags = "";
    public int SpecialTag;
    public float Min;
    public float Max;
}

/// <summary>Reads and writes the Jewel implicit table. Engine-independent so it can be tested without the game.</summary>
public static class JewelImplicitTable
{
    public static readonly string[] Columns = { "Subtype", "SubtypeName", "Property", "ModType", "Tags", "SpecialTag", "Min", "Max" };

    public static string Write(IEnumerable<JewelImplicitRow> rows, IReadOnlyList<string> subtypeNames)
    {
        var text = new StringBuilder();
        text.AppendLine("# Jewel implicits. Edit the numbers here in the source, then rebuild: this file is embedded in the mod at compile time.");
        text.AppendLine("# Subtype: 0 Cobalt, 1 Ruby, 2 Emerald, 3 Prismatic. The order of a subtype's rows is the order of its implicits.");
        text.AppendLine("# ModType ADDED is a flat amount, INCREASED is a fraction (0.12 = 12%). Min and Max are the roll range.");
        text.AppendLine("# Keep the number of rows per subtype if you already hold Jewels: their saved rolls follow the rows.");
        text.AppendLine(string.Join(",", Columns));
        foreach (var row in rows)
        {
            string name = row.Subtype >= 0 && row.Subtype < subtypeNames.Count ? subtypeNames[row.Subtype] : "";
            text.AppendLine(string.Join(",", row.Subtype.ToString(CultureInfo.InvariantCulture), CsvText.Quote(name),
                CsvText.Quote(row.Property), CsvText.Quote(row.ModType), CsvText.Quote(row.Tags),
                row.SpecialTag.ToString(CultureInfo.InvariantCulture),
                row.Min.ToString("R", CultureInfo.InvariantCulture), row.Max.ToString("R", CultureInfo.InvariantCulture)));
        }
        return text.ToString();
    }

    /// <summary>Parses the table. A bad row is reported and skipped.</summary>
    public static List<JewelImplicitRow> Parse(string text, List<string> errors)
    {
        var rows = new List<JewelImplicitRow>();
        bool header = false;
        foreach (var (number, line) in CsvText.DataLines(text))
        {
            var f = CsvText.SplitFields(line);
            if (!header)
            {
                header = true;
                if (f.Count != Columns.Length || !string.Equals(f[0], "Subtype", StringComparison.OrdinalIgnoreCase))
                {
                    errors?.Add("JewelImplicits.csv line " + number + ": the header row is missing or has the wrong columns.");
                    return rows;
                }
                continue;
            }
            if (f.Count != Columns.Length) { errors?.Add("JewelImplicits.csv line " + number + ": expected " + Columns.Length + " columns."); continue; }
            var row = new JewelImplicitRow { Property = f[2].Trim(), ModType = f[3].Trim(), Tags = f[4].Trim() };
            if (!int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out row.Subtype) || row.Subtype < 0 || row.Subtype >= JewelAffixRules.SubtypeCount)
                { errors?.Add("JewelImplicits.csv line " + number + ": Subtype must be 0 to " + (JewelAffixRules.SubtypeCount - 1) + "."); continue; }
            if (row.Property.Length == 0) { errors?.Add("JewelImplicits.csv line " + number + ": Property is empty."); continue; }
            if (!string.Equals(row.ModType, "ADDED", StringComparison.OrdinalIgnoreCase) && !string.Equals(row.ModType, "INCREASED", StringComparison.OrdinalIgnoreCase))
                { errors?.Add("JewelImplicits.csv line " + number + ": ModType must be ADDED or INCREASED."); continue; }
            if (!int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out row.SpecialTag) || row.SpecialTag < 0 || row.SpecialTag > 255)
                { errors?.Add("JewelImplicits.csv line " + number + ": SpecialTag must be 0 to 255."); continue; }
            if (!float.TryParse(f[6], NumberStyles.Float, CultureInfo.InvariantCulture, out row.Min)
                || !float.TryParse(f[7], NumberStyles.Float, CultureInfo.InvariantCulture, out row.Max) || row.Max < row.Min)
                { errors?.Add("JewelImplicits.csv line " + number + ": Min and Max must be numbers with Max at least Min."); continue; }
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>True when every subtype has at least one implicit.</summary>
    public static bool CoversEverySubtype(IReadOnlyCollection<JewelImplicitRow> rows)
    {
        var seen = new HashSet<int>();
        foreach (var row in rows) seen.Add(row.Subtype);
        return seen.Count == JewelAffixRules.SubtypeCount;
    }
}
