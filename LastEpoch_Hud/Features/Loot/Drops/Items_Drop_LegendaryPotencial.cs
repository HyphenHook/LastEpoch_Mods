using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Items;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

public class Items_Drop_LegendaryPotencial
{
    public static bool CanRun()
    {
        if ((Hud_Manager.IsPauseOpen()) && (ModUI.ForceDrop.ForceDropModel.Enabled))
        {
            return false;
        }
        else if ((Scenes.IsGameScene()) && (!Save_Manager.instance.IsNullOrDestroyed()))
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                return Save_Manager.instance.data.Items.Drop.Enable_LegendaryPotencial;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), "RollLegendaryPotential")]
    public class rollLegendaryPotential
    {
        [HarmonyPrefix]
        static bool Prefix(ref int __result)
        {
            if (!CanRun())
            {
                return true;
            }

            var drop = Save_Manager.instance.data.Items.Drop;
            __result = LegendaryPotentialRoll.Pick(
                drop.LegendaryPotencial_Min,
                drop.LegendaryPotencial_Max,
                Random.value
            );
            return false;
        }
    }
}
