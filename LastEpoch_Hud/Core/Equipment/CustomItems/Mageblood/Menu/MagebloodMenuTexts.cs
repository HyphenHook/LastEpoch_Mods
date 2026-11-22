using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>Locale keys of the Mageblood card and the choice of its status lines.</summary>
public static class MagebloodMenuTexts
{
    public const string CardTitle = "Mageblood";
    public const string MaxResistances = "Maximum Resistances";
    public const string RestoreDefaults = "Restore Defaults";
    public const string ConfirmRestore = "Press again to confirm";
    public const string WornActiveFlasks = "Worn: {0} active flasks.";
    public const string NotWorn = "Mageblood.NotWorn";
    public const string FileError = "Mageblood.FileError";
    public const string FileSkipped = "Mageblood.FileSkipped";
    public const string BackupSaved = "Mageblood.BackupSaved";
    public const string TooltipRestart = "Mageblood.TooltipRestart";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CardTitle,
        MaxResistances,
        RestoreDefaults,
        ConfirmRestore,
        WornActiveFlasks,
        NotWorn,
        FileError,
        FileSkipped,
        BackupSaved,
        TooltipRestart,
    };

    public static string Status(
        IReadOnlyDictionary<string, string> texts,
        MagebloodMenuStatus status
    )
    {
        if (!status.Worn)
        {
            return LocaleText.Get(texts, NotWorn);
        }

        return TextTemplate.Fill(LocaleText.Get(texts, WornActiveFlasks), status.ActiveFlasks);
    }

    public static string Warning(
        IReadOnlyDictionary<string, string> texts,
        MagebloodFileWarning warning
    )
    {
        return warning switch
        {
            MagebloodFileWarning.FileError => LocaleText.Get(texts, FileError),
            MagebloodFileWarning.SomeSkipped => LocaleText.Get(texts, FileSkipped),
            _ => null,
        };
    }

    public static string RestoreLabel(bool armed)
    {
        return armed ? ConfirmRestore : RestoreDefaults;
    }
}
