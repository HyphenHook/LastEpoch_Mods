using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Bar;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Bar;

/// <summary>Hover text of one flask: its name, then one line per stat.</summary>
internal static class MagebloodFlaskTooltip
{
    public static string Text(MagebloodFlask flask)
    {
        var rows = new List<string>(flask.Stats.Count);
        foreach (MagebloodBuffStat stat in flask.Stats)
        {
            rows.Add(Row(stat));
        }

        string name = MagebloodFlaskNames.Display(Locales.current_dictionary, flask.Name);
        return MagebloodFlaskLabel.Format(name, rows);
    }

    private static string Row(MagebloodBuffStat stat)
    {
        HeadhunterStatNames.TryRead(stat.StatId, out string gameName, out bool addedAsPercent);
        string row = HeadhunterBuffLabel.Format(
            gameName,
            HeadhunterStatNames.EnumName(stat.StatId),
            stat.Added,
            stat.Increased,
            addedAsPercent,
            1,
            HeadhunterStatNames.GameTagName(stat.Tags),
            stat.Tags == 0 ? null : HeadhunterStatNames.TagEnumName(stat.Tags)
        );
        return MagebloodFlaskLabel.WithMore(row, stat.More);
    }
}
