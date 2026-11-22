using System;
using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Adds dropped gold straight to the gold tracker.</summary>
[HarmonyPatch(typeof(GroundItemManager), "dropGoldForPlayer")]
internal class GoldPickupPatch
{
    [HarmonyPrefix]
    private static bool Prefix(int __1)
    {
        if (!AutoPickupGate.IsOn(AutoPickupToggle.Gold))
        {
            return true;
        }

        try
        {
            return PickUp(__1);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoPickup gold");
            return true;
        }
    }

    private static bool PickUp(int gold)
    {
        if (Refs_Manager.player_gold_tracker.IsNullOrDestroyed())
        {
            return true;
        }

        Refs_Manager.player_gold_tracker.modifyGold(gold);
        return false;
    }
}
