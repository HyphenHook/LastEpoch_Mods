using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.Tuning;

namespace LastEpoch_Hud;

/// <summary>Loads Data/CraftingNumbers.csv (embedded at compile time) before any feature reads its numbers.</summary>
internal static class TunablesLoader
{
    internal static void Load()
    {
        string text = EmbeddedData.Read("CraftingNumbers.csv");
        if (text == null)
        {
            Main.logger_instance?.Warning("CraftingNumbers.csv is not in the mod; every crafting number keeps its default.");
            return;
        }
        var errors = new List<string>();
        var table = TunablesTable.Parse(text, errors);
        Tunables.Set(table);
        foreach (string error in errors) Main.logger_instance?.Warning(error);
        Main.logger_instance?.Msg("Crafting numbers: loaded " + table.Count + " from CraftingNumbers.csv (" + errors.Count + " problems).");
    }
}
