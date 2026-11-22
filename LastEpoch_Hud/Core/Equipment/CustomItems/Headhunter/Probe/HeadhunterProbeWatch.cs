namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Probe;

/// <summary>Polls the probe flags at a fixed rate and reports each change.</summary>
public sealed class HeadhunterProbeWatch
{
    public const double PollSeconds = 0.25;

    private readonly IntervalGate _gate = new(PollSeconds);
    private HeadhunterProbeFlags _last;
    private double _since;

    /// <summary>True at most once per <see cref="PollSeconds"/>; first call true.</summary>
    public bool IsPollDue(double now)
    {
        return _gate.IsDue(now);
    }

    /// <summary>True when flags differ from the last ones; held = how long the old ones lasted.</summary>
    public bool TryChange(HeadhunterProbeFlags flags, double now, out double heldSeconds)
    {
        heldSeconds = 0;
        if (flags == _last)
        {
            return false;
        }

        heldSeconds = now - _since;
        _last = flags;
        _since = now;
        return true;
    }

    /// <summary>Forgets the flags and restarts the held clock.</summary>
    public void Reset(double now)
    {
        _last = default;
        _since = now;
    }
}
