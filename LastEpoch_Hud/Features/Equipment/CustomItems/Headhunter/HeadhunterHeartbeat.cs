using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter;
using LastEpoch_Hud.Scripts.ModUI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter;

/// <summary>Logs a Debug heartbeat every 10 s so a hang shows as missing lines.</summary>
internal static class HeadhunterHeartbeat
{
    private static readonly IntervalGate _gate = new(10.0);

    public static void Tick(double now)
    {
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        if (!_gate.IsDue(now))
        {
            return;
        }

        Main.logger_instance?.Msg(
            HeadhunterHeartbeatLog.Format(
                Time.frameCount,
                Time.timeScale,
                SceneManager.GetActiveScene().name
            )
        );
    }
}
