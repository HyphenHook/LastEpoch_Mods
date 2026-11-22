using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>A usable config plus every problem found while reading it.</summary>
public sealed class MagebloodConfigParseResult
{
    public MagebloodConfig Config { get; init; }
    public IReadOnlyList<MagebloodConfigProblem> Problems { get; init; }
    public bool IsReadable { get; init; }
}
