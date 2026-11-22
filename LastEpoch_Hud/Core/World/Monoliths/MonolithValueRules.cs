using System.Globalization;

namespace LastEpoch_Hud.Scripts.Core.Monoliths;

/// <summary>Input limits for monolith values.</summary>
public static class MonolithValueRules
{
    public const int MaxValue = 9999999;
    public const int CharacterLimit = 7;

    /// <summary>Clamps a value to [0, MaxValue].</summary>
    public static int Clamp(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        return value > MaxValue ? MaxValue : value;
    }

    /// <summary>Parses an integer and clamps it; false (value 0) when not an integer.</summary>
    public static bool TryParse(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return false;
        }

        value = Clamp(parsed);
        return true;
    }
}
