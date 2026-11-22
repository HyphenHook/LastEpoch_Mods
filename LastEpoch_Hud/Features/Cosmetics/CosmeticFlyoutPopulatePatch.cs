using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppEHG.Multiplayer;
using Il2CppLE.UI.MTXStore;

namespace LastEpoch_Hud.Scripts.Mods.Cosmetics;

/// <summary>Offline: swallows flyout populate errors and hides the loading overlay.</summary>
[HarmonyPatch(
    typeof(CosmeticSelectionFlyoutPanelUI),
    nameof(CosmeticSelectionFlyoutPanelUI.PopulateFlyoutWindowAsync)
)]
public class CosmeticFlyoutPopulatePatch
{
    private const string ErrorContext = "Offline cosmetic flyout";

    [HarmonyFinalizer]
    private static Exception Finalizer(
        Exception __exception,
        CosmeticSelectionFlyoutPanelUI __instance
    )
    {
        if (GameplayEnvironment.IsOnlinePlay)
        {
            return __exception;
        }

        if (__exception == null)
        {
            return null;
        }

        // Offline, the async populate chain hits backend calls; a throw would leave the overlay up forever.
        HideLoadingOverlay(__instance);
        ErrorLog.Report(__exception, ErrorContext);
        return null;
    }

    private static void HideLoadingOverlay(CosmeticSelectionFlyoutPanelUI panel)
    {
        if (panel.IsNullOrDestroyed() || panel._loadingOverlay.IsNullOrDestroyed())
        {
            return;
        }
        panel._loadingOverlay.SetActive(false);
    }
}
