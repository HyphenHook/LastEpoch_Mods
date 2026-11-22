namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause;

/// <summary>One ended cutscene hold, for the log.</summary>
public readonly record struct HeadhunterCutsceneStop(
    HeadhunterCutscene Cutscene,
    double HeldSeconds,
    HeadhunterCutsceneEnd By
);
