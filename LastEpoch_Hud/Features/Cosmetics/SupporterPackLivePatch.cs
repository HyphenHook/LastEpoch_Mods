using HarmonyLib;
using Il2CppEHG.Multiplayer;
using Il2CppLE.UI.MTXStore;

namespace LastEpoch_Hud.Scripts.Mods.Cosmetics;

/// <summary>Offline: no supporter pack counts as live.</summary>
[HarmonyPatch(typeof(MTXStoreController), nameof(MTXStoreController.IsSupporterPackLive))]
public class SupporterPackLivePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (GameplayEnvironment.IsOnlinePlay)
        {
            return true;
        }

        __result = false;
        return false;
    }
}
