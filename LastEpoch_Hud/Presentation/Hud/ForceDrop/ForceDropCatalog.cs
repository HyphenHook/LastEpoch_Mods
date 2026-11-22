using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.ForceDrop;

namespace LastEpoch_Hud.Scripts.ModUI.ForceDrop;

// One definition lookup for the picker, validation and the native constructor.
public static class ForceDropCatalog
{
    static readonly Dictionary<int, AffixList.Affix> definitions =
        new Dictionary<int, AffixList.Affix>();
    static IntPtr pointer;
    static int count = -1;

    static void Load()
    {
        var list = AffixList.get();
        if (list.IsNullOrDestroyed())
        {
            definitions.Clear();
            pointer = IntPtr.Zero;
            count = -1;
            return;
        }
        var all = list.AllAffixes;
        int currentCount = all.IsNullOrDestroyed() ? -1 : all.Count;
        if (
            currentCount >= 0
            && pointer == list.Pointer
            && count == currentCount
            && definitions.Count > 0
        )
            return;
        definitions.Clear();
        if (!all.IsNullOrDestroyed())
            foreach (var affix in all)
                Add(affix);
        if (!list.singleAffixes.IsNullOrDestroyed())
            foreach (var affix in list.singleAffixes)
                Add(affix);
        if (!list.multiAffixes.IsNullOrDestroyed())
            foreach (var affix in list.multiAffixes)
                Add(affix);
        pointer = list.Pointer;
        count = currentCount;
    }

    static void Add(AffixList.Affix affix)
    {
        if (!affix.IsNullOrDestroyed() && !definitions.ContainsKey(affix.affixId))
            definitions.Add(affix.affixId, affix);
    }

    public static IEnumerable<AffixList.Affix> Definitions()
    {
        Load();
        return definitions.Values;
    }

    public static AffixList.Affix Find(int id)
    {
        Load();
        return definitions.TryGetValue(id, out var affix) ? affix : null;
    }

    public static int MaximumTier(AffixList.Affix definition, int routeMaximum = 7)
    {
        return ForceDropTierRules.MaximumDisplayTier(
            definition.IsNullOrDestroyed() || definition.tiers.IsNullOrDestroyed()
                ? 0
                : definition.tiers.Count,
            routeMaximum
        );
    }
}
