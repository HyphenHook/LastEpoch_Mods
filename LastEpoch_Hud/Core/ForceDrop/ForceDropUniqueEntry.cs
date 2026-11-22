using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

/// <summary>Plain snapshot of one game unique/set entry.</summary>
public readonly record struct ForceDropUniqueEntry(
    int UniqueId,
    int BaseType,
    bool IsSet,
    IReadOnlyList<int> SubTypes,
    string Label
);
