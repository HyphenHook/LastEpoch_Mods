namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause;

/// <summary>Decides whether HH buff timers stand still: zone, arrival, cinematic, boss intro, cutscene or reward panel.</summary>
public static class HeadhunterPauseRule
{
    public static bool IsPaused(
        bool nonCombatZone,
        bool arrivalProtected,
        bool cinematicActive,
        bool bossIntroActive,
        bool cutscenePlaying,
        bool rewardMenuOpen
    )
    {
        return nonCombatZone
            || arrivalProtected
            || cinematicActive
            || bossIntroActive
            || cutscenePlaying
            || rewardMenuOpen;
    }
}
