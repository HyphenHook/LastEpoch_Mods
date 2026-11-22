using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.EssentiaSanguis;

[HarmonyPatch(typeof(PlayerLeechTracker), "AddLifeLeech")]
public class EssentiaSanguisLeechPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerLeechTracker __instance, float __0)
    {
        if (Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            return true;
        }

        if (Refs_Manager.player_protection_class.IsNullOrDestroyed())
        {
            return true;
        }

        if (
            !Refs_Manager.player_actor.itemContainersManager.hasUniqueEquipped(
                CustomUniqueSpecs.EssentiaSanguis.UniqueId
            )
        )
        {
            return true;
        }

        Refs_Manager.player_protection_class.CurrentWard += __0;
        return false;
    }
}
