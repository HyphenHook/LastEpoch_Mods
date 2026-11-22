namespace LastEpoch_Hud.Scripts.Core.ModUI;

/// <summary>Clamps the legacy x1–x10 point multipliers.</summary>
public static class PointMultiplier
{
    public const int Min = 1;
    public const int Max = 10;
    public const int BelowMinFallback = 2;

    public static int Clamp(float value)
    {
        int multiplier = (int)value;
        if (multiplier < Min)
        {
            return BelowMinFallback;
        }
        if (multiplier > Max)
        {
            return Max;
        }
        return multiplier;
    }
}
