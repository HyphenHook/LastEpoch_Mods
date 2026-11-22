using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

[HarmonyPatch(typeof(Emerging), "onExit")]
public class HeadhunterBossIntroEndPatch
{
    [HarmonyPrefix]
    private static void Prefix(Emerging __instance)
    {
        if (!HeadhunterTimerPause.HasBossIntro)
        {
            return;
        }

        try
        {
            HeadhunterTimerPause.OnBossIntroEnd(__instance.Pointer.ToInt64(), Time.unscaledTime);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HH boss intro end");
        }
    }
}
