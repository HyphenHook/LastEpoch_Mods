using System;

namespace LastEpoch_Hud.Scripts.Core.Heorot;

public static class HeorotRules
{
    // AilmentID is a byte in the native game. Do not allocate another Chill ID.
    public const byte ChillId = 3;
    // The numbers below come from HeorotValues.csv (embedded at compile time); these are the defaults.
    public const float DefaultIncreasedColdPerChill = .25f;
    public const int DefaultAdditionalChillStacks = 2;
    public static float IncreasedColdPerChill =>
        Math.Max(0f, HeorotValues.Get(HeorotValues.IncreasedColdPerChillName, DefaultIncreasedColdPerChill));
    public static int AdditionalChillStacks =>
        Math.Max(0, (int)Math.Round(HeorotValues.Get(HeorotValues.AdditionalChillStacksName, DefaultAdditionalChillStacks)));

    /// <summary>Power's increased Cold per Chill stack. <paramref name="increasedEffect" /> is the wearer's increased effect of buffs (0.2 = 20%).</summary>
    public static float PowerIncreasedCold(bool powerActive, float targetChillStacks, float increasedEffect = 0f) =>
        powerActive && float.IsFinite(targetChillStacks) && targetChillStacks > 0
            ? IncreasedColdPerChill * targetChillStacks * (1f + Math.Max(0f, increasedEffect)) : 0f;

    // Nonpositive native limits mean unlimited; preserve that convention.
    public static int ChillLimit(int nativeLimit, bool sourceHasDefence) =>
        sourceHasDefence && nativeLimit > 0
            ? (int)Math.Min(int.MaxValue, (long)nativeLimit + AdditionalChillStacks)
            : nativeLimit;

    // Both builds use the native additive pool. This ratio ONLY carries forward any
    // multiplicative edits made to a cached/copy damage array after the original build;
    // it does not calculate the Power bonus or operate on mitigated damage.
    public static float RebuiltColdPreservingScale(float builtCold, float outgoingCold, float rebuiltCold)
    {
        if (builtCold > 0) return rebuiltCold * (outgoingCold / builtCold);
        // A pool clamped to zero can become positive when Power is added. There is no
        // observable post-build multiplier in that case; retain any added outgoing cold.
        return outgoingCold + rebuiltCold;
    }
}
