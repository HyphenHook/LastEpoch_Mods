using System;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Items;

namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

/// <summary>Clamps loaded rarity 5/6 drops to 4 so items with more than four affixes load.</summary>
[HarmonyPatch(typeof(GroundItemManager), "dropItemForPlayer")]
public class DropRarityClampPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemData __1)
    {
        if (!Scenes.IsGameScene())
        {
            return true;
        }

        try
        {
            Clamp(__1);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Drop rarity clamp");
        }

        return true;
    }

    private static void Clamp(ItemData item)
    {
        if (item.IsNullOrDestroyed())
        {
            return;
        }

        if (!DropRarityClamp.TryClamp(item.rarity, out byte clamped))
        {
            return;
        }

        item.rarity = clamped;
        item.RefreshIDAndValues();
    }
}
