using System;
using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Picks up a dropped XP tome at once.</summary>
[HarmonyPatch(typeof(GroundItemManager), "dropXPTomeForPlayer")]
internal class XpTomePickupPatch
{
    [HarmonyPostfix]
    private static void Postfix(GroundItemManager __instance, Actor __0)
    {
        if (!AutoPickupGate.IsOn(AutoPickupToggle.XpTome))
        {
            return;
        }

        try
        {
            PickUp(__instance, __0);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoPickup XP tome");
        }
    }

    private static void PickUp(GroundItemManager manager, Actor actor)
    {
        if (actor.IsNullOrDestroyed())
        {
            return;
        }

        uint id = manager.nextXpTomeId - 1;
        int index = TomeLookup.IndexOfId(manager.activeXPTomes, id);
        if (index < 0)
        {
            return;
        }

        manager.pickupXPTome(actor, id, manager.activeXPTomes[index]);
    }
}
