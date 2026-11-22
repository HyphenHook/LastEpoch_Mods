using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

internal readonly record struct ForceDropItemIdentity(
    int BaseType,
    int SubType,
    int Rarity,
    int UniqueId
);

internal static class ForceDropItemSearch
{
    public static bool Matches(string query, string name, string aliases)
    {
        string haystack = Normalize((name ?? "") + "\n" + (aliases ?? ""));
        foreach (
            string token in (query ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
        )
            if (haystack.IndexOf(Normalize(token), StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        return true;
    }

    // Search works with both the active game translation and the raw English aliases.
    // Decomposing combining marks permits accent-free Latin input (e.g. "epée").
    static string Normalize(string text)
    {
        var result = new StringBuilder();
        foreach (char c in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                result.Append(c);
        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    // Names are not keys: two items can share a label. Refuse ambiguous/stale ids.
    public static int FindOption(
        ForceDropItemIdentity identity,
        IEnumerable<(int Index, ForceDropItemIdentity Identity)> options
    )
    {
        int match = -1;
        foreach (var option in options)
            if (option.Index > 0 && option.Identity == identity)
            {
                if (match > 0)
                    return -1;
                match = option.Index;
            }
        return match;
    }
}
