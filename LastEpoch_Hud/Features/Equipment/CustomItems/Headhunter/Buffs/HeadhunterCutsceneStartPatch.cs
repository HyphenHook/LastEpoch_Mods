using System;
using HarmonyLib;
using Il2CppLE.Networking.Cutscenes;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

[HarmonyPatch(typeof(ServerSyncedPlayableDirector), "HandleLocalCutsceneStart")]
public class HeadhunterCutsceneStartPatch
{
    [HarmonyPostfix]
    private static void Postfix(ServerSyncedPlayableDirector __instance)
    {
        try
        {
            HeadhunterTimerPause.OnCutsceneStart(
                __instance.playableDirector,
                __instance.cutsceneId,
                __instance.cutsceneTotalDuration,
                Time.unscaledTime
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HH cutscene start");
        }
    }
}
