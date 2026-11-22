namespace LastEpoch_Hud.Scripts.Core.Items;

// Picks a forced legendary potential from the mod's min/max drop settings.
public static class LegendaryPotentialRoll
{
    // Whole LP in [(int)min, (int)max], both inclusive; fraction is a random value in [0, 1].
    public static int Pick(float min, float max, float fraction)
    {
        int low = (int)min;
        int high = (int)max;
        if (high <= low)
        {
            return low;
        }

        int count = high - low + 1;
        int index = (int)(fraction * count);
        if (index >= count)
        {
            return high;
        }

        return low + index;
    }
}
