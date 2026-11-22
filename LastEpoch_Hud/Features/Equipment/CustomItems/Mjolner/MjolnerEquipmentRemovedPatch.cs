using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mjolner;

/// <summary>Resets Mjolner run state when Mjolner is unequipped.</summary>
[HarmonyPatch(typeof(ItemContainersManager), "OnEquipmentOrIdolRemoved")]
public class MjolnerEquipmentRemovedPatch
{
    [HarmonyPostfix]
    private static void Postfix(ItemContainerEntryHandler __1)
    {
        if (!CustomItemRemoval.IsUnique(__1, CustomUniqueSpecs.Mjolner.UniqueId))
        {
            return;
        }

        MjolnerTrigger.ResetRun();
    }
}
