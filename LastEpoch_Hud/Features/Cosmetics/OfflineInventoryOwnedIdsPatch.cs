using HarmonyLib;
using Il2Cpp;
using Il2CppEHG.Multiplayer;

namespace LastEpoch_Hud.Scripts.Mods.Cosmetics;

/// <summary>Offline: replaces the offline inventory's owned ids with the registry catalog.</summary>
[HarmonyPatch(
    typeof(OfflineUserInventory),
    nameof(OfflineUserInventory.LoadOfflineOwnedCosmeticIds)
)]
public class OfflineInventoryOwnedIdsPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref Il2CppSystem.Collections.Generic.List<string> __result)
    {
        if (GameplayEnvironment.IsOnlinePlay)
        {
            return;
        }

        Il2CppSystem.Collections.Generic.List<string> ids = OfflineCosmeticCatalog.OwnedIds();
        if (ids != null)
        {
            __result = ids;
        }
    }
}
