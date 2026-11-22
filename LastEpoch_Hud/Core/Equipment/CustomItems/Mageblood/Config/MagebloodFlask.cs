using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>One game-ready flask: name, icon name and resolved stat rows.</summary>
public sealed class MagebloodFlask
{
    public MagebloodFlask(
        string name,
        string iconName,
        IReadOnlyList<MagebloodBuffStat> stats,
        IReadOnlyList<MagebloodStatEntry> rows
    )
    {
        Name = name;
        IconName = iconName;
        Stats = stats;
        Rows = rows;
    }

    public string Name { get; }
    public string IconName { get; }
    public IReadOnlyList<MagebloodBuffStat> Stats { get; }

    /// <summary>The file row of each <see cref="Stats"/> entry, same order.</summary>
    public IReadOnlyList<MagebloodStatEntry> Rows { get; }
}
