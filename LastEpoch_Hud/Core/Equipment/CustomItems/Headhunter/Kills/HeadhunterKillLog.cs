using System;
using System.Collections.Generic;
using System.Text;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;

/// <summary>Builds the one-line debug text for a Headhunter kill.</summary>
public static class HeadhunterKillLog
{
    public static string Format(
        KillInfo kill,
        IReadOnlyList<BuffAction> actions,
        Func<int, string> statName,
        Func<int, string> tagName
    )
    {
        var text = new StringBuilder("Headhunter kill: kind=");
        text.Append(kill.Kind).Append(" byMinion=").Append(kill.ByMinion).Append(" keys=[");
        AppendKeys(text, kill.Mods);
        text.Append("] mods=[");
        AppendMods(text, kill.ModStats, statName, tagName);
        text.Append("] actions=[");
        AppendActions(text, actions);
        return text.Append(']').ToString();
    }

    private static void AppendKeys(StringBuilder text, IReadOnlyList<KillMod> mods)
    {
        if (mods == null)
        {
            return;
        }

        for (int i = 0; i < mods.Count; i++)
        {
            AppendSeparator(text, i);
            text.Append(mods[i].Key);
        }
    }

    private static void AppendMods(
        StringBuilder text,
        IReadOnlyList<HeadhunterStatKey> mods,
        Func<int, string> statName,
        Func<int, string> tagName
    )
    {
        if (mods == null)
        {
            return;
        }

        for (int i = 0; i < mods.Count; i++)
        {
            AppendSeparator(text, i);
            text.Append(statName(mods[i].StatId));
            AppendTag(text, mods[i].Tags, tagName);
        }
    }

    private static void AppendTag(StringBuilder text, int tags, Func<int, string> tagName)
    {
        if (tags != 0)
        {
            text.Append('[').Append(tagName(tags)).Append(']');
        }
    }

    private static void AppendActions(StringBuilder text, IReadOnlyList<BuffAction> actions)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            AppendSeparator(text, i);
            text.Append(actions[i].Kind).Append(' ').Append(actions[i].BuffName);
            AppendStacks(text, actions[i].Stacks);
        }
    }

    private static void AppendStacks(StringBuilder text, int stacks)
    {
        if (stacks > 1)
        {
            text.Append(" x").Append(stacks);
        }
    }

    private static void AppendSeparator(StringBuilder text, int index)
    {
        if (index > 0)
        {
            text.Append(", ");
        }
    }
}
