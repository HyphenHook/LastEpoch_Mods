using System;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.ModUI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

/// <summary>Scales the local player's fixed-distance dashes with the Headhunter model size.</summary>
internal static class HeadhunterDash
{
    private static readonly HeadhunterDashScale _scale = new();
    private static bool _logged;

    public static void Sync(int liveBuffs)
    {
        _scale.Set(HeadhunterConfigLoader.Current.ModelSize.Factor(liveBuffs));
    }

    public static void Reset()
    {
        _scale.Reset();
    }

    public static void Apply(
        MovementFromAbility mover,
        AbilityMovement type,
        ref float fixedDistance,
        ref float maxDistance
    )
    {
        if (_scale.IsNormal)
        {
            return;
        }

        DashMovement kind = ToDash(type);
        if (!HeadhunterDashScale.Scales(kind))
        {
            return;
        }

        try
        {
            IntPtr instance = PointerOf(mover.actor);
            IntPtr player = PointerOf(Refs_Manager.player_actor);
            float scaled = _scale.DistanceFor(kind, instance, player, fixedDistance);
            maxDistance = _scale.DistanceFor(kind, instance, player, maxDistance);
            if (scaled == fixedDistance)
            {
                return;
            }

            fixedDistance = scaled;
            LogOnce();
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "HeadhunterDash");
        }
    }

    private static DashMovement ToDash(AbilityMovement type)
    {
        return type switch
        {
            AbilityMovement.FixedDistanceDash => DashMovement.FixedDistanceDash,
            AbilityMovement.FixedDistanceDashThenWait => DashMovement.FixedDistanceDashThenWait,
            AbilityMovement.FixedDistanceVariableSpeedDash =>
                DashMovement.FixedDistanceVariableSpeedDash,
            AbilityMovement.FixedDistanceDashThenWaitThenDash =>
                DashMovement.FixedDistanceDashThenWaitThenDash,
            AbilityMovement.FixedDistanceLeap => DashMovement.FixedDistanceLeap,
            _ => DashMovement.None,
        };
    }

    private static void LogOnce()
    {
        if (_logged)
        {
            return;
        }

        _logged = true;
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        Main.logger_instance?.Msg("HH dash patch fired (factor " + _scale.Factor + ")");
    }

    private static IntPtr PointerOf(Il2CppObjectBase o)
    {
        return o is null ? IntPtr.Zero : o.Pointer;
    }
}
