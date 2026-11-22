using System;
using System.Text.RegularExpressions;

namespace LastEpoch_Hud.Scripts.Core.Jewels;

public static class JewelTooltipText
{
    public static bool Matches(string text, string wording)
    {
        string plain = Regex.Replace(text ?? "", "<[^>]*>", "");
        plain = Regex.Replace(plain, @"\s+", " ");
        plain = Regex.Replace(plain, @"\bDamage Mana Leech\b", "Damage Leeched as Mana", RegexOptions.IgnoreCase);
        if (wording.StartsWith("of ", StringComparison.OrdinalIgnoreCase)) wording = wording.Substring(3);
        if (wording == "Damage Leeched as Mana")
            return Regex.IsMatch(plain, @"(?<!Spell )(?<!Melee )(?<!Throwing )\bDamage Leeched as Mana\b", RegexOptions.IgnoreCase);
        return plain.IndexOf(wording, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string ReplaceValues(string text, string value, string minimum, string maximum, bool showRange)
    {
        bool replacedValue = false;
        int rangeNumber = -1;
        var resultBuilder = new System.Text.StringBuilder();
        int at = 0;
        foreach (Match match in Regex.Matches(text, @"<[^>]*>|(?<range>Range:\s*)|(?<number>[+-]?\d+(?:\.\d+)?%?)", RegexOptions.IgnoreCase))
        {
            resultBuilder.Append(text, at, match.Index - at);
            string replacement = match.Value;
            if (match.Groups["range"].Success) rangeNumber = 0;
            else if (match.Groups["number"].Success)
            {
                if (rangeNumber == 0) { rangeNumber++; replacement = minimum; }
                else if (rangeNumber == 1) { rangeNumber++; replacement = maximum; }
                else if (!replacedValue && rangeNumber < 0 && match.Value.EndsWith("%"))
                {
                    replacedValue = true;
                    replacement = value;
                }
            }
            resultBuilder.Append(replacement);
            at = match.Index + match.Length;
        }
        resultBuilder.Append(text, at, text.Length - at);
        string result = Regex.Replace(resultBuilder.ToString(),
            @"\b((?:Spell |Melee |Throwing )?)Damage Mana Leech\b", "of $1Damage Leeched as Mana", RegexOptions.IgnoreCase);
        if (showRange && rangeNumber < 0)
            result += "\n<size=85%><color=#6C8FD6>Range: " + minimum + " to " + maximum + "</color></size>";
        return result;
    }
}
