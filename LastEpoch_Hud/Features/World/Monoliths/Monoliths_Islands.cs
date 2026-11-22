using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Monoliths;

public class Monoliths_Islands
{
    public static bool CanRun()
    {
        bool r = false;
        if ((Scenes.IsGameScene()) && (!Save_Manager.instance.IsNullOrDestroyed()))
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                r = Save_Manager.instance.data.Scenes.Monoliths.Enable_Islands;
            }
        }
        return r;
    }

    [HarmonyPatch(typeof(EchoWeb), "islandCanBeRun")]
    public class EchoWeb_islandCanBeRun
    {
        [HarmonyPostfix]
        static void Postfix(EchoWeb __instance, ref bool __result, EchoWebIsland __0)
        {
            if ((Scenes.IsGameScene()) && (CanRun()))
            {
                __result = true;
            }
        }
    }
}
