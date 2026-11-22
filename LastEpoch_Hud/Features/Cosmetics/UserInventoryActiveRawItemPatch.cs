using HarmonyLib;
using Il2CppEHG.Multiplayer;
using Il2CppLE.MicrotransactionSystem;

namespace LastEpoch_Hud.Scripts.Mods.Cosmetics;

/// <summary>Offline: every raw item id counts as active (raw ids are backend-internal, not BackendIDs).</summary>
[HarmonyPatch(typeof(UserInventory), nameof(UserInventory.HasActiveItemRawId))]
public class UserInventoryActiveRawItemPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (GameplayEnvironment.IsOnlinePlay)
        {
            return;
        }

        __result = true;
    }
}
