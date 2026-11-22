using System;
using System.Collections.Generic;
using System.Text;

namespace LastEpoch_Hud.Scripts.Core.IO;

/// <summary>Minimal CSV reading and writing for the mod's data tables (quotes, "" escapes, # comment lines).</summary>
public static class CsvText
{
    /// <summary>Non-empty, non-comment lines with their 1-based line numbers.</summary>
    public static IEnumerable<(int Number, string Text)> DataLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        int number = 0;
        foreach (string raw in text.Split('\n'))
        {
            number++;
            string line = raw.TrimEnd('\r');
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
            yield return (number, line);
        }
    }

    public static List<string> SplitFields(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else current.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        fields.Add(current.ToString());
        return fields;
    }

    public static string Quote(string value)
    {
        value ??= "";
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
