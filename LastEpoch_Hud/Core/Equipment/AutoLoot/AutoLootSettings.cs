namespace LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

/// <summary>Snapshot of the six auto-loot toggles, read once per drop.</summary>
public readonly record struct AutoLootSettings(
    bool Keys,
    bool WovenEchoes,
    bool Materials,
    bool PickupFromFilter,
    bool SellFromFilter,
    bool ShatterFromFilter
)
{
    public bool AnyEnabled => Keys || WovenEchoes || Materials || UsesFilter;

    public bool UsesFilter => PickupFromFilter || SellFromFilter || ShatterFromFilter;
}
