using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Cosmetics;

/// <summary>Decides which registry cosmetics are real MTX and which are junk.</summary>
public static class CosmeticCatalogRules
{
    private static readonly string[] _realPrefixes =
    {
        "MTX_",
        "mtx_",
        "SP_",
        "KS_",
        "Event_",
        "TD_",
    };

    private static readonly HashSet<string> _blocklist = new(StringComparer.Ordinal)
    {
        "MTX_Chest_TemporalGuardianFix",
    };

    private static readonly char[] _separators = { '_', ' ', '-' };

    /// <summary>Verdict for one entry; precedence EmptyId, UnknownPrefix, Blocklisted, TestEntry, NoStoreIcon, Keep.</summary>
    public static CosmeticVerdict Judge(
        string id,
        string displayName,
        bool hasStoreIcon,
        bool isTitle
    )
    {
        if (string.IsNullOrEmpty(id))
        {
            return CosmeticVerdict.EmptyId;
        }
        if (!HasRealPrefix(id))
        {
            return CosmeticVerdict.UnknownPrefix;
        }
        if (_blocklist.Contains(id))
        {
            return CosmeticVerdict.Blocklisted;
        }
        if (HasTestWord(id) || HasTestWord(displayName))
        {
            return CosmeticVerdict.TestEntry;
        }
        if (!hasStoreIcon && !isTitle)
        {
            return CosmeticVerdict.NoStoreIcon;
        }
        return CosmeticVerdict.Keep;
    }

    /// <summary>True when the entry is not a real MTX cosmetic.</summary>
    public static bool IsJunk(string id, string displayName, bool hasStoreIcon, bool isTitle) =>
        Judge(id, displayName, hasStoreIcon, isTitle) != CosmeticVerdict.Keep;

    private static bool HasRealPrefix(string id)
    {
        foreach (string prefix in _realPrefixes)
        {
            if (id.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasTestWord(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        int start = 0;
        while (start <= text.Length)
        {
            int end = text.IndexOfAny(_separators, start);
            if (end < 0)
            {
                end = text.Length;
            }
            if (IsTestWord(text, start, end - start))
            {
                return true;
            }
            start = end + 1;
        }
        return false;
    }

    private static bool IsTestWord(string text, int start, int length)
    {
        if (length < 4)
        {
            return false;
        }
        if (
            string.CompareOrdinal(text, start, "Test", 0, 4) != 0
            && string.CompareOrdinal(text, start, "test", 0, 4) != 0
        )
        {
            return false;
        }
        if (length == 4)
        {
            return true;
        }
        char next = text[start + 4];
        return char.IsDigit(next) || char.IsUpper(next);
    }
}
