using System;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Probe;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Probe;

/// <summary>Logs how long the voice line blocks the next one.</summary>
[HarmonyPatch(typeof(VoiceLineHandlerBase), "ResetPlayingAfterDelay")]
public class HeadhunterProbeVoiceLengthPatch
{
    [HarmonyPostfix]
    private static void Postfix(VoiceLineHandlerBase __instance, float delay)
    {
        if (!HeadhunterProbe.IsOn)
        {
            return;
        }

        try
        {
            HeadhunterProbe.Write(
                HeadhunterProbeLog.VoiceLength(
                    HeadhunterProbe.Scene,
                    __instance.GetTargetName(),
                    delay
                )
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HH probe voice length");
        }
    }
}
