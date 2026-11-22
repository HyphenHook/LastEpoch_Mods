using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;

namespace LastEpoch_Hud.Scripts.Mods.Items.Charms;

internal static class CharmTooltips
{
    [HarmonyPatch(typeof(ItemTooltipContentBuilder), nameof(ItemTooltipContentBuilder.GetItemTooltipInfo))]
    private static class Modern
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, ItemTooltipInfo __result)
        {
            if (__result == null || !CharmEquipment.IsCharm(__0)) return;
            __result.Name = CharmEquipment.TestName;
            __result.PrimaryType = "Charm";
            __result.SecondaryType = "";
            __result.PotentialInfo = null;
        }
    }

    [HarmonyPatch]
    private static class Legacy
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipDefaultContent));
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipFullContent));
        }

        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, UITooltipItem.ItemTooltipInfo __result)
        {
            if (__result == null || !CharmEquipment.IsCharm(__0)) return;
            __result.nameText = CharmEquipment.TestName;
            __result.typeText = "Charm";
        }
    }
}
