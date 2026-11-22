namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Says when a periodic check is due.</summary>
public sealed class IntervalGate
{
    private readonly double _intervalSeconds;
    private double _next;

    public IntervalGate(double intervalSeconds)
    {
        _intervalSeconds = intervalSeconds;
    }

    public bool IsDue(double now)
    {
        if (now < _next)
        {
            return false;
        }

        _next = now + _intervalSeconds;
        return true;
    }
}
