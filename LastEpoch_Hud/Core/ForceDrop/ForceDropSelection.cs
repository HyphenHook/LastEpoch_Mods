using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

/// <summary>Builds and resolves Force Drop catalog options by id.</summary>
public static class ForceDropSelection
{
    public static int GameRarity(ForceDropRarity rarity)
    {
        return rarity switch
        {
            ForceDropRarity.Unique => 7,
            ForceDropRarity.Set => 8,
            _ => 0,
        };
    }

    public static List<ForceDropRarity> Rarities(
        int baseType,
        IReadOnlyList<ForceDropUniqueEntry> uniques
    )
    {
        List<ForceDropRarity> result = new();
        if (baseType < 0)
        {
            return result;
        }
        result.Add(ForceDropRarity.Base);
        if (HasAny(uniques, baseType, false))
        {
            result.Add(ForceDropRarity.Unique);
        }
        if (HasAny(uniques, baseType, true))
        {
            result.Add(ForceDropRarity.Set);
        }
        return result;
    }

    public static List<ForceDropItemOption> Items(
        int baseType,
        ForceDropRarity rarity,
        IReadOnlyList<ForceDropItemOption> baseItems,
        IReadOnlyList<ForceDropUniqueEntry> uniques
    )
    {
        List<ForceDropItemOption> result = new();
        if (rarity == ForceDropRarity.Base)
        {
            AddBaseItems(result, baseType, baseItems);
            return result;
        }
        AddUniques(result, baseType, rarity, uniques);
        return result;
    }

    public static bool TryOptionAt<T>(IReadOnlyList<T> options, int dropdownIndex, out T option)
    {
        option = default;
        if (dropdownIndex < 1 || dropdownIndex > options.Count)
        {
            return false;
        }
        option = options[dropdownIndex - 1];
        return true;
    }

    public static string CatalogLabel(string display, string internalName)
    {
        return string.IsNullOrWhiteSpace(display) ? internalName ?? "" : display;
    }

    private static void AddBaseItems(
        List<ForceDropItemOption> result,
        int baseType,
        IReadOnlyList<ForceDropItemOption> baseItems
    )
    {
        foreach (ForceDropItemOption item in baseItems)
        {
            if (item.BaseType == baseType)
            {
                result.Add(item);
            }
        }
    }

    private static void AddUniques(
        List<ForceDropItemOption> result,
        int baseType,
        ForceDropRarity rarity,
        IReadOnlyList<ForceDropUniqueEntry> uniques
    )
    {
        bool isSet = rarity == ForceDropRarity.Set;
        foreach (ForceDropUniqueEntry entry in uniques)
        {
            if (!Selectable(entry, baseType, isSet))
            {
                continue;
            }
            result.Add(
                new ForceDropItemOption(
                    entry.BaseType,
                    entry.SubTypes[0],
                    rarity,
                    entry.UniqueId,
                    entry.Label
                )
            );
        }
    }

    private static bool HasAny(
        IReadOnlyList<ForceDropUniqueEntry> uniques,
        int baseType,
        bool isSet
    )
    {
        foreach (ForceDropUniqueEntry entry in uniques)
        {
            if (Selectable(entry, baseType, isSet))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Selectable(ForceDropUniqueEntry entry, int baseType, bool isSet)
    {
        return entry.BaseType == baseType
            && entry.IsSet == isSet
            && entry.SubTypes != null
            && entry.SubTypes.Count > 0;
    }
}
