using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>The parsed Mageblood config file. Flask order is slot order.</summary>
public sealed class MagebloodConfig
{
    public int Version { get; init; }
    public IReadOnlyList<MagebloodFlaskEntry> Flasks { get; init; }
    public float MaxResistances { get; init; }
}
