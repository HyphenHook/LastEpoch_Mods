using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Ladder;

public class Ladder_Remove
{
    [HarmonyPatch(typeof(UIBase), "LadderKeyDown")]
    public class UIBase_LadderKeyDown
    {
        [HarmonyPrefix]
        static bool Prefix()
        {
            return false;
        }
    }
}
