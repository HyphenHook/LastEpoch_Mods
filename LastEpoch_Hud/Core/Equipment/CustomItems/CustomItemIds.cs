using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Picks the subtype id of a custom base item.</summary>
public static class CustomItemIds
{
    public const int None = -1;

    private const int MaxId = byte.MaxValue;

    public static int PickBaseId(int baseId, int subtypeCount, IReadOnlySet<int> usedIds)
    {
        if (baseId == CustomUniqueSpec.AllocateBaseId)
        {
            return FirstFreeFrom(subtypeCount, usedIds);
        }

        return IsFree(baseId, usedIds) ? baseId : None;
    }

    private static int FirstFreeFrom(int start, IReadOnlySet<int> usedIds)
    {
        for (int id = start; id <= MaxId; id++)
        {
            if (IsFree(id, usedIds))
            {
                return id;
            }
        }

        return None;
    }

    private static bool IsFree(int id, IReadOnlySet<int> usedIds)
    {
        return id >= 0 && id <= MaxId && !usedIds.Contains(id);
    }
}
