using System;
using HarmonyLib;
using Il2CppLE.UI.Login.UnityUI;

namespace LastEpoch_Hud.Scripts.Mods.Login;

public class Login_OfflineOnly
{
    private static LandingZonePanel landingPanel;

    public static void Tick()
    {
        if (landingPanel.IsNullOrDestroyed())
            return;
        try
        {
            if (
                landingPanel.isActiveAndEnabled
                && !landingPanel.playOnlineButton.IsNullOrDestroyed()
            )
                landingPanel.playOnlineButton.gameObject.SetActive(false);
        }
        catch (Exception ex)
        {
            landingPanel = null;
            Main.logger_instance?.Warning("[Offline] Could not hide online button: " + ex.Message);
        }
    }

    [HarmonyPatch(typeof(LandingZonePanel), "OnOnEnable")]
    public class LandingReady
    {
        [HarmonyPostfix]
        static void Postfix(LandingZonePanel __instance) => landingPanel = __instance;
    }

    [HarmonyPatch(typeof(LandingZonePanel), "OnDisable")]
    public class LandingClosed
    {
        [HarmonyPostfix]
        static void Postfix(LandingZonePanel __instance)
        {
            if (landingPanel == __instance)
                landingPanel = null;
        }
    }

    [HarmonyPatch(typeof(LandingZonePanel), "OnPlayOnlineClicked")]
    public class OnlineClicked
    {
        [HarmonyPrefix]
        static bool Prefix() => false;
    }

    // Play Offline is deliberately unpatched. The player clicks it and the game
    // owns readiness, character loading and the complete native transition.
}
