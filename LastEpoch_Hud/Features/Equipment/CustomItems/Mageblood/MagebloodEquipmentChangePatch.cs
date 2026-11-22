using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood;

[HarmonyPatch(typeof(ItemContainersManager), "OnEquipmentOrIdolChange")]
public class MagebloodEquipmentChangePatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        MagebloodBuffs.MarkDirty();
    }
}
