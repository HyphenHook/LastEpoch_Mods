using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause;

/// <summary>Holds the latest cutscene until it stops or runs out.</summary>
public sealed class HeadhunterCutsceneWatch
{
    public const double FallbackSeconds = 30;
    public const double MaxSeconds = 120;
    public const double MarginSeconds = 2;
    public const double StartGraceSeconds = 1;

    private HeadhunterCutscene _cutscene;

    public bool IsActive { get; private set; }

    /// <summary>Seconds to hold before the margin: the duration, capped; the fallback when unknown.</summary>
    public static double HoldSeconds(double durationSeconds)
    {
        if (double.IsNaN(durationSeconds) || durationSeconds <= 0)
        {
            return FallbackSeconds;
        }

        return Math.Min(durationSeconds, MaxSeconds);
    }

    /// <summary>Holds this cutscene, replacing any earlier one.</summary>
    public void Start(string id, double durationSeconds, double now)
    {
        _cutscene = new HeadhunterCutscene(id, durationSeconds, now);
        IsActive = true;
    }

    /// <summary>Ends the hold when the game state or the deadline says so.</summary>
    public bool TryEnd(HeadhunterCutsceneState state, double now, out HeadhunterCutsceneStop stop)
    {
        stop = default;
        if (!IsActive || !TryCause(state, now, out HeadhunterCutsceneEnd by))
        {
            return false;
        }

        stop = Stop(now, by);
        IsActive = false;
        return true;
    }

    /// <summary>The stop that ending now would give, without ending.</summary>
    public bool TryPeek(double now, HeadhunterCutsceneEnd by, out HeadhunterCutsceneStop stop)
    {
        stop = default;
        if (!IsActive)
        {
            return false;
        }

        stop = Stop(now, by);
        return true;
    }

    public void Reset()
    {
        IsActive = false;
    }

    private HeadhunterCutsceneStop Stop(double now, HeadhunterCutsceneEnd by)
    {
        return new HeadhunterCutsceneStop(_cutscene, now - _cutscene.StartedAt, by);
    }

    private bool TryCause(HeadhunterCutsceneState state, double now, out HeadhunterCutsceneEnd by)
    {
        by = HeadhunterCutsceneEnd.Missing;
        if (state == HeadhunterCutsceneState.Missing)
        {
            return true;
        }

        double elapsed = now - _cutscene.StartedAt;
        by = HeadhunterCutsceneEnd.Stopped;
        if (state == HeadhunterCutsceneState.Stopped && elapsed >= StartGraceSeconds)
        {
            return true;
        }

        by = HeadhunterCutsceneEnd.Duration;
        return elapsed >= HoldSeconds(_cutscene.DurationSeconds) + MarginSeconds;
    }
}
