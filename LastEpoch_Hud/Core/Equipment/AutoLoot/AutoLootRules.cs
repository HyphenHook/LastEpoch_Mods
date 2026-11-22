namespace LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

/// <summary>Decides what auto-loot does with a dropped item.</summary>
public static class AutoLootRules
{
    /// <summary>Routes a drop; direct kinds go to storage when the game would store them on click.</summary>
    public static LootRoute Route(LootKind kind, bool goesToStorage, AutoLootSettings settings)
    {
        return kind switch
        {
            LootKind.Key => DirectRoute(settings.Keys, goesToStorage),
            LootKind.WovenEcho => DirectRoute(settings.WovenEchoes, goesToStorage),
            LootKind.Material => DirectRoute(settings.Materials, goesToStorage),
            LootKind.Gear => settings.UsesFilter ? LootRoute.Filter : LootRoute.Ignore,
            _ => LootRoute.Ignore,
        };
    }

    public static FilterLootPlan ForFilter(LootVisibility visibility, AutoLootSettings settings)
    {
        return visibility switch
        {
            LootVisibility.Shown => new FilterLootPlan(settings.PickupFromFilter, false, false),
            LootVisibility.Hidden => new FilterLootPlan(
                false,
                settings.ShatterFromFilter,
                settings.SellFromFilter
            ),
            _ => default,
        };
    }

    public static AutoLootSettings ForOrigin(AutoLootSettings settings, DropOrigin origin)
    {
        if (origin == DropOrigin.Game)
        {
            return settings;
        }

        return settings with
        {
            SellFromFilter = false,
            ShatterFromFilter = false,
        };
    }

    private static LootRoute DirectRoute(bool enabled, bool goesToStorage)
    {
        if (!enabled)
        {
            return LootRoute.Ignore;
        }

        return goesToStorage ? LootRoute.Store : LootRoute.PickUp;
    }
}
