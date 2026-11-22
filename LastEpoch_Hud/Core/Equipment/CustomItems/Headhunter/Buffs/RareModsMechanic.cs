using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Rare or magic kill: stack its mod stats as buffs. Boss or miniboss kill: one random not-live buff. Always refreshes all live ones.</summary>
public sealed class RareModsMechanic
{
    private readonly HeadhunterResolvedConfig _config;
    private readonly HeadhunterStackState _stacks;
    private readonly List<BuffAction> _actions = new();
    private readonly HashSet<int> _handled = new();
    private readonly IHeadhunterRandom _random;

    public RareModsMechanic(
        HeadhunterResolvedConfig config,
        HeadhunterStackState stacks,
        IHeadhunterRandom random
    )
    {
        _config = config;
        _stacks = stacks;
        _random = random;
    }

    /// <summary>Buff changes for one kill. The list is reused: valid until the next call.</summary>
    public IReadOnlyList<BuffAction> OnKill(KillInfo kill, IReadOnlySet<int> liveRows)
    {
        _actions.Clear();
        _handled.Clear();
        if (!Fires(kill))
        {
            return _actions;
        }

        _stacks.Sync(liveRows);
        AddOwnRows(kill);
        RefreshOtherLive();
        return _actions;
    }

    /// <summary>Drops all per-run state (stacks and anything kept between kills).</summary>
    public void Reset()
    {
        _stacks.Reset();
    }

    private bool Fires(KillInfo kill)
    {
        if (kill.ByMinion && !_config.Triggers.MinionKills)
        {
            return false;
        }

        return kill.Kind switch
        {
            KillKind.Rare => _config.Triggers.Rare,
            KillKind.Boss => _config.Triggers.Boss,
            KillKind.Miniboss => _config.Triggers.Miniboss,
            KillKind.Magic => _config.Triggers.Magic,
            _ => false,
        };
    }

    private void AddOwnRows(KillInfo kill)
    {
        if (kill.Kind is KillKind.Boss or KillKind.Miniboss)
        {
            AddRandomRow();
            return;
        }

        AddKillStats(kill);
    }

    private void AddRandomRow()
    {
        int count = 0;
        for (int row = 0; row < _config.Stats.Count; row++)
        {
            if (_stacks.Get(row) == 0)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        int skip = _random.Next(count);
        for (int row = 0; row < _config.Stats.Count; row++)
        {
            if (_stacks.Get(row) != 0 || skip-- > 0)
            {
                continue;
            }

            _handled.Add(row);
            _stacks.TryAdd(row, _config.MaxStacks);
            Emit(BuffActionKind.Add, row);
            return;
        }
    }

    private void AddKillStats(KillInfo kill)
    {
        if (kill.ModStats == null)
        {
            return;
        }
        if (kill.Mods == null)
        {
            AddStatRange(kill.ModStats, 0, kill.ModStats.Count);
            return;
        }

        for (int i = 0; i < kill.Mods.Count; i++)
        {
            AddMod(kill.ModStats, kill.Mods[i]);
        }
    }

    private void AddMod(IReadOnlyList<HeadhunterStatKey> stats, KillMod mod)
    {
        if (!_config.TryGetAffixRows(mod.Key, out IReadOnlyList<int> rows))
        {
            AddStatRange(stats, mod.StatStart, mod.StatCount);
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            AddRow(rows[i]);
        }
    }

    private void AddStatRange(IReadOnlyList<HeadhunterStatKey> stats, int start, int count)
    {
        int end = Math.Min(start + count, stats.Count);
        for (int i = Math.Max(start, 0); i < end; i++)
        {
            if (_config.TryGetRow(stats[i], out int row))
            {
                AddRow(row);
            }
        }
    }

    private void AddRow(int row)
    {
        if (!_handled.Add(row))
        {
            return;
        }

        bool added = _stacks.TryAdd(row, _config.MaxStacks);
        Emit(added ? BuffActionKind.Add : BuffActionKind.Refresh, row);
    }

    private void RefreshOtherLive()
    {
        for (int row = 0; row < _config.Stats.Count; row++)
        {
            if (_stacks.Get(row) == 0 || !_handled.Add(row))
            {
                continue;
            }

            Emit(BuffActionKind.Refresh, row);
        }
    }

    private void Emit(BuffActionKind kind, int row)
    {
        HeadhunterBuffStat stat = _config.Stats[row];
        int stacks = _stacks.Get(row);
        _actions.Add(
            new BuffAction(
                kind,
                stat.BuffName,
                stat.StatId,
                stat.AddedFor(stacks),
                stat.IncreasedFor(stacks),
                _config.DurationSeconds,
                stacks,
                stat.Tags
            )
        );
    }
}
