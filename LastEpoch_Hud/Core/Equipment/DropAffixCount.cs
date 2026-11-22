using System;

namespace LastEpoch_Hud.Scripts.Core.Items;

/// <summary>Turns a saved affix-count float into the whole number the slider shows.</summary>
public static class DropAffixCount
{
    public static int ToWhole(float value) => (int)Math.Round(value);
}
