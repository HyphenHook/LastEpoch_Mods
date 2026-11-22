using System;
using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Picks up a dropped favor tome at once.</summary>
[HarmonyPatch(typeof(GroundItemManager), "dropFavorTomeForPlayer")]
internal class FavorTomePickupPatch
{
    [HarmonyPostfix]
    private static void Postfix(GroundItemManager __instance, Actor __0)
    {
        if (!AutoPickupGate.IsOn(AutoPickupToggle.FavorTome))
        {
            return;
        }

        try
        {
            PickUp(__instance, __0);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoPickup favor tome");
        }
    }

    private static void PickUp(GroundItemManager manager, Actor actor)
    {
        if (actor.IsNullOrDestroyed())
        {
            return;
        }

        uint id = manager.nextFavorTomeId - 1;
        int index = TomeLookup.IndexOfId(manager.activeFavorTomes, id);
        if (index < 0)
        {
            return;
        }

        manager.pickupFavorTome(actor, id, manager.activeFavorTomes[index]);
    }
}
