using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.PanelSystem;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

[HarmonyPatch(typeof(PanelSystem), "ClosePanelInternal")]
public class HeadhunterRewardMenuClosePatch
{
    [HarmonyPostfix]
    private static void Postfix(Panel panel)
    {
        if (!HeadhunterTimerPause.HasRewardMenu || panel.IsNullOrDestroyed())
        {
            return;
        }

        try
        {
            HeadhunterTimerPause.OnRewardPanelClose(panel.Pointer.ToInt64(), Time.unscaledTime);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HH reward menu close");
        }
    }
}
