namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause.RewardMenu;

/// <summary>One ended reward panel hold, for the log.</summary>
public readonly record struct HeadhunterRewardMenuStop(
    HeadhunterRewardMenu Menu,
    double HeldSeconds,
    HeadhunterRewardMenuEnd By
);
