using System;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood;

/// <summary>Shows the Mageblood resistance cap on the character sheet.</summary>
internal static class MagebloodSheetResistance
{
    public static void Apply(float uncapped, ref string text)
    {
        if (!MagebloodBuffs.Worn)
        {
            return;
        }
        try
        {
            if (
                MagebloodResistanceText.TryFormat(
                    uncapped,
                    MagebloodConfigLoader.Current.MaxResistances,
                    out string rebuilt
                )
            )
            {
                text = rebuilt;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "MagebloodSheetResistance.Apply");
        }
    }
}
