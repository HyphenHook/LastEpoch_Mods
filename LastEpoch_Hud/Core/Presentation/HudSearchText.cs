using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LastEpoch_Hud.Scripts.Core.ModUI;

public static class HudSearchText
{
    public static int ScoreEither(
        string query,
        string label,
        string card,
        string tab,
        string section,
        string alternateLabel,
        string alternateCard,
        string alternateTab,
        string alternateSection
    )
    {
        int canonical = Score(query, label, card, tab, section);
        int alternate = Score(query, alternateLabel, alternateCard, alternateTab, alternateSection);
        if (canonical < 0)
            return alternate;
        if (alternate < 0)
            return canonical;
        return Math.Min(canonical, alternate);
    }

    public static int Score(string query, string label, string card, string tab, string section)
    {
        string needle = Normalize(query);
        if (needle.Length == 0)
            return -1;

        string normalizedLabel = Normalize(label);
        string normalizedCard = Normalize(card);
        string normalizedTab = Normalize(tab);
        string normalizedSection = Normalize(section);
        if (normalizedLabel == needle)
            return 0;
        if (normalizedLabel.StartsWith(needle, StringComparison.Ordinal))
            return 10;
        if (normalizedLabel.Contains(needle, StringComparison.Ordinal))
            return 20;

        string[] terms = needle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (AllTerms(normalizedLabel, terms))
            return 30;
        if (AllTerms(normalizedLabel + " " + normalizedCard, terms))
            return 50;
        if (
            AllTerms(
                normalizedLabel
                    + " "
                    + normalizedCard
                    + " "
                    + normalizedTab
                    + " "
                    + normalizedSection,
                terms
            )
        )
            return 70;
        return -1;
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var output = new StringBuilder(value.Length);
        bool spacing = true;
        foreach (
            char character in value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
        )
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(character))
            {
                output.Append(character);
                spacing = false;
            }
            else if (!spacing)
            {
                output.Append(' ');
                spacing = true;
            }
        }
        return output.ToString().Trim();
    }

    private static bool AllTerms(string value, IReadOnlyList<string> terms)
    {
        foreach (string term in terms)
            if (!value.Contains(term, StringComparison.Ordinal))
                return false;
        return true;
    }
}
