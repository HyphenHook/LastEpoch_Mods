using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Decides the local player's fixed dash distance from the Headhunter model size.</summary>
public sealed class HeadhunterDashScale
{
    private readonly HeadhunterReachScale _size = new();

    public float Factor => _size.Factor;

    public bool IsNormal => _size.IsNormal;

    public void Set(float factor)
    {
        _size.Set(factor);
    }

    public void Reset()
    {
        _size.Reset();
    }

    public static bool Scales(DashMovement movement)
    {
        return movement switch
        {
            DashMovement.FixedDistanceDash => true,
            DashMovement.FixedDistanceDashThenWait => true,
            DashMovement.FixedDistanceVariableSpeedDash => true,
            DashMovement.FixedDistanceDashThenWaitThenDash => true,
            DashMovement.FixedDistanceLeap => true,
            _ => false,
        };
    }

    public float DistanceFor(DashMovement movement, IntPtr instance, IntPtr player, float distance)
    {
        if (!Scales(movement))
        {
            return distance;
        }

        if (!(distance > 0f && distance < float.MaxValue))
        {
            return distance;
        }

        return _size.ScaleFor(instance, player, distance);
    }
}
