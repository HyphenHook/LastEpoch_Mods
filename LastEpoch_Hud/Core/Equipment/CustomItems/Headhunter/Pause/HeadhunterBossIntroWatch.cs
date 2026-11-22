using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause;

/// <summary>Tracks long boss intros in progress.</summary>
public sealed class HeadhunterBossIntroWatch
{
    public const float MinSeconds = 5f;
    public const double ExpiryMarginSeconds = 2;

    private readonly List<HeadhunterBossIntro> _intros = new();

    public bool IsActive => _intros.Count > 0;

    public static bool IsLong(float durationSeconds)
    {
        return durationSeconds >= MinSeconds;
    }

    /// <summary>Tracks a long intro, replacing the same id; false for short ones.</summary>
    public bool TryStart(long id, string actor, float durationSeconds, double now)
    {
        if (!IsLong(durationSeconds))
        {
            return false;
        }

        RemoveAt(IndexOf(id));
        _intros.Add(new HeadhunterBossIntro(id, actor, durationSeconds, now));
        return true;
    }

    /// <summary>Stops tracking an intro whose emerge ended.</summary>
    public bool TryEnd(long id, double now, out HeadhunterBossIntro intro, out double heldSeconds)
    {
        return TryTake(IndexOf(id), now, out intro, out heldSeconds);
    }

    /// <summary>Removes one intro past its duration plus the margin.</summary>
    public bool TryExpire(double now, out HeadhunterBossIntro intro, out double heldSeconds)
    {
        for (int i = 0; i < _intros.Count; i++)
        {
            HeadhunterBossIntro candidate = _intros[i];
            double deadline = candidate.StartedAt + candidate.DurationSeconds + ExpiryMarginSeconds;
            if (now >= deadline)
            {
                return TryTake(i, now, out intro, out heldSeconds);
            }
        }

        intro = default;
        heldSeconds = 0;
        return false;
    }

    public void Reset()
    {
        _intros.Clear();
    }

    private bool TryTake(int index, double now, out HeadhunterBossIntro intro, out double held)
    {
        intro = default;
        held = 0;
        if (index < 0)
        {
            return false;
        }

        intro = _intros[index];
        held = now - intro.StartedAt;
        _intros.RemoveAt(index);
        return true;
    }

    private void RemoveAt(int index)
    {
        if (index >= 0)
        {
            _intros.RemoveAt(index);
        }
    }

    private int IndexOf(long id)
    {
        for (int i = 0; i < _intros.Count; i++)
        {
            if (_intros[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}
