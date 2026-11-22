using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Finds a custom unique from the keys the game hands to our patches.</summary>
public static class CustomUniqueLookup
{
    /// <summary>Index in <see cref="CustomUniqueSpecs.All"/> of the item with this unique id, -1 if none.</summary>
    public static int IndexOf(int uniqueId)
    {
        for (int i = 0; i < CustomUniqueSpecs.All.Count; i++)
        {
            if (CustomUniqueSpecs.All[i].UniqueId == uniqueId)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Index of the item whose icon asset ends the bundle asset name, -1 if none.</summary>
    public static int IconIndexOf(string assetName)
    {
        if (string.IsNullOrEmpty(assetName))
        {
            return -1;
        }
        string name = assetName.Replace('\\', '/');
        for (int i = 0; i < CustomUniqueSpecs.All.Count; i++)
        {
            if (
                name.EndsWith(
                    CustomUniqueSpecs.All[i].IconAsset,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Index of the spec whose icon this spec borrows while its own is missing, -1 if none.</summary>
    public static int IconFallbackIndexOf(int index)
    {
        if (index < 0 || index >= CustomUniqueSpecs.All.Count)
        {
            return -1;
        }

        ushort fallbackId = CustomUniqueSpecs.All[index].IconFallbackUniqueId;
        return fallbackId == 0 ? -1 : IndexOf(fallbackId);
    }

    /// <summary>Visual borrowed by our registered unique matching all keys, null if none.</summary>
    public static CustomItemVisualSource VisualSource(
        int equipmentType,
        int subType,
        int uniqueId,
        CustomUniqueSubtypes registered
    )
    {
        int index = IndexOf(uniqueId);
        if (index < 0)
        {
            return null;
        }

        CustomUniqueSpec spec = CustomUniqueSpecs.All[index];
        if (spec.BaseType != equipmentType)
        {
            return null;
        }

        int registeredSubType = registered.Get(uniqueId);
        if (registeredSubType == CustomItemIds.None || registeredSubType != subType)
        {
            return null;
        }

        return spec.VisualSource;
    }
}
