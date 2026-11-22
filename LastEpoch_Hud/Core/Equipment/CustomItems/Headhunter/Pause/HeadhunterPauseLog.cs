using System.Globalization;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause.RewardMenu;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause;

/// <summary>Debug lines for the zone pause.</summary>
public static class HeadhunterPauseLog
{
    public static string Zone(string scene, bool nonCombat, HeadhunterPauseChange change)
    {
        string zone = nonCombat ? "yes" : "no";
        return $"Headhunter zone: scene={scene} nonCombat={zone} timers={Timers(change)}";
    }

    public static string Applied(bool holding, int buffCount)
    {
        string state = holding ? "held" : "released";
        return $"Headhunter timers {state}: {buffCount}";
    }

    public static string Arrival(string scene, double heldSeconds, HeadhunterArrivalState state)
    {
        return $"Headhunter arrival ended: scene={scene} held={Seconds(heldSeconds)}s by={Cause(state)}";
    }

    public static string Cinematic(string scene, bool active, double heldSeconds)
    {
        if (active)
        {
            return $"Headhunter cinematic started: scene={scene}";
        }

        return $"Headhunter cinematic ended: scene={scene} held={Seconds(heldSeconds)}s";
    }

    public static string BossIntroStarted(string scene, HeadhunterBossIntro intro)
    {
        return $"Headhunter boss intro started: scene={scene} actor={intro.Actor} duration={Seconds(intro.DurationSeconds)}s";
    }

    public static string BossIntroEnded(string scene, HeadhunterBossIntro intro, double heldSeconds)
    {
        return $"Headhunter boss intro ended: scene={scene} actor={intro.Actor} duration={Seconds(intro.DurationSeconds)}s held={Seconds(heldSeconds)}s";
    }

    public static string BossIntroExpired(
        string scene,
        HeadhunterBossIntro intro,
        double heldSeconds
    )
    {
        return $"Headhunter boss intro expired: scene={scene} actor={intro.Actor} duration={Seconds(intro.DurationSeconds)}s held={Seconds(heldSeconds)}s";
    }

    public static string CutsceneStarted(string scene, HeadhunterCutscene cutscene)
    {
        return $"Headhunter cutscene started: scene={scene} id={cutscene.Id} duration={Seconds(cutscene.DurationSeconds)}s";
    }

    public static string CutsceneEnded(string scene, HeadhunterCutsceneStop stop)
    {
        return $"Headhunter cutscene ended: scene={scene} id={stop.Cutscene.Id} duration={Seconds(stop.Cutscene.DurationSeconds)}s held={Seconds(stop.HeldSeconds)}s by={CutsceneCause(stop.By)}";
    }

    public static string RewardMenuStarted(string scene, HeadhunterRewardMenu menu)
    {
        return $"Headhunter reward menu opened: scene={scene} panel={menu.Panel}";
    }

    public static string RewardMenuEnded(string scene, HeadhunterRewardMenuStop stop)
    {
        return $"Headhunter reward menu closed: scene={scene} panel={stop.Menu.Panel} held={Seconds(stop.HeldSeconds)}s by={RewardMenuCause(stop.By)}";
    }

    private static string Seconds(double seconds)
    {
        return seconds.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>Log word for how the watch ended.</summary>
    private static string Cause(HeadhunterArrivalState state)
    {
        return state == HeadhunterArrivalState.Damageable ? "damageable" : "missing";
    }

    private static string CutsceneCause(HeadhunterCutsceneEnd by)
    {
        return by switch
        {
            HeadhunterCutsceneEnd.Duration => "duration",
            HeadhunterCutsceneEnd.Stopped => "stopped",
            HeadhunterCutsceneEnd.Missing => "missing",
            _ => "scene",
        };
    }

    private static string RewardMenuCause(HeadhunterRewardMenuEnd by)
    {
        return by switch
        {
            HeadhunterRewardMenuEnd.Closed => "closed",
            HeadhunterRewardMenuEnd.Hidden => "hidden",
            HeadhunterRewardMenuEnd.Missing => "missing",
            HeadhunterRewardMenuEnd.Cap => "cap",
            _ => "scene",
        };
    }

    private static string Timers(HeadhunterPauseChange change)
    {
        return change switch
        {
            HeadhunterPauseChange.Paused => "paused",
            HeadhunterPauseChange.Resumed => "resumed",
            _ => "unchanged",
        };
    }
}
