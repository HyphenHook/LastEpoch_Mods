using System.Collections.Generic;
using System.Linq;

namespace LastEpoch_Hud.Scripts.Core.Crafting;

public sealed class MemoryChoice
{
    public ushort AffixId;
    public byte Roll;
}

/// <summary>The saved affix slot and replacement choices, shared by crafting and Bazaar listings.</summary>
public sealed class ObscuredMemory
{
    public static bool SupportsTier(int storedTier, int minimumDisplayTier, int maximumDisplayTier) =>
        storedTier >= 0 && minimumDisplayTier >= 1
        && storedTier + 1 >= minimumDisplayTier && storedTier < maximumDisplayTier;

    public byte Tier;
    public int AffixType;
    public int? OriginalAffixId;
    public bool Revealed;
    public List<MemoryChoice> Choices = new();

    public ObscuredMemory Copy() => new()
    {
        Tier = Tier,
        AffixType = AffixType,
        OriginalAffixId = OriginalAffixId,
        Revealed = Revealed,
        Choices = Choices.Select(choice => new MemoryChoice { AffixId = choice.AffixId, Roll = choice.Roll }).ToList(),
    };
}
