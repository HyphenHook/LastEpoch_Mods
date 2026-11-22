namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>What the buff sink must do.</summary>
public enum BuffActionKind
{
    Add,
    Refresh,
    Remove,

    /// <summary>Set the seconds of a live buff; never adds.</summary>
    SetRemaining,
}
