namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>One bad value in the config file. Code names the broken rule, Path the JSON path, Message is for the log only.</summary>
public readonly record struct MagebloodConfigProblem(
    MagebloodConfigProblemCode Code,
    string Path,
    string Message
);
