namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause;

/// <summary>Watches zone-arrival protection after a hostile load.</summary>
public sealed class HeadhunterArrivalWatch
{
    private double _startedAt;

    public bool IsWatching { get; private set; }

    public string Scene { get; private set; } = string.Empty;

    /// <summary>Starts watching after a hostile load, stops after a non-combat one. Returns whether arrival protection is assumed.</summary>
    public bool Begin(string scene, bool nonCombat, double now)
    {
        IsWatching = !nonCombat;
        if (nonCombat)
        {
            return false;
        }

        Scene = scene;
        _startedAt = now;
        return true;
    }

    /// <summary>Stops watching once the player is not protected (or gone).</summary>
    public bool TryEnd(HeadhunterArrivalState state, double now, out double heldSeconds)
    {
        heldSeconds = 0;
        if (!IsWatching || state == HeadhunterArrivalState.Protected)
        {
            return false;
        }

        heldSeconds = now - _startedAt;
        IsWatching = false;
        return true;
    }

    /// <summary>Stops watching; returns whether it was watching.</summary>
    public bool Cancel()
    {
        bool was = IsWatching;
        IsWatching = false;
        return was;
    }
}
