using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.PanelSystem;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

[HarmonyPatch(typeof(Panel), "OnOpen")]
public class HeadhunterRewardMenuOpenPatch
{
    [HarmonyPostfix]
    private static void Postfix(Panel __instance)
    {
        if (!HeadhunterTimerPause.WatchesRewardMenus)
        {
            return;
        }

        try
        {
            HeadhunterTimerPause.OnRewardPanelOpen(
                __instance,
                __instance.GetIl2CppType().Name,
                Time.unscaledTime
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HH reward menu open");
        }
    }
}
