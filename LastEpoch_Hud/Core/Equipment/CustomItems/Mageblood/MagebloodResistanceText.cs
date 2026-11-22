using System;
using System.Globalization;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

/// <summary>Character sheet resistance text with the Mageblood cap, in the game's format.</summary>
public static class MagebloodResistanceText
{
    /// <summary>Own copy of <c>CharacterSheet.uncappedResistanceColour</c>; never read from the game.</summary>
    public const string UncappedColour = "<color=#a0a0a0>";

    public static bool TryFormat(float uncapped, float bonusPercent, out string text)
    {
        text = null;
        if (!(uncapped > MagebloodMaxResistance.GameCap))
        {
            return false;
        }
        float raised = MagebloodMaxResistance.RaisedCap(bonusPercent);
        if (raised <= MagebloodMaxResistance.GameCap)
        {
            return false;
        }
        text =
            uncapped > raised
                ? Percent(raised) + UncappedColour + "\n(" + Percent(uncapped) + ")"
                : Percent(uncapped);
        return true;
    }

    private static string Percent(float fraction)
    {
        return RoundPercent(fraction).ToString(CultureInfo.InvariantCulture) + "%";
    }

    private static int RoundPercent(float fraction)
    {
        return (int)Math.Round((double)(fraction * 100f));
    }
}
