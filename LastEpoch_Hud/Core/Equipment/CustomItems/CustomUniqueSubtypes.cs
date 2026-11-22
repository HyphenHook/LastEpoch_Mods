using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Subtype id each custom unique was registered with.</summary>
public sealed class CustomUniqueSubtypes
{
    private readonly Dictionary<int, int> _byUniqueId = new();

    /// <summary>Records (overwrites) the registered subtype of a unique.</summary>
    public void Set(int uniqueId, int subType)
    {
        _byUniqueId[uniqueId] = subType;
    }

    /// <summary>Registered subtype, <see cref="CustomItemIds.None"/> if not set.</summary>
    public int Get(int uniqueId)
    {
        return _byUniqueId.TryGetValue(uniqueId, out int subType) ? subType : CustomItemIds.None;
    }
}
