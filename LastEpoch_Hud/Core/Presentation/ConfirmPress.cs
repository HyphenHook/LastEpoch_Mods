namespace LastEpoch_Hud.Scripts.Core.ModUI;

/// <summary>Two-press confirm: the first press arms, a second one inside the window confirms.</summary>
public sealed class ConfirmPress
{
    private readonly double _windowSeconds;
    private double _armedAt;
    private bool _armed;

    public ConfirmPress(double windowSeconds)
    {
        _windowSeconds = windowSeconds;
    }

    /// <summary>True when this press confirms.</summary>
    public bool Press(double now)
    {
        if (IsArmed(now))
        {
            Reset();
            return true;
        }

        _armed = true;
        _armedAt = now;
        return false;
    }

    public bool IsArmed(double now)
    {
        return _armed && now < _armedAt + _windowSeconds;
    }

    public void Reset()
    {
        _armed = false;
    }
}
