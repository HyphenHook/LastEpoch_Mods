using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;
using LastEpoch_Hud.Scripts.ModUI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Menu;

/// <summary>Writes the menu's edits into mageblood.json, debounced.</summary>
internal static class MagebloodMenuWriter
{
    public const double QuietSeconds = 0.4;

    private static readonly MagebloodEditDebounce _debounce = new(QuietSeconds);
    private static readonly CustomItemConfigStore _backupStore = new("mageblood.backup.json");

    public static void Queue(MagebloodValueEdit edit, double now)
    {
        if (_debounce.Set(edit, now, out MagebloodValueEdit flushFirst))
        {
            Write(flushFirst);
        }
    }

    public static bool TryPeek(MagebloodValueTarget target, out float fileValue)
    {
        return _debounce.TryPeek(target, out fileValue);
    }

    public static void Tick(double now)
    {
        if (_debounce.TryTakeDue(now, out MagebloodValueEdit edit))
        {
            Write(edit);
        }
    }

    /// <summary>Writes a pending edit first, then swaps two flasks in the file. False when nothing was written.</summary>
    public static bool Swap(string first, string second)
    {
        if (_debounce.TryTakeAny(out MagebloodValueEdit pending))
        {
            Write(pending);
        }

        CustomItemConfigStore store = MagebloodConfigLoader.Store;
        string json = store.Read();
        if (json == null)
        {
            ModSettings.Trace("Mageblood swap: config not readable");
            return false;
        }

        if (!MagebloodFileEditor.TrySwapFlasks(json, first, second, out string text))
        {
            Main.logger_instance?.Warning("Mageblood swap refused: " + first + " / " + second);
            return false;
        }

        if (!store.Write(text))
        {
            ModSettings.Trace("Mageblood swap: write failed");
            return false;
        }

        ModSettings.Trace("Mageblood swap written: " + first + " / " + second);
        MagebloodConfigLoader.RequestReload();
        return true;
    }

    /// <summary>Writes the defaults. True when the old file was saved as a backup first.</summary>
    public static bool Restore()
    {
        _debounce.Discard();
        MagebloodRestoreResult result = MagebloodRestore.Run(
            MagebloodConfigLoader.Store,
            _backupStore,
            MagebloodConfigWriter.Write(MagebloodConfigDefaults.Config)
        );
        if (
            result
            is MagebloodRestoreResult.DefaultsWritten
                or MagebloodRestoreResult.BackedUpAndWritten
                or MagebloodRestoreResult.AlreadyDefaults
        )
        {
            MagebloodConfigLoader.RequestReload();
            return result == MagebloodRestoreResult.BackedUpAndWritten;
        }

        Main.logger_instance?.Warning("Mageblood restore aborted: " + result);
        return false;
    }

    private static void Write(MagebloodValueEdit edit)
    {
        CustomItemConfigStore store = MagebloodConfigLoader.Store;
        string json = store.Read();
        if (json == null)
        {
            return;
        }

        if (!MagebloodFileEditor.TrySetValue(json, edit.Target, edit.FileValue, out string text))
        {
            Main.logger_instance?.Warning(
                "Mageblood edit refused for " + edit.Target + ": " + edit.FileValue
            );
            return;
        }

        if (store.Write(text))
        {
            MagebloodConfigLoader.RequestReload();
        }
    }
}
