namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

/// <summary>One bad value in the config file. Code names the broken rule, Path the JSON path (or the parent when a key is missing), Message is for the log only.</summary>
public readonly record struct HeadhunterConfigProblem(
    HeadhunterConfigProblemCode Code,
    string Path,
    string Message
);
