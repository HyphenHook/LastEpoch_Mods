using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Reads one mod text from a locale dictionary.</summary>
public static class LocaleText
{
    public static string Get(IReadOnlyDictionary<string, string> texts, string key)
    {
        if (texts == null || string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (!texts.TryGetValue(key, out string text))
        {
            return null;
        }

        return string.IsNullOrEmpty(text) ? null : text;
    }
}
