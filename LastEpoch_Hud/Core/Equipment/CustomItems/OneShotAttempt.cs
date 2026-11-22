namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>State of a run-once task that may retry until it succeeds.</summary>
public sealed class OneShotAttempt
{
    private bool _failureReported;

    public bool IsDone { get; private set; }

    public void MarkDone()
    {
        IsDone = true;
    }

    /// <summary>True only the first time, so a repeating failure is logged once.</summary>
    public bool ShouldReportFailure()
    {
        if (_failureReported)
        {
            return false;
        }

        _failureReported = true;
        return true;
    }
}
