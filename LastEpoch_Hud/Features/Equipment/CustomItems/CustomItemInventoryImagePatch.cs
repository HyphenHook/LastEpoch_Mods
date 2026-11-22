using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

[HarmonyPatch(typeof(InventoryItemUI), "SetImageSpritesAndColours")]
public class CustomItemInventoryImagePatch
{
    [HarmonyPostfix]
    private static void Postfix(InventoryItemUI __instance)
    {
        CustomItemInventoryIcons.Bind(__instance);
    }
}
