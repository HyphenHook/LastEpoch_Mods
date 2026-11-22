using HarmonyLib;
using Il2CppEHG.Multiplayer;
using Il2CppLE.Networking.Cosmetics;

namespace LastEpoch_Hud.Scripts.Mods.Cosmetics;

/// <summary>Offline: an item is owned when the registry catalog owns it.</summary>
[HarmonyPatch(typeof(PlayerCosmeticInventory), nameof(PlayerCosmeticInventory.IsOwnedItem))]
public class PlayerCosmeticInventoryOwnedPatch
{
    [HarmonyPostfix]
    private static void Postfix(string itemId, ref bool __result)
    {
        if (GameplayEnvironment.IsOnlinePlay)
        {
            return;
        }

        if (OfflineCosmeticCatalog.TryIsOwned(itemId, out bool owned))
        {
            __result = owned;
        }
    }
}
