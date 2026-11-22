using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

[HarmonyPatch(typeof(ItemContainersManager), "OnEquipmentOrIdolRemoved")]
public class HeadhunterEquipmentRemovedPatch
{
    [HarmonyPostfix]
    private static void Postfix(ItemContainerEntryHandler __1)
    {
        HeadhunterUnequipHandler.OnItemRemoved(__1);
    }
}
