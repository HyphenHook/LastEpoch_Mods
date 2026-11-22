using HarmonyLib;

namespace LastEpoch_Hud.Scripts.Mods.Fixs;

public class Fix_PlayerLoopHelper
{
    //Fix exception when player isn't set
    [HarmonyPatch(typeof(Il2CppCysharp.Threading.Tasks.PlayerLoopHelper), "AddAction")]
    public class Il2CppCysharp_Threading_Tasks_PlayerLoopHelper_AddAction
    {
        [HarmonyPrefix]
        static bool Prefix()
        {
            // Always let the game schedule UniTasks. Skipping AddAction before the HUD
            // exists drops one-shot splash tasks and the client never leaves ClientSplash.
            return true;
        }
    }
}
