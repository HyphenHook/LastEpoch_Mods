namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

/// <summary>Why the Mageblood buffs must be synced again.</summary>
public enum MagebloodSyncReason
{
    None,
    Dirty,
    WornChanged,
    BuffsLost,
}
