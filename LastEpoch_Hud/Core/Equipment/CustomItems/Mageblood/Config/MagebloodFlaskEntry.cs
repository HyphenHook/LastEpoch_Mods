using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>One flask as written in the file.</summary>
public sealed class MagebloodFlaskEntry
{
    public string Name { get; init; }
    public string Icon { get; init; }
    public IReadOnlyList<MagebloodStatEntry> Stats { get; init; }
}
