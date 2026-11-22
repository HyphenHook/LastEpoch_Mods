namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>One stage of adding a custom unique to the game.</summary>
public enum CustomUniqueStep
{
    Wait,
    AddBase,
    AddUnique,
    AddToDictionary,
    Done,
    Failed,
}
