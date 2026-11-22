using HarmonyLib;
using Il2CppEHG.Multiplayer;
using Il2CppLE.MicrotransactionSystem;
using Il2CppLE.UI.MTXStore;

namespace LastEpoch_Hud.Scripts.Mods.Cosmetics;

/// <summary>Offline: the store lists no supporter packs.</summary>
[HarmonyPatch(typeof(MTXStoreController), nameof(MTXStoreController.GetLiveSupporterPacks))]
public class LiveSupporterPacksPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref Il2CppSystem.Collections.Generic.List<CatalogItem> __result)
    {
        if (GameplayEnvironment.IsOnlinePlay)
        {
            return true;
        }

        __result = new Il2CppSystem.Collections.Generic.List<CatalogItem>();
        return false;
    }
}
