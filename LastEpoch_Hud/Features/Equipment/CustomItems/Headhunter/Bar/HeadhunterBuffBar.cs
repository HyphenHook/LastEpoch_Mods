using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Kills;
using LastEpoch_Hud.Scripts.ModUI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;

/// <summary>Polls the live Headhunter buffs and feeds the bar view.</summary>
internal static class HeadhunterBuffBar
{
    private static readonly RefreshGate _gate = new(1.0);
    private static readonly HeadhunterBuffBarModel _model = new();
    private static readonly HeadhunterBarChangeTracker _changes = new();
    private static float[] _remaining = Array.Empty<float>();

    public static void MarkDirty()
    {
        _gate.MarkDirty();
    }

    public static void Tick(double now)
    {
        if (!_gate.ShouldRefresh(now))
        {
            return;
        }

        try
        {
            Refresh();
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter buff bar");
        }
    }

    private static void Refresh()
    {
        StatBuffs buffs = HeadhunterBuffSink.PlayerBuffs();
        if (!ShouldShow(buffs))
        {
            HeadhunterBuffBarView.Hide();
            HeadhunterModelScaler.Apply(0);
            HeadhunterAreaBuff.Sync(0);
            HeadhunterReach.Sync(0);
            HeadhunterDash.Sync(0);
            HeadhunterAura.Sync(0);
            LogIfChanged(Array.Empty<HeadhunterBarEntry>());
            return;
        }

        HeadhunterResolvedConfig config = HeadhunterConfigLoader.Resolved;
        HeadhunterTimerPause.ApplyPending();
        float[] remaining = RemainingFor(config.Stats.Count);
        HeadhunterBuffSink.FillRemaining(buffs, config.Stats, remaining);
        HeadhunterTimerPause.Freeze.ShowFrozen(remaining);
        IReadOnlyList<HeadhunterBarEntry> entries = _model.Build(
            config.Stats,
            remaining,
            HeadhunterConfigLoader.Stacks,
            config.DurationSeconds
        );
        HeadhunterBuffBarView.Show(
            entries,
            HeadhunterConfigLoader.Current.Bar,
            HeadhunterTimerPause.Freeze.IsHolding
        );
        HeadhunterModelScaler.Apply(entries.Count);
        HeadhunterAreaBuff.Sync(entries.Count);
        HeadhunterReach.Sync(entries.Count);
        HeadhunterDash.Sync(entries.Count);
        HeadhunterAura.Sync(entries.Count);
        LogIfChanged(entries);
    }

    private static void LogIfChanged(IReadOnlyList<HeadhunterBarEntry> entries)
    {
        if (!_changes.Update(entries) || !ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        Main.logger_instance?.Msg(
            HeadhunterBarLog.Format(
                entries,
                HeadhunterStatNames.EnumName,
                HeadhunterStatNames.TagEnumName
            )
        );
    }

    private static bool ShouldShow(StatBuffs buffs)
    {
        if (!Scenes.IsGameScene() || buffs == null)
        {
            return false;
        }

        if (HeadhunterConfigLoader.Resolved == null)
        {
            return false;
        }

        return HeadhunterKillSource.IsHeadhunterWorn();
    }

    private static float[] RemainingFor(int count)
    {
        if (_remaining.Length != count)
        {
            _remaining = new float[count];
        }

        return _remaining;
    }
}
