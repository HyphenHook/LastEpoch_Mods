using System;

namespace LastEpoch_Hud.Scripts.Core.Volcanic;

public static class VolcanicVisualRules
{
    public const int RingFrameCount = 64;
    public static bool HasAura(bool equippedCore, int activeBuffMask) => equippedCore || activeBuffMask != 0;

    public static (int Current, int Next, float Blend) RingAnimation(double seconds)
    {
        double position = seconds * 24 + .9 * Math.Sin(seconds * .29) + .35 * Math.Sin(seconds * .73);
        position = ((position % RingFrameCount) + RingFrameCount) % RingFrameCount;
        int current = (int)Math.Floor(position);
        return (current, (current + 1) % RingFrameCount, (float)(position - current));
    }

    // Preserve total opacity while compositing the new frame over the previous frame.
    public static (float Back, float Front) RingOpacity(float blend, float opacity = .9f)
    {
        blend = Math.Clamp(blend, 0, 1);
        opacity = Math.Clamp(opacity, 0, .99f);
        float front = opacity * blend;
        return (opacity * (1 - blend) / (1 - front), front);
    }
}
