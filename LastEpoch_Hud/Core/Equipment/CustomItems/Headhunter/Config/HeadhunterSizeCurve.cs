using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

/// <summary>Model size growth per distinct buff, in percent, with a cap.</summary>
public readonly record struct HeadhunterSizeCurve(float PerBuffPercent, float CapPercent)
{
    public const float NormalFactor = 1f;

    /// <summary>Scale multiplier for a distinct buff count.</summary>
    public float Factor(int buffs)
    {
        if (buffs <= 0)
        {
            return NormalFactor;
        }

        float percent = Math.Min(buffs * PerBuffPercent, CapPercent);
        return NormalFactor + (percent / 100f);
    }
}
