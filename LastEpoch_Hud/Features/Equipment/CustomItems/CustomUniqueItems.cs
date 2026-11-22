using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Tells whether a game item is one of our custom uniques.</summary>
public static class CustomUniqueItems
{
    /// <summary>Registered subtypes shared by the registrar and the patches.</summary>
    public static readonly CustomUniqueSubtypes Subtypes = new();

    /// <summary>Index in <see cref="CustomUniqueSpecs.All"/>, -1 when the item is not a custom unique.</summary>
    public static int IndexOf(ItemData item)
    {
        if (item.IsNullOrDestroyed())
        {
            return -1;
        }

        int index = CustomUniqueLookup.IndexOf(item.uniqueID);
        if (index < 0 || !item.isUniqueSetOrLegendary())
        {
            return -1;
        }

        return index;
    }
}
