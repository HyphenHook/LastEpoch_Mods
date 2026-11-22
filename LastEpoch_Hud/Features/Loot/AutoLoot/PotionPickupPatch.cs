using System;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Refills the health potion instead of dropping a potion.</summary>
[HarmonyPatch(typeof(GroundItemManager), "dropPotionForPlayer")]
internal class PotionPickupPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (!AutoPickupGate.IsOn(AutoPickupToggle.Potions))
        {
            return true;
        }

        try
        {
            return PickUp();
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoPickup potion");
            return true;
        }
    }

    private static bool PickUp()
    {
        if (Refs_Manager.health_potion.IsNullOrDestroyed())
        {
            return true;
        }

        if (
            !AutoPickupRules.PotionHasRoom(
                Refs_Manager.health_potion.currentCharges,
                Refs_Manager.health_potion.maxCharges
            )
        )
        {
            return true;
        }

        Refs_Manager.health_potion.currentCharges++;
        return false;
    }
}
