namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Says when to refresh: right after a change was flagged, or on a regular poll.</summary>
public sealed class RefreshGate
{
    private readonly IntervalGate _poll;
    private bool _dirty;

    public RefreshGate(double intervalSeconds)
    {
        _poll = new IntervalGate(intervalSeconds);
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    public bool ShouldRefresh(double now)
    {
        if (_dirty)
        {
            _dirty = false;
            return true;
        }

        return _poll.IsDue(now);
    }
}
