namespace LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

/// <summary>What auto-loot does with a drop before any filter lookup.</summary>
public enum LootRoute
{
    Ignore,
    PickUp,
    Filter,

    /// <summary>Let the game drop it, then pick it up like a click so it lands in storage.</summary>
    Store,
}
