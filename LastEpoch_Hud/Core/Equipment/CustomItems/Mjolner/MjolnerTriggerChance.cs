using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mjolner;

/// <summary>Mjolner trigger chance from its fraction settings (0.3 = 30%).</summary>
public static class MjolnerTriggerChance
{
    public static float Probability(float fraction)
    {
        return Math.Clamp(fraction, 0f, 1f);
    }

    public static int Percent(float fraction)
    {
        return (int)MathF.Round(Probability(fraction) * 100f);
    }

    public static bool Procs(float minFraction, float maxFraction, float chanceT, float roll)
    {
        float min = Probability(minFraction);
        float max = Probability(maxFraction);
        return roll < (min * (1f - chanceT)) + (max * chanceT);
    }
}
