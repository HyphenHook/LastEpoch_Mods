namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause;

/// <summary>Tracks whether a cinematic plays and since when.</summary>
public sealed class HeadhunterCinematicWatch
{
    private double _startedAt;

    public bool IsActive { get; private set; }

    /// <summary>Records a start or end. True when the flag changed; held is the cinematic length on an end.</summary>
    public bool TryChange(bool active, double now, out double heldSeconds)
    {
        heldSeconds = 0;
        if (active == IsActive)
        {
            return false;
        }

        IsActive = active;
        if (active)
        {
            _startedAt = now;
            return true;
        }

        heldSeconds = now - _startedAt;
        return true;
    }

    /// <summary>Forgets a running cinematic.</summary>
    public void Reset()
    {
        IsActive = false;
    }
}
