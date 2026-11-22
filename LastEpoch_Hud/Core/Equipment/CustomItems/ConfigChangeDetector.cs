using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Decides when to look at a config file and whether its last-write stamp changed.</summary>
public sealed class ConfigChangeDetector
{
    private readonly IntervalGate _gate;
    private DateTime? _remembered;
    private bool _forced;

    public ConfigChangeDetector(double intervalSeconds)
    {
        _gate = new IntervalGate(intervalSeconds);
    }

    public void Remember(DateTime? writeTimeUtc)
    {
        _remembered = writeTimeUtc;
    }

    /// <summary>The next check is due and counts as changed, even with the same stamp.</summary>
    public void ForceNext()
    {
        _forced = true;
    }

    public bool IsCheckDue(double now)
    {
        return _forced || _gate.IsDue(now);
    }

    public bool HasChanged(DateTime? writeTimeUtc)
    {
        bool forced = _forced;
        _forced = false;
        if (writeTimeUtc == null)
        {
            return false;
        }
        if (!forced && writeTimeUtc == _remembered)
        {
            return false;
        }

        _remembered = writeTimeUtc;
        return true;
    }
}
