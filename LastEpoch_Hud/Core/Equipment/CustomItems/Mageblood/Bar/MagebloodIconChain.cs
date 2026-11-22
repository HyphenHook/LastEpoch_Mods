using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Bar;

/// <summary>Orders the icon lookups of a flask, the item icon last.</summary>
public static class MagebloodIconChain
{
    public static IReadOnlyList<MagebloodIconSource> For(
        string iconName,
        string flaskName,
        IReadOnlyList<string> statNames
    )
    {
        var steps = new List<MagebloodIconSource>();
        AddIcon(steps, iconName);
        AddBuff(steps, MagebloodDefaultIcons.BuffIconFor(flaskName));
        AddStats(steps, statNames);
        steps.Add(new MagebloodIconSource(MagebloodIconSourceKind.ItemIcon, null));
        return steps;
    }

    private static void AddIcon(List<MagebloodIconSource> steps, string iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return;
        }
        steps.Add(new MagebloodIconSource(MagebloodIconSourceKind.FlaskIcon, iconName));
        AddBuff(steps, iconName);
    }

    private static void AddStats(List<MagebloodIconSource> steps, IReadOnlyList<string> statNames)
    {
        if (statNames == null)
        {
            return;
        }
        foreach (string stat in statNames)
        {
            AddBuff(steps, stat);
        }
    }

    private static void AddBuff(List<MagebloodIconSource> steps, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        foreach (MagebloodIconSource step in steps)
        {
            if (
                step.Kind == MagebloodIconSourceKind.BuffIcon
                && string.Equals(step.Name, name, StringComparison.Ordinal)
            )
            {
                return;
            }
        }
        steps.Add(new MagebloodIconSource(MagebloodIconSourceKind.BuffIcon, name));
    }
}
