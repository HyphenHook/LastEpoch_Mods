using System;
using HarmonyLib;
using Il2CppEHG.Multiplayer;
using Il2CppLE.Services.Bazaar;
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
            int clickedStall = StallNpcClickedPatch.PendingStall;
            StallNpcClickedPatch.PendingStall = -1;
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
            // Boxing a native Nullable<T> can produce a boxed T. Its generated backing-field
            // accessors then read unrelated memory. Build the parameter through its native
            // constructor from the NPC's plain enum, never through nullable backing fields.
            bool hasStall = clickedStall >= 0 && Enum.IsDefined(typeof(BazaarStallType), clickedStall);
            var stallType = hasStall
                ? new Il2CppSystem.Nullable<BazaarStallType>((BazaarStallType)clickedStall)
                : new Il2CppSystem.Nullable<BazaarStallType>();
            var ui = __instance.BazaarUI;
            ui._initialStallType = stallType;
            ui.ConfigureInternal(stallType, skipAutoSubmitRequest: true, ignoreItemToList: true);
            // Do not restore saved subtype/unique/affix filters from another visit. Submit only once,
            // after both category configuration and the native filter reset are complete.
            ui.FilterUI.ResetUI(attemptLoadFilter: false, stallType: stallType, skipAutoSubmitRequest: true);
            BazaarRefresh.LastPage = 1;
            BazaarRefresh.Request();
            if (_logged++ < 3)
            {
                Main.logger_instance?.Msg("Bazaar: opened stall offline (stall type "
                    + (hasStall ? ((BazaarStallType)clickedStall).ToString() : "none") + ").");
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
    internal static int PendingStall = -1;

    [HarmonyPrefix]
    private static void Prefix(Il2Cpp.NpcStallTooltip __instance)
    {
        if (GameplayEnvironment.IsOnlinePlay)
        {
            return;
        }
        try
        {
            PendingStall = -1;
            if (!__instance.IsNullOrDestroyed())
            {
                LastStall = (int)__instance.StallType;
                PendingStall = LastStall;
                OfflineBazaarMarket.Crumb("clicked stall NPC: stall type " + LastStall + " (" + __instance.StallType + ")");
            }
        }
        catch (Exception) { }
    }
}
