using System;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Probe;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Probe;

[HarmonyPatch(typeof(StartCinematicInteraction), "Interaction")]
public class HeadhunterProbeCinematicPatch
{
    [HarmonyPostfix]
    private static void Postfix(StartCinematicInteraction __instance)
    {
        if (!HeadhunterProbe.IsOn)
        {
            return;
        }

        try
        {
            HeadhunterProbe.Write(
                HeadhunterProbeLog.Cinematic(
                    HeadhunterProbe.Scene,
                    __instance.name,
                    __instance.duration,
                    __instance.interuptable,
                    __instance.noMovementLock,
                    __instance.noPlayerInvincibility
                )
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HH probe cinematic");
        }
    }
}
