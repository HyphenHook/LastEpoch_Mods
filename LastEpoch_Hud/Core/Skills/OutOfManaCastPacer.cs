using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Skills;

/// <summary>
/// Paces casts the mod forces without enough mana, so they keep the character's own cast speed.
/// Forcing a cast skips the game's pacing, so the next one may only start once the previous
/// cast's animation has ended. A cast is recorded at its release point, partway into the
/// animation, so that is castDuration minus the skill's wind-up, measured from each forced
/// start to its release. With movement held, the next cast also waits until the character has
/// moved, or two cast lengths if they're blocked, so holding both alternates cast, move, cast.
/// </summary>
public sealed class OutOfManaCastPacer
{
    /// <summary>The largest wind-up, as a fraction of the cast's duration, that is trusted.</summary>
    public const float MaxWindupFraction = 0.8f;

    /// <summary>How far the character must move between casts while movement is held.</summary>
    public const float MinMoveBetweenCasts = 0.5f;

    private readonly Dictionary<long, float> _windupFraction = new();
    private float _lastCastTime = float.NegativeInfinity;
    private float _lastCastDuration;
    private float _forcedStarted;

    /// <summary>The skill's measured wind-up as a fraction of its cast duration (0 until measured).</summary>
    public float WindupFraction(long ability) =>
        _windupFraction.TryGetValue(ability, out float fraction) ? fraction : 0f;

    /// <summary>Whether a forced cast of the skill may start now.</summary>
    public bool IsReady(
        long ability,
        float now,
        bool midAnimation,
        bool movementHeld,
        float distanceMoved
    )
    {
        if (midAnimation)
        {
            return false;
        }
        float sinceCast = now - _lastCastTime;
        if (sinceCast < _lastCastDuration * (1f - WindupFraction(ability)))
        {
            return false;
        }
        return !movementHeld
            || distanceMoved >= MinMoveBetweenCasts
            || sinceCast >= _lastCastDuration * 2f;
    }

    /// <summary>The mod started a forced cast.</summary>
    public void ForcedCastStarted(float now) => _forcedStarted = now;

    /// <summary>
    /// A forced cast was released: the time since its start is the skill's wind-up. Call before
    /// <see cref="CastReleased"/>, so it's measured against the previous cast's duration. A value
    /// at or above that duration means the cast was held back, not wound up, so it's ignored.
    /// </summary>
    public void ForcedCastReleased(long ability, float now)
    {
        float windup = now - _forcedStarted;
        if (_lastCastDuration > 0f && windup >= 0f && windup < _lastCastDuration)
        {
            _windupFraction[ability] = Math.Min(windup / _lastCastDuration, MaxWindupFraction);
        }
    }

    /// <summary>Any player cast was released; later forced casts are paced from it.</summary>
    public void CastReleased(float now, float castDuration)
    {
        _lastCastTime = now;
        _lastCastDuration = castDuration;
    }
}
