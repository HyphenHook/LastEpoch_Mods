using System;
using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.Items;

namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

public enum ForceDropSeal
{
    None,
    Regular,
    Primordial,
    Corruption,
}

public enum ForceDropMode
{
    Legal,
    Illegal,
}

// Values here are already resolved, including random rolls. Never read HUD controls
// or sample RNG while constructing or verifying this request.
public sealed class ResolvedForceDropAffix
{
    public int Id { get; }
    public int Tier { get; }
    public int Roll { get; }
    public ForceDropSeal Seal { get; }

    public ResolvedForceDropAffix(int id, int tier, int roll, ForceDropSeal seal)
    {
        if (id < 0 || id > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(id));
        if (tier < 0 || tier > 7)
            throw new ArgumentOutOfRangeException(nameof(tier));
        if (roll < 0 || roll > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(roll));
        if (!Enum.IsDefined(typeof(ForceDropSeal), seal))
            throw new ArgumentOutOfRangeException(nameof(seal));
        Id = id;
        Tier = tier;
        Roll = roll;
        Seal = seal;
    }
}

public sealed class ResolvedForceDrop
{
    public int ItemType { get; }
    public int SubType { get; }
    public int UniqueId { get; }
    public int Rarity { get; }
    public int ForgingPotential { get; }
    public int LegendaryPotential { get; }
    public int WeaversWill { get; }
    public bool Corrupted { get; }
    public IReadOnlyList<int> ImplicitRolls { get; }
    public IReadOnlyList<int> UniqueRolls { get; }
    public IReadOnlyList<ResolvedForceDropAffix> Affixes { get; }
    public IReadOnlyList<int> VariantIds { get; }
    public ResolvedForceDropAffix Corruption { get; }
    public ForceDropMode Mode { get; }
    public bool IsIllegal => Mode == ForceDropMode.Illegal;

    public ResolvedForceDrop(
        int itemType,
        int subType,
        int uniqueId,
        int rarity,
        int forgingPotential,
        int legendaryPotential,
        int weaversWill,
        bool corrupted,
        IEnumerable<int> implicitRolls,
        IEnumerable<int> uniqueRolls,
        IEnumerable<ResolvedForceDropAffix> affixes,
        IEnumerable<int> variantIds,
        ResolvedForceDropAffix corruption,
        ForceDropMode mode = ForceDropMode.Legal
    )
    {
        CheckRange(itemType, byte.MaxValue, nameof(itemType));
        CheckRange(subType, ushort.MaxValue, nameof(subType));
        CheckRange(uniqueId, ushort.MaxValue, nameof(uniqueId));
        CheckRange(rarity, byte.MaxValue, nameof(rarity));
        CheckRange(forgingPotential, byte.MaxValue, nameof(forgingPotential));
        CheckRange(legendaryPotential, byte.MaxValue, nameof(legendaryPotential));
        CheckRange(weaversWill, byte.MaxValue, nameof(weaversWill));
        if (!Enum.IsDefined(typeof(ForceDropMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        Mode = mode;
        ItemType = itemType;
        SubType = subType;
        UniqueId = uniqueId;
        Rarity = rarity;
        // Native corruption consumes these resources. Normalize at the resolved
        // request boundary so construction and final verification agree, even if
        // values remain selected in a disabled HUD field.
        ForgingPotential = corrupted ? 0 : forgingPotential;
        WeaversWill = corrupted ? 0 : weaversWill;
        Corrupted = corrupted;
        ImplicitRolls = CopyRolls(implicitRolls, nameof(implicitRolls));
        UniqueRolls = CopyRolls(uniqueRolls, nameof(uniqueRolls));
        Affixes = Array.AsReadOnly(
            (affixes ?? throw new ArgumentNullException(nameof(affixes))).ToArray()
        );
        // Transferred affixes finish the Legendary upgrade. Native fixed unique
        // modifiers belong to VariantIds and do not consume LP on their own.
        LegendaryPotential =
            corrupted || ForceDropPotentialRules.CreatesLegendary(rarity, Affixes.Count)
                ? 0
                : legendaryPotential;
        VariantIds = Array.AsReadOnly(
            (variantIds ?? throw new ArgumentNullException(nameof(variantIds))).ToArray()
        );
        Corruption = corruption;
        var ids = new HashSet<int>();
        int regularSeals = 0,
            primordialSeals = 0;
        foreach (var affix in Affixes)
        {
            if (affix == null || !ids.Add(affix.Id))
                throw new ArgumentException("Affixes must have distinct defined IDs.");
            if (affix.Seal == ForceDropSeal.Regular)
                regularSeals++;
            else if (affix.Seal == ForceDropSeal.Primordial)
            {
                primordialSeals++;
                if (affix.Tier != 7)
                    throw new ArgumentException("A Primordial sealed affix must be T8.");
            }
            else if (affix.Seal != ForceDropSeal.None)
                throw new ArgumentException("Corruption must use its separate corruption slot.");
        }
        if (primordialSeals > 1)
            throw new ArgumentException("Only one Primordial seal can be represented.");
        if (regularSeals > 1)
            throw new ArgumentException("Only one regular seal can be represented.");
        foreach (int id in VariantIds)
            if (id < 0 || id > ushort.MaxValue || !ids.Add(id))
                throw new ArgumentException("Unique variants must have distinct defined IDs.");
        if (
            corruption != null
            && (
                !corrupted || corruption.Seal != ForceDropSeal.Corruption || !ids.Add(corruption.Id)
            )
        )
            throw new ArgumentException("Corruption must use its own distinct corruption seal.");
        if (Affixes.Count + VariantIds.Count + (corruption == null ? 0 : 1) > AffixLimits.MaxTotal)
            throw new ArgumentException(
                "An item holds at most " + AffixLimits.MaxTotal + " affixes in total."
            );
    }

    static IReadOnlyList<int> CopyRolls(IEnumerable<int> source, string name)
    {
        var result = (source ?? throw new ArgumentNullException(name)).ToArray();
        foreach (int value in result)
            CheckRange(value, byte.MaxValue, name);
        return Array.AsReadOnly(result);
    }

    static void CheckRange(int value, int maximum, string name)
    {
        if (value < 0 || value > maximum)
            throw new ArgumentOutOfRangeException(name);
    }
}

public static class ForceDropPotentialRules
{
    public static bool CreatesLegendary(int rarity, int selectedAffixCount) =>
        rarity >= 7 && selectedAffixCount > 0;
}

public static class ForceDropTierRules
{
    // Display tiers are one-based. The definition, rather than a global slider,
    // owns its actual supported range (ordinary idol affixes have one tier).
    public static int MaximumDisplayTier(int definitionTierCount, int routeMaximum)
    {
        return Math.Max(0, Math.Min(8, Math.Min(definitionTierCount, routeMaximum)));
    }

    public static bool Supports(int storedTier, int definitionTierCount, int routeMaximum)
    {
        return storedTier >= 0
            && storedTier < MaximumDisplayTier(definitionTierCount, routeMaximum);
    }
}
