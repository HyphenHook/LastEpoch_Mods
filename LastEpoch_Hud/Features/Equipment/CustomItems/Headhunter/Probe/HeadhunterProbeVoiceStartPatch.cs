using System;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Probe;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Probe;

/// <summary>Logs a played actor voice line.</summary>
[HarmonyPatch(typeof(VoiceLineHandlerBase), "TryPlayLineInternal")]
public class HeadhunterProbeVoiceStartPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        VoiceLineHandlerBase __instance,
        ActorVoiceLines.VoiceLine voiceLine,
        ActorVoiceLines.VoiceLineSet set
    )
    {
        if (!HeadhunterProbe.IsOn)
        {
            return;
        }

        try
        {
            string trigger = set == null ? null : set.Trigger.ToString();
            HeadhunterProbe.Write(
                HeadhunterProbeLog.VoiceStart(
                    HeadhunterProbe.Scene,
                    __instance.GetTargetName(),
                    trigger,
                    voiceLine == null ? 0 : voiceLine.priority,
                    voiceLine?.Text,
                    voiceLine?.TextKey,
                    __instance.isPlaying
                )
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HH probe voice start");
        }
    }
}
