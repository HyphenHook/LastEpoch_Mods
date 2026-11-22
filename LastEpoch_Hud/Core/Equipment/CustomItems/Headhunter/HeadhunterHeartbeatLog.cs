using System.Globalization;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter;

/// <summary>Debug heartbeat line.</summary>
public static class HeadhunterHeartbeatLog
{
    public static string Format(int frame, float timeScale, string scene)
    {
        return "HH heartbeat frame="
            + frame
            + " timeScale="
            + timeScale.ToString(CultureInfo.InvariantCulture)
            + " scene="
            + scene;
    }
}
