using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items;

public static class Items_Ascendance_Name
{
    // Ascendance changes the identity on the existing object. Its unpacked
    // wrapper also keeps a cached full name, which must follow the new identity.
    [HarmonyPatch(typeof(ItemData), "ChangeToUniqueOfSameItemType")]
    public class ChangeToUniqueOfSameItemType
    {
        [HarmonyPostfix]
        static void Postfix(ItemData __instance)
        {
            if (__instance.IsNullOrDestroyed() || !__instance.isUniqueSetOrLegendary())
                return;
            var item = __instance.TryCast<ItemDataUnpacked>();
            if (!item.IsNullOrDestroyed())
                item.MakeFullName();
        }
    }
}
