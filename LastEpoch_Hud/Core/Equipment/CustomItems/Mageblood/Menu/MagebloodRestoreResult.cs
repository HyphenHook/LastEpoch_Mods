namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>How a Restore Defaults run ended.</summary>
public enum MagebloodRestoreResult
{
    DefaultsWritten,
    BackedUpAndWritten,
    AlreadyDefaults,
    BackupFailed,
    ReadFailed,
    WriteFailed,
}
