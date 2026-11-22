using HarmonyLib;

namespace LastEpoch_Hud.Scripts.Mods.Fixs;

public class Fix_Filter
{
    //Fix Exception when filter isn't set
    [HarmonyPatch(typeof(Il2CppItemFiltering.ItemFilterManager), "LoadFilter")]
    public class Il2CppItemFiltering_ItemFilterManager_LoadFilter
    {
        [HarmonyPrefix]
        static bool Prefix(
            Il2CppItemFiltering.ItemFilterManager __instance,
            bool __result,
            string __0
        )
        {
            if (__0 != "")
            {
                return true;
            }
            else
            {
                Main.logger_instance?.Warning(
                    "Fix : ItemFilterManager.LoadFilter(); Filter name is null, Don't load"
                );
                return false;
            }
        }
    }
}
