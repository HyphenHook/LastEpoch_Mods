using System;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Holds the weapon reach multiplier that follows the Headhunter model size.</summary>
public sealed class HeadhunterReachScale
{
    public float Factor { get; private set; } = HeadhunterSizeCurve.NormalFactor;

    public bool IsNormal => Factor == HeadhunterSizeCurve.NormalFactor;

    public void Set(float factor)
    {
        Factor = factor;
    }

    public float Scale(float range)
    {
        if (IsNormal)
        {
            return range;
        }

        return range * Factor;
    }

    public float ScaleFor(IntPtr instance, IntPtr player, float range)
    {
        if (player == IntPtr.Zero || instance != player)
        {
            return range;
        }

        return Scale(range);
    }

    public void Reset()
    {
        Factor = HeadhunterSizeCurve.NormalFactor;
    }
}
