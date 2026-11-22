using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

/// <summary>Turns "the first N flasks apply" into buff actions: leftover buffs are removed first, then the current ones are set.</summary>
public static class MagebloodFlaskRule
{
    public const float PermanentSeconds = 1_000_000f;

    public static IReadOnlyList<BuffAction> Plan(
        IReadOnlyList<MagebloodFlask> flasks,
        int slots,
        IReadOnlyCollection<string> applied
    )
    {
        var adds = new List<BuffAction>();
        AddActions(flasks, slots, adds);

        var actions = new List<BuffAction>();
        AddRemoves(applied, adds, actions);
        actions.AddRange(adds);
        return actions;
    }

    public static int ActiveCount(int flaskCount, int slots)
    {
        return Math.Min(Math.Max(slots, 0), flaskCount);
    }

    private static void AddActions(
        IReadOnlyList<MagebloodFlask> flasks,
        int slots,
        List<BuffAction> into
    )
    {
        if (flasks == null)
        {
            return;
        }
        int count = ActiveCount(flasks.Count, slots);
        for (int i = 0; i < count; i++)
        {
            foreach (MagebloodBuffStat row in flasks[i].Stats)
            {
                into.Add(ToAdd(row));
            }
        }
    }

    private static void AddRemoves(
        IReadOnlyCollection<string> applied,
        List<BuffAction> adds,
        List<BuffAction> into
    )
    {
        if (applied == null)
        {
            return;
        }
        var skip = new HashSet<string>(StringComparer.Ordinal);
        foreach (BuffAction add in adds)
        {
            skip.Add(add.BuffName);
        }
        foreach (string name in applied)
        {
            if (skip.Add(name))
            {
                into.Add(ToRemove(name));
            }
        }
    }

    private static BuffAction ToAdd(MagebloodBuffStat row)
    {
        return new BuffAction(
            BuffActionKind.Add,
            row.BuffName,
            row.StatId,
            row.Added,
            row.Increased,
            PermanentSeconds,
            1,
            row.Tags,
            row.More
        );
    }

    private static BuffAction ToRemove(string name)
    {
        return new BuffAction(BuffActionKind.Remove, name, 0, 0f, 0f, 0f, 0);
    }
}
