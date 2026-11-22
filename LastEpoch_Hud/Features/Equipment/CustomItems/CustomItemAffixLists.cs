using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Builds the game's Il2Cpp affix lists from Core data.</summary>
internal static class CustomItemAffixLists
{
    public static Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit> Implicits(
        IReadOnlyList<CustomBaseImplicit> data
    )
    {
        var result = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit>();
        foreach (CustomBaseImplicit item in data)
        {
            result.Add(
                new ItemList.EquipmentImplicit
                {
                    implicitMaxValue = item.MaxValue,
                    implicitValue = item.Value,
                    property = Property(item.Stat),
                    specialTag = item.SpecialTag,
                    tags = Tags(item.Tags),
                    type = ModType(item.Type),
                }
            );
        }

        return result;
    }

    public static Il2CppSystem.Collections.Generic.List<UniqueItemMod> Mods(
        IReadOnlyList<CustomUniqueMod> data
    )
    {
        var result = new Il2CppSystem.Collections.Generic.List<UniqueItemMod>();
        foreach (CustomUniqueMod item in data)
        {
            result.Add(
                new UniqueItemMod
                {
                    canRoll = item.CanRoll,
                    property = Property(item.Stat),
                    tags = Tags(item.Tags),
                    type = ModType(item.Type),
                    maxValue = item.MaxValue,
                    value = item.Value,
                }
            );
        }

        return result;
    }

    public static Il2CppSystem.Collections.Generic.List<UniqueModDisplayListEntry> TooltipEntries(
        IReadOnlyList<byte> data
    )
    {
        var result = new Il2CppSystem.Collections.Generic.List<UniqueModDisplayListEntry>();
        foreach (byte entry in data)
        {
            result.Add(new UniqueModDisplayListEntry(entry));
        }

        return result;
    }

    private static SP Property(CustomItemStat stat)
    {
        return stat switch
        {
            CustomItemStat.Health => SP.Health,
            CustomItemStat.Strength => SP.Strength,
            CustomItemStat.Dexterity => SP.Dexterity,
            CustomItemStat.Damage => SP.Damage,
            CustomItemStat.DodgeRating => SP.DodgeRating,
            CustomItemStat.Mana => SP.Mana,
            CustomItemStat.Intelligence => SP.Intelligence,
            CustomItemStat.FireResistance => SP.FireResistance,
            CustomItemStat.IncreasedCooldownRecoverySpeed => SP.IncreasedCooldownRecoverySpeed,
            CustomItemStat.HealthLeech => SP.HealthLeech,
            CustomItemStat.LightningResistance => SP.LightningResistance,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
        };
    }

    private static AT Tags(CustomItemTag tag)
    {
        return tag switch
        {
            CustomItemTag.None => AT.None,
            CustomItemTag.Physical => AT.Physical,
            CustomItemTag.Lightning => AT.Lightning,
            _ => throw new ArgumentOutOfRangeException(nameof(tag), tag, null),
        };
    }

    private static BaseStats.ModType ModType(CustomItemModType type)
    {
        return type switch
        {
            CustomItemModType.Added => BaseStats.ModType.ADDED,
            CustomItemModType.Increased => BaseStats.ModType.INCREASED,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }
}
