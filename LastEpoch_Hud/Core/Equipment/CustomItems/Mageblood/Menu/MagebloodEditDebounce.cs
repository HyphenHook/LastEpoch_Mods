namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>Holds the latest edit until the input has been quiet for a while.</summary>
public sealed class MagebloodEditDebounce
{
    private readonly double _quietSeconds;
    private MagebloodValueEdit _pending;
    private double _setAt;
    private bool _hasPending;

    public MagebloodEditDebounce(double quietSeconds)
    {
        _quietSeconds = quietSeconds;
    }

    /// <summary>True when <paramref name="flushFirst"/> is an older edit of another target to write now.</summary>
    public bool Set(MagebloodValueEdit edit, double now, out MagebloodValueEdit flushFirst)
    {
        flushFirst = _pending;
        bool flush = _hasPending && _pending.Target != edit.Target;
        _pending = edit;
        _setAt = now;
        _hasPending = true;
        return flush;
    }

    public bool TryPeek(MagebloodValueTarget target, out float fileValue)
    {
        fileValue = _pending.FileValue;
        return _hasPending && _pending.Target == target;
    }

    public bool TryTakeDue(double now, out MagebloodValueEdit edit)
    {
        edit = _pending;
        if (!_hasPending || now < _setAt + _quietSeconds)
        {
            return false;
        }

        _hasPending = false;
        return true;
    }

    /// <summary>Takes the pending edit now, before its quiet time is over.</summary>
    public bool TryTakeAny(out MagebloodValueEdit edit)
    {
        edit = _pending;
        bool had = _hasPending;
        _hasPending = false;
        return had;
    }

    public void Discard()
    {
        _hasPending = false;
    }
}
