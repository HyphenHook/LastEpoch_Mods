namespace LastEpoch_Hud.Scripts.Core.Items;

/// <summary>Decides whether to store materials before a pickup that would not fit.</summary>
public static class FullInventoryStoreRule
{
    public static bool ShouldStoreBeforePickup(bool enabled, bool cursorEmpty, bool hasSpace)
    {
        return enabled && cursorEmpty && !hasSpace;
    }
}
