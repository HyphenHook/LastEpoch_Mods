namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Mirror of the game's AbilityMovement values, kept free of game types.</summary>
public enum DashMovement
{
    None = 0,
    Leap = 1,
    StationaryLeap = 2,
    Dash = 3,
    Spin = 4,
    FixedDistanceDash = 5,
    Stationary = 6,
    DashThenWait = 7,
    ZigZag = 8,
    VariableSpeedDash = 9,
    FixedDistanceDashThenWait = 10,
    FixedDistanceVariableSpeedDash = 11,
    LeapThenWait = 12,
    FixedDistanceLeap = 13,
    DashThenWaitThenDash = 14,
    FixedDistanceDashThenWaitThenDash = 15,
}
