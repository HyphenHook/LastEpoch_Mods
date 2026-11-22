using System;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Probe;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Probe;

/// <summary>Logs an over-head bark and its display time.</summary>
[HarmonyPatch(typeof(FloatingBark), "Bark", new[] { typeof(string), typeof(string) })]
public class HeadhunterProbeBarkPatch
{
    [HarmonyPostfix]
    private static void Postfix(FloatingBark __instance, string content, string npcName)
    {
        if (!HeadhunterProbe.IsOn)
        {
            return;
        }

        try
        {
            HeadhunterProbe.Write(
                HeadhunterProbeLog.Bark(
                    HeadhunterProbe.Scene,
                    npcName,
                    content,
                    __instance.DisplayInSeconds
                )
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HH probe bark");
        }
    }
}
