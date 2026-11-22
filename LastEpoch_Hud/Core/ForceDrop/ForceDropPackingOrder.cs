using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

public static class ForceDropPackingOrder
{
    // Fixed unique modifiers occupy the unique prefix before the native seal
    // and ordinary-affix sequence. Return indices so callers move existing
    // objects without reconstructing or changing any selected affix metadata.
    public static IReadOnlyList<int> UniquePrefixIndices(
        IReadOnlyList<int> affixIds,
        IEnumerable<int> variantIds
    )
    {
        if (affixIds == null)
            throw new ArgumentNullException(nameof(affixIds));
        if (variantIds == null)
            throw new ArgumentNullException(nameof(variantIds));
        var variants = new HashSet<int>();
        foreach (int id in variantIds)
            if (!variants.Add(id))
                throw new ArgumentException("Duplicate unique modifier IDs.", nameof(variantIds));
        var seen = new HashSet<int>();
        var prefix = new List<int>();
        var remaining = new List<int>();
        for (int i = 0; i < affixIds.Count; i++)
        {
            int id = affixIds[i];
            if (!seen.Add(id))
                throw new ArgumentException("Duplicate item affix IDs.", nameof(affixIds));
            if (variants.Contains(id))
                prefix.Add(i);
            else
                remaining.Add(i);
        }
        if (prefix.Count != variants.Count)
            throw new ArgumentException("A selected unique modifier is missing.", nameof(affixIds));
        prefix.AddRange(remaining);
        return prefix.AsReadOnly();
    }
}
