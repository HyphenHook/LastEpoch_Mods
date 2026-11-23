using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

public class Items_Drop_UniqueMods
{
    public static bool CanRun()
    {
        if ((Scenes.IsGameScene()) && (!Save_Manager.instance.IsNullOrDestroyed()))
        {
            if ((Hud_Manager.IsPauseOpen()) && (ModUI.ForceDrop.ForceDropModel.Enabled))
            {
                return false;
            }
            else if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                return Save_Manager.instance.data.Items.Drop.Enable_UniqueMods;
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

    [HarmonyPatch(typeof(ItemData), "randomiseUniqueRolls")]
    public class randomiseUniqueRolls
    {
        [HarmonyPrefix]
        static void Prefix(ItemData __instance, out byte[] __state) =>
            __state = LastEpoch_Hud.Scripts.Mods.Items.Jewels.HeorotCore.RememberRoll(__instance);

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        static void Postfix(ItemData __instance, byte[] __state)
        {
            // Heorot's Core keeps the roll that says which buffs it gives, whatever just randomised the item.
            LastEpoch_Hud.Scripts.Mods.Items.Jewels.HeorotCore.RestoreRoll(__instance, __state);
            if (__state == null && CanRun() && !__instance.IsNullOrDestroyed())
            {
                for (int k = 0; k < __instance.uniqueRolls.Count; k++)
                {
                    byte roll = 0;
                    if (
                        Save_Manager.instance.data.Items.Drop.UniqueMods_Min
                        == Save_Manager.instance.data.Items.Drop.UniqueMods_Max
                    )
                    {
                        roll = (byte)Save_Manager.instance.data.Items.Drop.UniqueMods_Max;
                    }
                    else
                    {
                        roll = (byte)
                            Random.RandomRange(
                                Save_Manager.instance.data.Items.Drop.UniqueMods_Min,
                                Save_Manager.instance.data.Items.Drop.UniqueMods_Max
                            );
                    }
                    __instance.uniqueRolls[k] = roll;
                }
                // Only change the rolls here. Ascendance can call this before it has
                // finished assigning the unique identity; rebuilding or refreshing now
                // can serialize that intermediate base item. The caller owns finalization.
            }
        }
    }
}
