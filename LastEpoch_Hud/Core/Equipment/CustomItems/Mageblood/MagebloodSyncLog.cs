namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

/// <summary>Builds the Debug log lines written by the Mageblood sync.</summary>
public static class MagebloodSyncLog
{
    public static string Format(MagebloodSyncReason reason, string scene, int activeFlasks)
    {
        return $"Mageblood sync: reason={reason} scene={scene ?? string.Empty} n={activeFlasks}";
    }
}
