using System;

namespace LastEpoch_Hud.Scripts.Core.QualityOfLife;

public enum SceneTravelPhase
{
    Idle,
    Loading,
    Placing,
    UnloadingSource,
    Recovering,
}

// The source scene can only be retired after a successful, verified placement.
public sealed class SceneTravelProgress
{
    public SceneTravelPhase Phase { get; private set; }
    public string Source { get; private set; } = "";
    public string Target { get; private set; } = "";
    public string Failure { get; private set; } = "";
    public bool Busy => Phase != SceneTravelPhase.Idle;
    public bool CanUnloadSource => Phase == SceneTravelPhase.UnloadingSource;
    double phaseStarted;

    public bool Begin(string source, string target, double now)
    {
        if (
            Busy
            || !double.IsFinite(now)
            || !TravelSceneRules.IsSceneName(source)
            || !TravelSceneRules.IsDestination(target)
            || string.Equals(source, target, StringComparison.OrdinalIgnoreCase)
        )
            return false;
        Source = source;
        Target = target;
        Failure = "";
        SetPhase(SceneTravelPhase.Loading, now);
        return true;
    }

    public bool Loaded(double now)
    {
        if (Phase != SceneTravelPhase.Loading || !double.IsFinite(now))
            return false;
        SetPhase(SceneTravelPhase.Placing, now);
        return true;
    }

    // A rejected load has no async operation to finish. Only release the guard when
    // the caller verifies that the source is intact and no destination was loaded.
    public bool LoadRejected(string reason, bool sourceRetained, bool targetAbsent)
    {
        if (Phase != SceneTravelPhase.Loading || !sourceRetained || !targetAbsent)
            return false;
        Failure = reason ?? "Scene loading did not start";
        Phase = SceneTravelPhase.Idle;
        return true;
    }

    public bool Placed(bool success, double now)
    {
        if (Phase != SceneTravelPhase.Placing || !success || !double.IsFinite(now))
            return false;
        SetPhase(SceneTravelPhase.UnloadingSource, now);
        return true;
    }

    public bool Expired(double now, double timeout)
    {
        return Busy
            && double.IsFinite(now)
            && double.IsFinite(timeout)
            && timeout > 0
            && now >= phaseStarted
            && now - phaseStarted >= timeout;
    }

    public bool Recover(string reason, double now)
    {
        // An accepted source unload cannot be cancelled by Unity. Finish observing it.
        if (
            !double.IsFinite(now)
            || (Phase != SceneTravelPhase.Loading && Phase != SceneTravelPhase.Placing)
        )
            return false;
        Failure = reason ?? "Travel failed";
        SetPhase(SceneTravelPhase.Recovering, now);
        return true;
    }

    public bool Recovered(bool sourceRestored, bool targetCleaned)
    {
        if (Phase != SceneTravelPhase.Recovering || !sourceRestored || !targetCleaned)
            return false;
        Phase = SceneTravelPhase.Idle;
        return true;
    }

    public bool Completed(bool sourceUnloaded)
    {
        if (!CanUnloadSource || !sourceUnloaded)
            return false;
        Phase = SceneTravelPhase.Idle;
        return true;
    }

    void SetPhase(SceneTravelPhase phase, double now)
    {
        Phase = phase;
        phaseStarted = now;
    }
}
