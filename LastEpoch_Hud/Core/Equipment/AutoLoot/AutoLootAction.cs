namespace LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

/// <summary>What auto-loot did with a drop; Kept = the game drops it normally.</summary>
public enum AutoLootAction
{
    Kept,
    Picked,
    Stored,
    Shattered,
    Sold,
}
