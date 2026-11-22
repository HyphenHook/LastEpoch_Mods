using System.Collections.Generic;
using System.Globalization;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

/// <summary>Mageblood description text, with the slot range or an item's rolled count.</summary>
public static class MagebloodDescription
{
    /// <summary>Description template filled with the slot range and the max resistance bonus, null when missing.</summary>
    public static string Text(IReadOnlyDictionary<string, string> texts, float maxResistances)
    {
        string template = LocaleText.Get(texts, CustomItemLocaleKeys.MagebloodDescription);
        return TextTemplate.Fill(
            template,
            MagebloodFlaskSlots.RangeText,
            maxResistances.ToString("0.##", CultureInfo.InvariantCulture)
        );
    }

    /// <summary>The text with the slot range replaced by the rolled slot count.</summary>
    public static string WithSlots(string description, int slots)
    {
        if (description == null)
        {
            return null;
        }

        return description.Replace(
            MagebloodFlaskSlots.RangeText,
            slots.ToString(CultureInfo.InvariantCulture)
        );
    }
}
