namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause.RewardMenu;

/// <summary>Holds the latest reward panel until it closes, hides, vanishes or hits the cap.</summary>
public sealed class HeadhunterRewardMenuWatch
{
    public const double StartGraceSeconds = 1;
    public const double MaxHoldSeconds = 300;

    private HeadhunterRewardMenu _menu;

    public bool IsActive { get; private set; }

    /// <summary>Holds this panel, replacing any earlier one.</summary>
    public void Start(long id, string panel, double now)
    {
        _menu = new HeadhunterRewardMenu(id, panel, now);
        IsActive = true;
    }

    /// <summary>Ends the hold on the game's close of the held panel.</summary>
    public bool TryClose(long id, double now, out HeadhunterRewardMenuStop stop)
    {
        stop = default;
        if (!IsActive || _menu.Id != id)
        {
            return false;
        }

        stop = Finish(now, HeadhunterRewardMenuEnd.Closed);
        return true;
    }

    /// <summary>Ends the hold when the polled state or the cap says so.</summary>
    public bool TryEnd(
        HeadhunterRewardMenuState state,
        double now,
        out HeadhunterRewardMenuStop stop
    )
    {
        stop = default;
        if (!IsActive || !TryCause(state, now, out HeadhunterRewardMenuEnd by))
        {
            return false;
        }

        stop = Finish(now, by);
        return true;
    }

    /// <summary>The stop that ending now would give, without ending.</summary>
    public bool TryPeek(double now, HeadhunterRewardMenuEnd by, out HeadhunterRewardMenuStop stop)
    {
        stop = default;
        if (!IsActive)
        {
            return false;
        }

        stop = new HeadhunterRewardMenuStop(_menu, now - _menu.OpenedAt, by);
        return true;
    }

    public void Reset()
    {
        IsActive = false;
    }

    private HeadhunterRewardMenuStop Finish(double now, HeadhunterRewardMenuEnd by)
    {
        IsActive = false;
        return new HeadhunterRewardMenuStop(_menu, now - _menu.OpenedAt, by);
    }

    private bool TryCause(
        HeadhunterRewardMenuState state,
        double now,
        out HeadhunterRewardMenuEnd by
    )
    {
        by = HeadhunterRewardMenuEnd.Missing;
        if (state == HeadhunterRewardMenuState.Missing)
        {
            return true;
        }

        double elapsed = now - _menu.OpenedAt;
        by = HeadhunterRewardMenuEnd.Hidden;
        if (state == HeadhunterRewardMenuState.Hidden && elapsed >= StartGraceSeconds)
        {
            return true;
        }

        by = HeadhunterRewardMenuEnd.Cap;
        return elapsed >= MaxHoldSeconds;
    }
}
