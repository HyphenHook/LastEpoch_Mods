using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

/// <summary>Mageblood max resistance effect on one resisted damage value.</summary>
public static class MagebloodMaxResistance
{
    /// <summary>Own copy of the game's resistance cap; never read from the game.</summary>
    public const float GameCap = 0.75f;
    public const float MaxCap = 1f;

    public static float RaisedCap(float bonusPercent)
    {
        if (!(bonusPercent > 0f) || float.IsInfinity(bonusPercent))
        {
            return GameCap;
        }
        return Math.Min(MaxCap, GameCap + (bonusPercent / 100f));
    }

    public static float Rescale(
        float resisted,
        float damage,
        float uncappedResistance,
        float bonusPercent
    )
    {
        float raised = RaisedCap(bonusPercent);
        if (!(uncappedResistance > GameCap) || raised <= GameCap)
        {
            return resisted;
        }
        float extra = Math.Min(uncappedResistance, raised) - GameCap;
        return Math.Max(0f, resisted - (damage * extra));
    }

    public static bool AppliesTo(IntPtr instance, IntPtr player)
    {
        return player != IntPtr.Zero && instance == player;
    }
}
