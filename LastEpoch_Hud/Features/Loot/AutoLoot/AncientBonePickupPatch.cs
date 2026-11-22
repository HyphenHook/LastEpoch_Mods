using System;
using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Adds dropped ancient bones straight to the bones tracker.</summary>
[HarmonyPatch(typeof(GroundItemManager), "dropAncientBoneForPlayer")]
internal class AncientBonePickupPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Actor __0, int __1)
    {
        if (!AutoPickupGate.IsOn(AutoPickupToggle.Materials))
        {
            return true;
        }

        try
        {
            return PickUp(__0, __1);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoPickup ancient bones");
            return true;
        }
    }

    private static bool PickUp(Actor actor, int amount)
    {
        if (amount <= 0 || actor.IsNullOrDestroyed())
        {
            return true;
        }

        AncientBonesTracker tracker = FindTracker(actor);
        if (tracker.IsNullOrDestroyed())
        {
            return true;
        }

        return !tracker.modifyAncientBones(amount);
    }

    private static AncientBonesTracker FindTracker(Actor actor)
    {
        AncientBonesTracker tracker = actor.ancientBonesTracker;
        if (!tracker.IsNullOrDestroyed())
        {
            return tracker;
        }

        return PlayerFinder.getAncientBonesTracker();
    }
}
