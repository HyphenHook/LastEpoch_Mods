namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>Restore Defaults: backs the old file up first, then writes the defaults.</summary>
public static class MagebloodRestore
{
    public static MagebloodRestoreResult Run(
        IConfigText file,
        IConfigText backup,
        string defaultsText
    )
    {
        if (!file.Exists())
        {
            return file.Write(defaultsText)
                ? MagebloodRestoreResult.DefaultsWritten
                : MagebloodRestoreResult.WriteFailed;
        }

        string old = file.Read();
        if (old == null)
        {
            return MagebloodRestoreResult.ReadFailed;
        }

        if (old == defaultsText)
        {
            return MagebloodRestoreResult.AlreadyDefaults;
        }

        if (!backup.Write(old))
        {
            return MagebloodRestoreResult.BackupFailed;
        }

        return file.Write(defaultsText)
            ? MagebloodRestoreResult.BackedUpAndWritten
            : MagebloodRestoreResult.WriteFailed;
    }
}
