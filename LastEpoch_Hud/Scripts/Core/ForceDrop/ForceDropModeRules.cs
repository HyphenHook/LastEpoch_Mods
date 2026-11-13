namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

// Illegal mode relaxes gameplay eligibility, not the native definition or
// three-bit packed tier range. Creation mode belongs to the immutable request.
public static class ForceDropModeRules
{
    // Native refresh represents T8 as Primordial even when selected in an
    // ordinary illegal slot. Only that observed transition is permitted;
    // corruption and regular seals keep their own ownership.
    public static ForceDropSeal PersistedSeal(int tier, ForceDropSeal seal, ForceDropMode mode) =>
        mode == ForceDropMode.Illegal && tier == 7 && seal == ForceDropSeal.None
            ? ForceDropSeal.Primordial
            : seal;

    public static int MaximumTier(int definitionTierCount, ForceDropMode mode) =>
        ForceDropTierRules.MaximumDisplayTier(
            definitionTierCount,
            mode == ForceDropMode.Illegal ? 8 : 7
        );

    public static bool CanSealPrimordial(
        int itemType,
        int rarity,
        ForceDropAffixFamily family,
        int definitionTierCount,
        ForceDropMode mode
    ) =>
        definitionTierCount >= 8
        && (
            mode == ForceDropMode.Illegal
            || (
                itemType >= 0
                && itemType <= 24
                // Every rarity can carry a Primordial seal, uniques, sets and legendaries included.
                && family == ForceDropAffixFamily.Standard
            )
        );

    public static int MaximumPersistedTier(int definitionTierCount) =>
        ForceDropTierRules.MaximumDisplayTier(definitionTierCount, 8) - 1;
}
