using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Scripts.Core.Economy;

/// <summary>Crafting metadata stored alongside a listing's native item bytes.</summary>
public sealed class BazaarCraftingState
{
    public int? EncapsulatedAffixId;
    public int? RemindedAffixId;
    public ObscuredMemory Memory;
    public AttunementState Attunement;

    public BazaarCraftingState Copy() => new()
    {
        EncapsulatedAffixId = EncapsulatedAffixId,
        RemindedAffixId = RemindedAffixId,
        Memory = Memory?.Copy(),
        Attunement = Attunement?.Copy(),
    };
}
