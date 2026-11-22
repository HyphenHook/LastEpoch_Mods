using System.Globalization;

namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Game localization keys for custom items.</summary>
public static class CustomItemKeys
{
    public static string SubtypeName(int baseType, int baseId)
    {
        return "Item_SubType_Name_" + Format(baseType) + "_" + Format(baseId);
    }

    public static string UniqueName(int uniqueId)
    {
        return "Unique_Name_" + Format(uniqueId);
    }

    public static string UniqueTooltip(int uniqueId)
    {
        return "Unique_Tooltip_0_" + Format(uniqueId);
    }

    public static string UniqueLore(int uniqueId)
    {
        return "Unique_Lore_" + Format(uniqueId);
    }

    private static string Format(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
