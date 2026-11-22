using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

[HarmonyPatch(
    typeof(UITooltipItem),
    "SetItemImage",
    new[] { typeof(ItemDataUnpacked), typeof(UITooltipItem.ItemTooltipInfo), typeof(bool) }
)]
public class CustomItemTooltipImagePatch
{
    [HarmonyPostfix]
    private static void Postfix(UITooltipItem __instance, ItemDataUnpacked __0, bool __2)
    {
        CustomItemTooltipIcons.Bind(__instance, __0, __2);
    }
}
