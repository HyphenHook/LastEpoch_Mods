using System;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Probe;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Kills;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Probe;

/// <summary>Logs boss/miniboss death delays.</summary>
[HarmonyPatch(typeof(Dying), "onEnter")]
public class HeadhunterProbeDeathPatch
{
    [HarmonyPostfix]
    private static void Postfix(Dying __instance)
    {
        if (!HeadhunterProbe.IsOn)
        {
            return;
        }

        try
        {
            Actor actor = __instance.getActor();
            if (!HeadhunterBossKind.TryGet(actor, out KillKind kind))
            {
                return;
            }

            HeadhunterProbe.Write(
                HeadhunterProbeLog.DeathStart(
                    HeadhunterProbe.Scene,
                    actor.name,
                    kind,
                    __instance.sinkingDelay,
                    __instance.destructionDelay
                )
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HH probe death start");
        }
    }
}
