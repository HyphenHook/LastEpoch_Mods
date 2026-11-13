using System;
using HarmonyLib;
using Il2CppEHG.Multiplayer;
using Il2CppLE.UI.PanelSystem;

namespace LastEpoch_Hud.Scripts.Mods.Economy;

/// <summary>
/// Offline: BazaarPanel.OnOpenWithArgs refuses ("Stalls are not available in offline mode") and closes the
/// panel before it configures itself. Offline, skip that decision and configure the stall directly, which
/// is what the game does when a stall is allowed. Online play is untouched.
/// </summary>
[HarmonyPatch(typeof(BazaarPanel), nameof(BazaarPanel.OnOpenWithArgs))]
public class BazaarPanelOpenOfflinePatch
{
    private static int _logged;

    /// <summary>The open Bazaar panel, so list/unlist and the refresh can reach its containers and UI.</summary>
    public static BazaarPanel Panel;

    [HarmonyPrefix]
    private static bool Prefix(BazaarPanel __instance, BazaarPanelOpenData data)
    {
        if (GameplayEnvironment.IsOnlinePlay || __instance.IsNullOrDestroyed() || data.IsNullOrDestroyed())
        {
            return true;
        }
        try
        {
            if (__instance.BazaarUI.IsNullOrDestroyed())
            {
                return true;
            }
            // The game's own open call hands the UI your gold tracker; the listing rows read it to colour
            // unaffordable prices, and throw when it is null.
            if (__instance.BazaarUI.GoldTracker.IsNullOrDestroyed())
            {
                __instance.BazaarUI.GoldTracker = Scripts.Refs_Manager.player_gold_tracker;
            }
            Panel = __instance;
            OfflineBazaarMarket.BazaarOpened();
            __instance.BazaarUI.Configure(data.StallType);
            // The panel only loads listings when you press Search; load the first page on open instead.
            BazaarRefresh.LastPage = 1;
            BazaarRefresh.Request();
            if (_logged++ < 3)
            {
                Main.logger_instance?.Msg("Bazaar: opened stall offline (stall type " + data.StallType + ").");
            }
            return false;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Bazaar offline open failed, using the game's own path: " + ex.Message);
            return true;
        }
    }
}

/// <summary>The Bazaar screen closing: release the hold on the rest of the game.</summary>
[HarmonyPatch(typeof(BazaarPanel), nameof(BazaarPanel.OnClose))]
public class BazaarPanelClosePatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        try
        {
            OfflineBazaarMarket.BazaarClosed();
        }
        catch (Exception) { }
    }
}

/// <summary>Remembers the stall NPC you clicked (a plain enum, unlike the Nullable on the open data).</summary>
[HarmonyPatch(typeof(Il2Cpp.NpcStallTooltip), nameof(Il2Cpp.NpcStallTooltip.Interaction))]
public class StallNpcClickedPatch
{
    public static int LastStall = -1;

    [HarmonyPrefix]
    private static void Prefix(Il2Cpp.NpcStallTooltip __instance)
    {
        try
        {
            if (!__instance.IsNullOrDestroyed())
            {
                LastStall = (int)__instance.StallType;
                OfflineBazaarMarket.Crumb("clicked stall NPC: stall type " + LastStall + " (" + __instance.StallType + ")");
            }
        }
        catch (Exception) { }
    }
}
