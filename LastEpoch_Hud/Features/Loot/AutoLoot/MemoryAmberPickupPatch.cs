using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Picks up a freshly created memory amber for the local player at once.</summary>
[HarmonyPatch(
    typeof(PickupableObjectsManager),
    "CreatePickupableObjectForPlayer",
    new[]
    {
        typeof(PickupableObjectType),
        typeof(PickupableObjectSet),
        typeof(Vector3),
        typeof(uint),
    }
)]
internal class MemoryAmberPickupPatch
{
    private const long NotArmed = -1;

    [HarmonyPrefix]
    private static void Prefix(PickupableObjectType __0, PickupableObjectSet __1, out long __state)
    {
        __state = NotArmed;
        if (!AutoPickupGate.IsOn(AutoPickupToggle.MemoryAmber))
        {
            return;
        }

        try
        {
            __state = Arm(__0, __1);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoPickup memory amber");
        }
    }

    [HarmonyPostfix]
    private static void Postfix(
        PickupableObjectsManager __instance,
        PickupableObjectSet __1,
        long __state
    )
    {
        if (__state == NotArmed)
        {
            return;
        }

        try
        {
            PickUp(__instance, __1, (uint)__state);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoPickup memory amber");
        }
    }

    private static long Arm(PickupableObjectType type, PickupableObjectSet set)
    {
        if (type != PickupableObjectType.MemoryAmber || set.IsNullOrDestroyed())
        {
            return NotArmed;
        }

        if (Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            return NotArmed;
        }

        if (set.PlayerActor != Refs_Manager.player_actor)
        {
            return NotArmed;
        }

        return set.NextID;
    }

    private static void PickUp(PickupableObjectsManager manager, PickupableObjectSet set, uint id)
    {
        if (set.IsNullOrDestroyed() || set.NextID == id)
        {
            return;
        }

        if (set.pickupables.IsNullOrDestroyed())
        {
            return;
        }

        if (
            !set.pickupables.TryGetValue(
                PickupableObjectType.MemoryAmber,
                out Il2CppSystem.Collections.Generic.List<PickupableObject> objects
            )
        )
        {
            return;
        }

        // Indexing avoids invalidating an enumerator when PickupObject removes the item.
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            PickupableObject pickup = objects[i];
            if (pickup.Id != id)
            {
                continue;
            }

            manager.PickupObject(set, pickup, i);
            return;
        }
    }
}
