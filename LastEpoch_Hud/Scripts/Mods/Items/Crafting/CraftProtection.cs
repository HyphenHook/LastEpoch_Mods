using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static class CraftProtection
{
    public const string ImmutableMessage = "Corrupted, Unique, Legendary, and Set items cannot be modified.";
    public const string PendingMessage = "Unveil and keep the Obscured Memory before other crafting.";

    public static bool IsImmutable(ItemData item) =>
        !item.IsNullOrDestroyed() && (item.corrupted || item.isUniqueSetOrLegendary());

    public static bool IsAttached(ItemData item, ItemAffix affix) =>
        affix != null && (affix.IsSealed || affix.IsSealedPrimordial || affix.IsSealedCorrupted
            || Items_Encapsulate.IsEncapsulated(item, affix));

    public static string EquipmentRestriction(ItemData item)
    {
        if (item.IsNullOrDestroyed()) return "Put an item in the slot first.";
        return ItemList.isEquipment(item.itemType) ? "" : "Place equipment in the forge slot.";
    }

    public static string Blocked(ItemData item)
    {
        string equipment = EquipmentRestriction(item);
        if (equipment.Length != 0) return equipment;
        if (IsImmutable(item)) return ImmutableMessage;
        if (Items_Reminiscence.ObscuredOf(item) != null) return PendingMessage;
        return "";
    }
}
