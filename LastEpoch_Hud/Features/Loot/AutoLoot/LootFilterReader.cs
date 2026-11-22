using Il2Cpp;
using Il2CppItemFiltering;
using LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Reads the player's active loot filter and its verdict on an item.</summary>
public static class LootFilterReader
{
    /// <summary>The player's loaded loot filter, or null when none is loaded; the in-game filter toggle (ManualFilterDisable) only hides labels, auto-loot keeps the rules.</summary>
    public static ItemFilter ActiveFilter()
    {
        ItemFilterManager manager = ItemFilterManager.Instance;
        if (manager.IsNullOrDestroyed())
        {
            return null;
        }

        ItemFilter filter = manager.Filter;
        return filter.IsNullOrDestroyed() ? null : filter;
    }

    public static LootVisibility Read(ItemFilter filter, ItemDataUnpacked item)
    {
        if (filter == null)
        {
            return LootVisibility.Unfiltered;
        }

        if (!LootFilterMatch.TryMatch(filter, item, out Rule.RuleOutcome outcome))
        {
            ErrorLog.Report("ItemFilter.Match not found", "AutoLoot filter");
            return LootVisibility.Unfiltered;
        }

        return outcome == Rule.RuleOutcome.HIDE ? LootVisibility.Hidden : LootVisibility.Shown;
    }
}
