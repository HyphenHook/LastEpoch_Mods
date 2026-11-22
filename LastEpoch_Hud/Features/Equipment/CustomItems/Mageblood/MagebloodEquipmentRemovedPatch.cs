using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood;

[HarmonyPatch(typeof(ItemContainersManager), "OnEquipmentOrIdolRemoved")]
public class MagebloodEquipmentRemovedPatch
{
    [HarmonyPostfix]
    private static void Postfix(ItemContainerEntryHandler __1)
    {
        if (!CustomItemRemoval.IsUnique(__1, CustomUniqueSpecs.Mageblood.UniqueId))
        {
            return;
        }

        MagebloodBuffs.MarkDirty();
    }
}
