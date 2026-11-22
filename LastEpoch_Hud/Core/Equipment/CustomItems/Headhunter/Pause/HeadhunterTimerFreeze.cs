using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause;

/// <summary>Holds HH buff timers while paused: kept seconds per stat row.</summary>
public sealed class HeadhunterTimerFreeze
{
    /// <summary>Placeholder seconds written into the game while a timer is held.</summary>
    public const float HeldSeconds = 1_000_000f;

    private readonly List<BuffAction> _actions = new();
    private readonly List<BuffAction> _held = new();
    private float[] _kept = Array.Empty<float>();

    public bool IsPaused { get; private set; }
    public bool IsHolding { get; private set; }
    public bool IsApplyPending => IsPaused != IsHolding;

    public HeadhunterPauseChange Request(bool paused)
    {
        if (paused == IsPaused)
        {
            return HeadhunterPauseChange.None;
        }

        IsPaused = paused;
        return paused ? HeadhunterPauseChange.Paused : HeadhunterPauseChange.Resumed;
    }

    /// <summary>Actions for the pending transition. Valid until the next call.</summary>
    public IReadOnlyList<BuffAction> Apply(
        HeadhunterResolvedConfig config,
        IReadOnlyList<float> live
    )
    {
        _actions.Clear();
        if (!IsApplyPending)
        {
            return _actions;
        }

        if (IsPaused)
        {
            HoldLive(config, live);
            return _actions;
        }

        Release(config);
        return _actions;
    }

    /// <summary>Kill actions rewritten to the held placeholder while holding. Valid until the next call.</summary>
    public IReadOnlyList<BuffAction> Hold(
        HeadhunterResolvedConfig config,
        IReadOnlyList<BuffAction> actions
    )
    {
        if (!IsHolding)
        {
            return actions;
        }

        EnsureRows(config.Stats.Count);
        _held.Clear();
        for (int i = 0; i < actions.Count; i++)
        {
            _held.Add(HoldOne(config, actions[i]));
        }

        return _held;
    }

    /// <summary>Swaps live seconds for the kept seconds while holding.</summary>
    public void ShowFrozen(float[] remaining)
    {
        if (!IsHolding)
        {
            return;
        }

        int count = Math.Min(remaining.Length, _kept.Length);
        for (int i = 0; i < count; i++)
        {
            if (remaining[i] > 0f && _kept[i] > 0f)
            {
                remaining[i] = _kept[i];
            }
        }
    }

    /// <summary>Drops kept seconds; the buffs were just removed, so nothing is held or pending.</summary>
    public void ClearTimers()
    {
        Array.Clear(_kept, 0, _kept.Length);
        IsHolding = IsPaused;
    }

    private BuffAction HoldOne(HeadhunterResolvedConfig config, BuffAction action)
    {
        bool known = config.TryGetRow(
            new HeadhunterStatKey(action.StatId, action.Tags),
            out int row
        );
        if (!known)
        {
            return action;
        }

        if (action.Kind == BuffActionKind.Remove)
        {
            _kept[row] = 0f;
            return action;
        }

        if (action.Kind == BuffActionKind.SetRemaining)
        {
            return action;
        }

        _kept[row] = action.DurationSeconds;
        return action with { DurationSeconds = HeldSeconds };
    }

    /// <summary>Keeps live seconds and emits held actions.</summary>
    private void HoldLive(HeadhunterResolvedConfig config, IReadOnlyList<float> live)
    {
        EnsureRows(config.Stats.Count);
        for (int i = 0; i < config.Stats.Count; i++)
        {
            float seconds = i < live.Count ? live[i] : 0f;
            _kept[i] = seconds > 0f ? Math.Min(seconds, config.DurationSeconds) : 0f;
            if (_kept[i] > 0f)
            {
                _actions.Add(SetRemaining(config.Stats[i], HeldSeconds));
            }
        }

        IsHolding = true;
    }

    /// <summary>Emits kept seconds and drops them.</summary>
    private void Release(HeadhunterResolvedConfig config)
    {
        int count = Math.Min(_kept.Length, config.Stats.Count);
        for (int i = 0; i < count; i++)
        {
            if (_kept[i] > 0f)
            {
                _actions.Add(SetRemaining(config.Stats[i], _kept[i]));
            }
        }

        Array.Clear(_kept, 0, _kept.Length);
        IsHolding = false;
    }

    /// <summary>Resizes the kept seconds (fresh array on resize).</summary>
    private void EnsureRows(int count)
    {
        if (_kept.Length != count)
        {
            _kept = new float[count];
        }
    }

    private static BuffAction SetRemaining(HeadhunterBuffStat stat, float seconds)
    {
        return new BuffAction(
            BuffActionKind.SetRemaining,
            stat.BuffName,
            stat.StatId,
            0f,
            0f,
            seconds,
            0,
            stat.Tags
        );
    }
}
