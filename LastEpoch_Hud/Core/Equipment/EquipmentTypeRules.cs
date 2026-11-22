namespace LastEpoch_Hud.Scripts.Core.Items;

public static class EquipmentTypeRules
{
    public const byte CharmBaseType = 43;
    public const byte JewelBaseType = 44;
    public const int JewelCraftedAffixes = 4;
    public const int JewelTransferAffixes = 6;
    public static bool IsEquipment(int type) => (type >= 0 && type <= 24) || type == CharmBaseType || type == JewelBaseType;
    public static bool IsCorruptionOnly(int type) => type == CharmBaseType || type == JewelBaseType;
    public static int TransferSideLimit(int type) => type == JewelBaseType ? JewelTransferAffixes : AffixLimits.MaxPerSide;
}
