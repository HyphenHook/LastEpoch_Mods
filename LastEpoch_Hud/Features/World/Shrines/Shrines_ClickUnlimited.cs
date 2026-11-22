using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Shrines;

public class Shrines_ClickUnlimited
{
    public static bool CanRun()
    {
        if (!Save_Manager.instance.IsNullOrDestroyed())
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                return Save_Manager.instance.data.modsNotInHud.Shrines_Unlimited;
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

    [HarmonyPatch(typeof(WorldObjectClickListener), "ObjectClick")]
    public class WorldObjectClickListener_ObjectClick
    {
        [HarmonyPostfix]
        static void Postfix(
            ref WorldObjectClickListener __instance,
            UnityEngine.GameObject __0,
            bool __1
        )
        {
            if (CanRun())
            {
                if ((__instance.gameObject.name.ToLower().Contains(" shrine")) && (__1 == true))
                {
                    GameObject copy = GameObject.Instantiate(__instance.gameObject);
                    Vector3 position = __instance.gameObject.transform.position;
                    Object.Destroy(__instance.gameObject);
                    ShrinesManager shrine_placement_manager =
                        GameObject.FindObjectOfType<ShrinesManager>();
                    if (!shrine_placement_manager.IsNullOrDestroyed())
                    {
                        shrine_placement_manager.PlaceNewShrine(0, position);
                    }
                    else
                    {
                        Main.logger_instance?.Error("ShrinesManager not Found");
                    }
                }
            }
        }
    }
}
