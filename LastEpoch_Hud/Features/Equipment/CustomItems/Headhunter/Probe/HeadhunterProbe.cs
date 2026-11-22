using System;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Probe;
using LastEpoch_Hud.Scripts.ModUI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Probe;

/// <summary>Debug-only probe: logs game signals during cutscenes and boss intros.</summary>
internal static class HeadhunterProbe
{
    private static readonly HeadhunterProbeWatch _watch = new();

    public static bool IsOn => ModSettings.Debug.Enabled.Value;

    public static string Scene { get; private set; } = string.Empty;

    /// <summary>Remembers the scene for log lines and resets the watch.</summary>
    public static void OnActiveSceneChanged(string sceneName, double now)
    {
        Scene = sceneName;
        _watch.Reset(now);
    }

    /// <summary>Logs the flags when they change.</summary>
    public static void Tick(double now)
    {
        if (!IsOn)
        {
            return;
        }

        if (!_watch.IsPollDue(now))
        {
            return;
        }

        if (!TryReadFlags(out HeadhunterProbeFlags flags))
        {
            return;
        }

        if (!_watch.TryChange(flags, now, out double held))
        {
            return;
        }

        Write(HeadhunterProbeLog.Flags(Scene, flags, held));
    }

    /// <summary>Writes one probe line.</summary>
    public static void Write(string line)
    {
        Main.logger_instance?.Msg(line);
    }

    /// <summary>Reads the game's cinematic, input and subtitle flags.</summary>
    private static bool TryReadFlags(out HeadhunterProbeFlags flags)
    {
        flags = default;
        try
        {
            EpochInputManager input = EpochInputManager.instance;
            CinematicSubtitlePanel subtitles = CinematicSubtitlePanel.Instance;
            bool hasInput = !input.IsNullOrDestroyed();
            flags = new HeadhunterProbeFlags(
                hasInput && input.cinematicActive,
                hasInput && input.interuptableCinematicActive,
                hasInput && input.forceDisableInput,
                !subtitles.IsNullOrDestroyed() && subtitles.IsActive
            );
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HH probe flags");
            return false;
        }
    }
}
