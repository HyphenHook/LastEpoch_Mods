using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;
using MelonLoader;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

/// <summary>Applies Headhunter buff actions to the player's buffs.</summary>
internal static class HeadhunterBuffSink
{
    private static readonly Func<string, bool> _isLiveCached = IsLiveCurrent;
    private static StatBuffs _current;

    public static StatBuffs PlayerBuffs()
    {
        if (Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            return null;
        }

        StatBuffs buffs = Refs_Manager.player_actor.statBuffs;
        return buffs.IsNullOrDestroyed() ? null : buffs;
    }

    public static void FillActive(
        StatBuffs buffs,
        IReadOnlyList<HeadhunterBuffStat> stats,
        HashSet<int> liveRows
    )
    {
        _current = buffs;
        HeadhunterLiveRows.Fill(stats, _isLiveCached, liveRows);
        _current = null;
    }

    public static void FillRemaining(
        StatBuffs buffs,
        IReadOnlyList<HeadhunterBuffStat> stats,
        float[] remaining
    )
    {
        for (int i = 0; i < stats.Count; i++)
        {
            remaining[i] = TryGetLive(buffs, stats[i].BuffName, out Buff buff)
                ? buff.remainingDuration
                : 0f;
        }
    }

    public static void Apply(StatBuffs buffs, IReadOnlyList<BuffAction> actions)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            Apply(buffs, actions[i]);
        }
    }

    public static void Apply(StatBuffs buffs, BuffAction action)
    {
        switch (action.Kind)
        {
            case BuffActionKind.Add:
                Replace(buffs, action);
                break;
            case BuffActionKind.Refresh:
                Refresh(buffs, action);
                break;
            case BuffActionKind.Remove:
                buffs.removeBuffsWithName(action.BuffName);
                break;
            case BuffActionKind.SetRemaining:
                SetRemaining(buffs, action);
                break;
        }
    }

    public static bool IsLive(StatBuffs buffs, string name)
    {
        return TryGetLive(buffs, name, out _);
    }

    /// <summary>Sets the seconds of a live buff.</summary>
    private static void SetRemaining(StatBuffs buffs, BuffAction action)
    {
        if (!TryGetLive(buffs, action.BuffName, out Buff buff))
        {
            return;
        }

        buff.remainingDuration = action.DurationSeconds;
    }

    private static void Refresh(StatBuffs buffs, BuffAction action)
    {
        if (!TryGetLive(buffs, action.BuffName, out Buff buff))
        {
            Replace(buffs, action);
            return;
        }

        buff.remainingDuration = action.DurationSeconds;
    }

    private static void Replace(StatBuffs buffs, BuffAction action)
    {
        buffs.removeBuffsWithName(action.BuffName);
        buffs.addBuff(
            action.DurationSeconds,
            (SP)action.StatId,
            action.Added,
            action.Increased,
            MoreValues(action.More),
            (AT)action.Tags,
            0,
            0,
            action.BuffName
        );
    }

    private static Il2CppSystem.Collections.Generic.List<float> MoreValues(float more)
    {
        if (more == 0f)
        {
            return null;
        }

        var list = new Il2CppSystem.Collections.Generic.List<float>();
        list.Add(more);
        return list;
    }

    private static bool IsLiveCurrent(string name)
    {
        return TryGetLive(_current, name, out _);
    }

    private static bool TryGetLive(StatBuffs buffs, string name, out Buff buff)
    {
        buff = null;
        if (buffs.activeBuffNames == null)
        {
            return false;
        }

        if (!buffs.activeBuffNames.TryGetValue(name, out buff))
        {
            return false;
        }

        return !buff.IsNullOrDestroyed() && buff.remainingDuration > 0f;
    }
}
