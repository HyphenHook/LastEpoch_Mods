using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppLE.UI.PanelSystem;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause.RewardMenu;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.ModUI;
using UnityEngine;
using UnityEngine.Playables;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

/// <summary>Applies the zone, arrival, cinematic, boss intro, cutscene and reward panel pause to the player's HH buffs.</summary>
internal static class HeadhunterTimerPause
{
    private static float[] _live = Array.Empty<float>();

    private static readonly HeadhunterZonePause _zone = new();

    private static PlayableDirector _cutsceneDirector;
    private static Panel _rewardPanel;

    public static HeadhunterTimerFreeze Freeze => _zone.Freeze;

    public static bool HasBossIntro => _zone.HasBossIntro;

    public static bool WatchesRewardMenus => _zone.WatchesRewardMenus;

    public static bool HasRewardMenu => _zone.HasRewardMenu;

    /// <summary>Re-evaluates the zone pause for the newly active scene.</summary>
    public static void OnActiveSceneChanged(string sceneName, double now)
    {
        DropCutscene(now);
        DropRewardMenu(now);
        bool nonCombat = IsNonCombat(sceneName);
        HeadhunterPauseChange change = _zone.OnScene(sceneName, nonCombat, now);
        if (ModSettings.Debug.Enabled.Value)
        {
            Main.logger_instance?.Msg(HeadhunterPauseLog.Zone(sceneName, nonCombat, change));
        }

        ApplyPending();
        HeadhunterBuffBar.MarkDirty();
    }

    /// <summary>Polls arrival and cinematic state in combat zones.</summary>
    public static void Tick(double now)
    {
        if (!_zone.IsPollDue(now))
        {
            return;
        }

        PollArrival(now);
        PollCinematic(now);
        PollBossIntroExpiry(now);
        PollCutscene(now);
        PollRewardMenu(now);
    }

    /// <summary>Freezes timers while a long boss intro plays.</summary>
    public static void OnBossIntroStart(long id, string actor, float durationSeconds, double now)
    {
        if (!_zone.TryStartBossIntro(id, actor, durationSeconds, now))
        {
            return;
        }

        ApplyPending();
        HeadhunterBuffBar.MarkDirty();
        if (ModSettings.Debug.Enabled.Value)
        {
            HeadhunterBossIntro intro = new(id, actor, durationSeconds, now);
            Main.logger_instance?.Msg(HeadhunterPauseLog.BossIntroStarted(_zone.Scene, intro));
        }
    }

    /// <summary>Resumes timers when a tracked boss intro ends.</summary>
    public static void OnBossIntroEnd(long id, double now)
    {
        if (!_zone.TryEndBossIntro(id, now, out HeadhunterBossIntro intro, out double held))
        {
            return;
        }

        ApplyPending();
        HeadhunterBuffBar.MarkDirty();
        if (ModSettings.Debug.Enabled.Value)
        {
            Main.logger_instance?.Msg(HeadhunterPauseLog.BossIntroEnded(_zone.Scene, intro, held));
        }
    }

    /// <summary>Freezes timers while a cutscene plays in a combat zone.</summary>
    public static void OnCutsceneStart(
        PlayableDirector director,
        string id,
        double durationSeconds,
        double now
    )
    {
        if (!_zone.TryStartCutscene(id, durationSeconds, now))
        {
            return;
        }

        _cutsceneDirector = director;
        ApplyPending();
        HeadhunterBuffBar.MarkDirty();
        if (ModSettings.Debug.Enabled.Value)
        {
            HeadhunterCutscene cutscene = new(id, durationSeconds, now);
            Main.logger_instance?.Msg(HeadhunterPauseLog.CutsceneStarted(_zone.Scene, cutscene));
        }
    }

    /// <summary>Freezes timers while a reward-choice panel is open in a combat zone.</summary>
    public static void OnRewardPanelOpen(Panel panel, string typeName, double now)
    {
        long id = panel.Pointer.ToInt64();
        if (!_zone.TryStartRewardMenu(id, typeName, now))
        {
            return;
        }

        _rewardPanel = panel;
        ApplyPending();
        HeadhunterBuffBar.MarkDirty();
        if (ModSettings.Debug.Enabled.Value)
        {
            HeadhunterRewardMenu menu = new(id, typeName, now);
            Main.logger_instance?.Msg(HeadhunterPauseLog.RewardMenuStarted(_zone.Scene, menu));
        }
    }

    /// <summary>Resumes timers when the game closes the held reward panel.</summary>
    public static void OnRewardPanelClose(long id, double now)
    {
        if (_zone.TryCloseRewardMenu(id, now, out HeadhunterRewardMenuStop stop))
        {
            ReleaseRewardMenu(stop);
        }
    }

    /// <summary>Drops the arrival watch and kept timers after HH buffs were removed.</summary>
    public static void Clear()
    {
        _cutsceneDirector = null;
        _rewardPanel = null;
        _zone.Clear();
    }

    public static void ApplyPending()
    {
        if (!Freeze.IsApplyPending)
        {
            return;
        }

        TryApply();
    }

    /// <summary>Resumes timers once arrival protection ends.</summary>
    private static void PollArrival(double now)
    {
        if (!_zone.IsWatchingArrival)
        {
            return;
        }

        HeadhunterArrivalState state = ReadArrivalState();
        if (!_zone.TryEndArrival(state, now, out double held))
        {
            return;
        }

        ApplyPending();
        HeadhunterBuffBar.MarkDirty();
        if (ModSettings.Debug.Enabled.Value)
        {
            Main.logger_instance?.Msg(HeadhunterPauseLog.Arrival(_zone.Scene, held, state));
        }
    }

    /// <summary>Freezes or resumes timers when a cinematic starts or ends.</summary>
    private static void PollCinematic(double now)
    {
        bool active = ReadCinematic();
        if (!_zone.TryCinematic(active, now, out double held))
        {
            return;
        }

        ApplyPending();
        HeadhunterBuffBar.MarkDirty();
        if (ModSettings.Debug.Enabled.Value)
        {
            Main.logger_instance?.Msg(HeadhunterPauseLog.Cinematic(_zone.Scene, active, held));
        }
    }

    /// <summary>Releases timers held by intros whose end was never seen.</summary>
    private static void PollBossIntroExpiry(double now)
    {
        while (_zone.TryExpireBossIntro(now, out HeadhunterBossIntro intro, out double held))
        {
            ApplyPending();
            HeadhunterBuffBar.MarkDirty();
            if (ModSettings.Debug.Enabled.Value)
            {
                Main.logger_instance?.Msg(
                    HeadhunterPauseLog.BossIntroExpired(_zone.Scene, intro, held)
                );
            }
        }
    }

    /// <summary>Resumes timers when the held cutscene stops, vanishes or runs out.</summary>
    private static void PollCutscene(double now)
    {
        if (!_zone.HasCutscene)
        {
            return;
        }

        if (!_zone.TryEndCutscene(ReadCutsceneState(), now, out HeadhunterCutsceneStop stop))
        {
            return;
        }

        _cutsceneDirector = null;
        ApplyPending();
        HeadhunterBuffBar.MarkDirty();
        if (ModSettings.Debug.Enabled.Value)
        {
            Main.logger_instance?.Msg(HeadhunterPauseLog.CutsceneEnded(_zone.Scene, stop));
        }
    }

    /// <summary>Resumes timers when the held reward panel is destroyed, hidden or past the cap.</summary>
    private static void PollRewardMenu(double now)
    {
        if (!_zone.HasRewardMenu)
        {
            return;
        }

        if (_zone.TryEndRewardMenu(ReadRewardMenuState(), now, out HeadhunterRewardMenuStop stop))
        {
            ReleaseRewardMenu(stop);
        }
    }

    private static void ReleaseRewardMenu(HeadhunterRewardMenuStop stop)
    {
        _rewardPanel = null;
        ApplyPending();
        HeadhunterBuffBar.MarkDirty();
        if (ModSettings.Debug.Enabled.Value)
        {
            Main.logger_instance?.Msg(HeadhunterPauseLog.RewardMenuEnded(_zone.Scene, stop));
        }
    }

    /// <summary>Logs a reward panel still held at zone change; the zone reset clears the hold itself.</summary>
    private static void DropRewardMenu(double now)
    {
        _rewardPanel = null;
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        if (
            _zone.TryPeekRewardMenu(
                now,
                HeadhunterRewardMenuEnd.Scene,
                out HeadhunterRewardMenuStop stop
            )
        )
        {
            Main.logger_instance?.Msg(HeadhunterPauseLog.RewardMenuEnded(_zone.Scene, stop));
        }
    }

    /// <summary>Reads the held panel: destroyed, inactive or open.</summary>
    private static HeadhunterRewardMenuState ReadRewardMenuState()
    {
        try
        {
            if (_rewardPanel.IsNullOrDestroyed())
            {
                return HeadhunterRewardMenuState.Missing;
            }

            return _rewardPanel.isActiveAndEnabled
                ? HeadhunterRewardMenuState.Open
                : HeadhunterRewardMenuState.Hidden;
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter reward menu check");
            return HeadhunterRewardMenuState.Missing;
        }
    }

    /// <summary>Logs a cutscene still held at zone change; the zone reset clears the hold itself.</summary>
    private static void DropCutscene(double now)
    {
        _cutsceneDirector = null;
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        if (
            _zone.TryPeekCutscene(now, HeadhunterCutsceneEnd.Scene, out HeadhunterCutsceneStop stop)
        )
        {
            Main.logger_instance?.Msg(HeadhunterPauseLog.CutsceneEnded(_zone.Scene, stop));
        }
    }

    /// <summary>Reads the held director: gone, stopped (Unity reports Paused) or playing.</summary>
    private static HeadhunterCutsceneState ReadCutsceneState()
    {
        try
        {
            if (_cutsceneDirector.IsNullOrDestroyed())
            {
                return HeadhunterCutsceneState.Missing;
            }

            return _cutsceneDirector.state == PlayState.Paused
                ? HeadhunterCutsceneState.Stopped
                : HeadhunterCutsceneState.Playing;
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter cutscene check");
            return HeadhunterCutsceneState.Missing;
        }
    }

    /// <summary>Reads the game's cinematic flag; a missing input manager reads as no cinematic.</summary>
    private static bool ReadCinematic()
    {
        try
        {
            EpochInputManager input = Refs_Manager.epoch_input_manager;
            return !input.IsNullOrDestroyed() && input.cinematicActive;
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter cinematic check");
            return false;
        }
    }

    /// <summary>Reads the game's arrival protection.</summary>
    private static HeadhunterArrivalState ReadArrivalState()
    {
        try
        {
            PlayerZoneTransitionHandler handler = Refs_Manager.player_health.IsNullOrDestroyed()
                ? null
                : Refs_Manager.player_health.playerZoneTransition;
            if (handler.IsNullOrDestroyed())
            {
                return HeadhunterArrivalState.Missing;
            }

            return handler.damageable()
                ? HeadhunterArrivalState.Damageable
                : HeadhunterArrivalState.Protected;
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter arrival check");
            return HeadhunterArrivalState.Missing;
        }
    }

    private static bool IsNonCombat(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return false;
        }

        if (SceneList.instance.IsNullOrDestroyed())
        {
            return false;
        }

        try
        {
            return SceneList.IsNonCombatZone(sceneName);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter zone check");
            return false;
        }
    }

    private static void TryApply()
    {
        StatBuffs buffs = HeadhunterBuffSink.PlayerBuffs();
        HeadhunterResolvedConfig config = HeadhunterConfigLoader.Resolved;
        if (buffs == null || config == null)
        {
            return;
        }

        try
        {
            ApplyTo(buffs, config);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter timer pause");
        }
    }

    private static void ApplyTo(StatBuffs buffs, HeadhunterResolvedConfig config)
    {
        if (_live.Length != config.Stats.Count)
        {
            _live = new float[config.Stats.Count];
        }

        HeadhunterBuffSink.FillRemaining(buffs, config.Stats, _live);
        IReadOnlyList<BuffAction> actions = Freeze.Apply(config, _live);
        HeadhunterBuffSink.Apply(buffs, actions);
        HeadhunterBuffBar.MarkDirty();
        if (ModSettings.Debug.Enabled.Value)
        {
            Main.logger_instance?.Msg(HeadhunterPauseLog.Applied(Freeze.IsHolding, actions.Count));
        }
    }
}
