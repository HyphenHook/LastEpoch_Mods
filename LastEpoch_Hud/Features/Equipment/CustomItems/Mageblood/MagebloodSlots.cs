using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood;

/// <summary>Reads the rolled flask slot count from a Mageblood item.</summary>
public static class MagebloodSlots
{
    /// <summary>Slot count of the item; false when it is no Mageblood or has no rolls.</summary>
    public static bool TryRead(ItemData item, out int slots)
    {
        slots = 0;
        if (item.IsNullOrDestroyed())
        {
            return false;
        }

        if (item.uniqueID != CustomUniqueSpecs.Mageblood.UniqueId || !item.isUniqueSetOrLegendary())
        {
            return false;
        }

        return MagebloodFlaskSlots.TryFromRolls(item.uniqueRolls, out slots);
    }
}
