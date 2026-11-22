using System;
using HarmonyLib;
using Il2CppLE.Networking.Cutscenes;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Probe;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Probe;

[HarmonyPatch(typeof(ServerSyncedPlayableDirector), "HandleLocalCutsceneStart")]
public class HeadhunterProbeCutscenePatch
{
    [HarmonyPostfix]
    private static void Postfix(ServerSyncedPlayableDirector __instance)
    {
        if (!HeadhunterProbe.IsOn)
        {
            return;
        }

        try
        {
            HeadhunterProbe.Write(
                HeadhunterProbeLog.Cutscene(
                    HeadhunterProbe.Scene,
                    __instance.cutsceneId,
                    (float)__instance.cutsceneTotalDuration
                )
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HH probe cutscene");
        }
    }
}
