using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Bar;
using LastEpoch_Hud.Scripts.ModUI;
using UnityEngine.SceneManagement;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood;

/// <summary>Keeps the Mageblood flask buffs in line with the worn belt and the config.</summary>
internal static class MagebloodBuffs
{
    private static readonly RefreshGate _gate = new(1.0);
    private static readonly MagebloodAppliedState _state = new();

    public static bool Worn => _state.Worn;
    public static int ActiveFlasks => _state.ActiveFlasks;

    public static void MarkDirty()
    {
        _state.MarkDirty();
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
            ErrorLog.Report(ex, "MagebloodBuffs.Refresh");
        }
    }

    public static void OnSceneLoaded(string sceneName)
    {
        if (HeadhunterRunReset.IsCharacterExit(sceneName))
        {
            ClearAll();
            return;
        }

        MarkDirty();
    }

    private static void Refresh()
    {
        StatBuffs buffs = HeadhunterBuffSink.PlayerBuffs();
        if (buffs == null || !Scenes.IsGameScene())
        {
            _state.MarkDirty();
            MagebloodBar.Hide();
            return;
        }

        bool worn = IsWorn();
        SyncBuffs(buffs, worn);
        ShowBar();
    }

    private static void SyncBuffs(StatBuffs buffs, bool worn)
    {
        if (_state.RecordIfIdle(worn))
        {
            return;
        }

        MagebloodSyncReason reason = _state.NeedsSync(worn, AllLive(buffs));
        if (reason == MagebloodSyncReason.None)
        {
            return;
        }

        Sync(buffs, worn, reason);
    }

    private static void ShowBar()
    {
        try
        {
            MagebloodBar.Sync(
                MagebloodConfigLoader.Flasks,
                _state.ActiveFlasks,
                HeadhunterConfigLoader.Current.Bar
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Mageblood flask bar");
        }
    }

    private static bool AllLive(StatBuffs buffs)
    {
        foreach (string name in _state.Names)
        {
            if (!HeadhunterBuffSink.IsLive(buffs, name))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWorn()
    {
        return Refs_Manager.player_actor.itemContainersManager.hasUniqueEquipped(
            CustomUniqueSpecs.Mageblood.UniqueId
        );
    }

    private static int ReadSlots()
    {
        ItemContainersManager manager = Refs_Manager.player_actor.itemContainersManager;
        if (manager.equipment.IsNullOrDestroyed() || manager.equipment.belt.IsNullOrDestroyed())
        {
            return MagebloodFlaskSlots.Min;
        }
        if (!manager.equipment.belt.TryGetContent(out ItemContainerEntry entry))
        {
            return MagebloodFlaskSlots.Min;
        }

        return MagebloodSlots.TryRead(entry.data, out int slots) ? slots : MagebloodFlaskSlots.Min;
    }

    private static void Sync(StatBuffs buffs, bool worn, MagebloodSyncReason reason)
    {
        int slots = worn ? ReadSlots() : 0;
        IReadOnlyList<BuffAction> actions = MagebloodFlaskRule.Plan(
            MagebloodConfigLoader.Flasks,
            slots,
            _state.Names
        );
        HeadhunterBuffSink.Apply(buffs, actions);

        int active = MagebloodFlaskRule.ActiveCount(MagebloodConfigLoader.Flasks.Count, slots);
        _state.Record(actions, worn, active);
        LogSync(reason, active);
    }

    private static void LogSync(MagebloodSyncReason reason, int activeFlasks)
    {
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        string scene = SceneManager.GetActiveScene().name;
        Main.logger_instance?.Msg(MagebloodSyncLog.Format(reason, scene, activeFlasks));
    }

    private static void ClearAll()
    {
        StatBuffs buffs = HeadhunterBuffSink.PlayerBuffs();
        if (buffs != null)
        {
            HeadhunterBuffSink.Apply(buffs, MagebloodFlaskRule.Plan(null, 0, _state.Names));
        }

        _state.Clear();
        MagebloodBar.Hide();
    }
}
