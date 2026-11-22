using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>Locale keys of the Mageblood Flasks card and its slot label.</summary>
public static class MagebloodFlasksTexts
{
    public const string CardTitle = "Mageblood Flasks";
    public const string Slot = "Slot {0}";
    public const string SlotInactive = "Slot {0} (inactive)";
    public const string LeftmostHelp = "Mageblood.LeftmostHelp";
    public const string NoFlasks = "Mageblood.NoFlasks";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CardTitle,
        Slot,
        SlotInactive,
        LeftmostHelp,
        NoFlasks,
    };

    /// <summary>The slot's label, numbered from 1; null when the template is missing.</summary>
    public static string SlotLabel(
        IReadOnlyDictionary<string, string> texts,
        int slot,
        bool inactive
    )
    {
        string template = LocaleText.Get(texts, inactive ? SlotInactive : Slot);
        return TextTemplate.Fill(template, slot + 1);
    }
}
