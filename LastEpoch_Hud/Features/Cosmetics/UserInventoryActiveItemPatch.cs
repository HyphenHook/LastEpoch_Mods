using HarmonyLib;
using Il2CppEHG.Multiplayer;
using Il2CppLE.MicrotransactionSystem;

namespace LastEpoch_Hud.Scripts.Mods.Cosmetics;

/// <summary>Offline: an item is active when the registry catalog owns it.</summary>
[HarmonyPatch(typeof(UserInventory), nameof(UserInventory.HasActiveItemId))]
public class UserInventoryActiveItemPatch
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
