using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Maps a game item to a LootKind with the game's own type helpers; unknown types are Other.</summary>
public static class LootKindReader
{
    public static LootKind Read(ItemData item)
    {
        if (item.isIdolAltar())
        {
            return LootKind.Other;
        }

        int type = item.itemType;
        if (ItemList.isIdol(type) || ItemList.isEquipment(type))
        {
            return LootKind.Gear;
        }

        if (Item.isKey(type))
        {
            return LootKind.Key;
        }

        if (Item.isWovenEcho(type))
        {
            return LootKind.WovenEcho;
        }

        if (ItemList.isCraftingItem(type))
        {
            return LootKind.Material;
        }

        return LootKind.Other;
    }
}
