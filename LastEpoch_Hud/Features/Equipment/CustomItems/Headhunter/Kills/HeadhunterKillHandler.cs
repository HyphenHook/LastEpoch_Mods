using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.ModUI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Kills;

/// <summary>Handles one Headhunter kill: live rows, mechanic, sink, debug line.</summary>
internal static class HeadhunterKillHandler
{
    private static readonly HashSet<int> _liveRows = new();

    public static void Handle(KillInfo kill)
    {
        RareModsMechanic mechanic = HeadhunterConfigLoader.Mechanic;
        StatBuffs buffs = HeadhunterBuffSink.PlayerBuffs();
        if (mechanic == null || buffs == null)
        {
            return;
        }

        HeadhunterTimerPause.ApplyPending();
        HeadhunterBuffSink.FillActive(buffs, HeadhunterConfigLoader.Resolved.Stats, _liveRows);
        IReadOnlyList<BuffAction> actions = mechanic.OnKill(kill, _liveRows);
        HeadhunterBuffSink.Apply(
            buffs,
            HeadhunterTimerPause.Freeze.Hold(HeadhunterConfigLoader.Resolved, actions)
        );
        HeadhunterBuffBar.MarkDirty();
        LogKill(kill, actions);
    }

    private static void LogKill(KillInfo kill, IReadOnlyList<BuffAction> actions)
    {
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        Main.logger_instance?.Msg(
            HeadhunterKillLog.Format(
                kill,
                actions,
                HeadhunterStatNames.EnumName,
                HeadhunterStatNames.TagEnumName
            )
        );
    }
}
