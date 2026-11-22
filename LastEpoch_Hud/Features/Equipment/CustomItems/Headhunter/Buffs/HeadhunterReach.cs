using System;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;
using LastEpoch_Hud.Scripts.ModUI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

/// <summary>Scales the local player's weapon range with the Headhunter model size.</summary>
internal static class HeadhunterReach
{
    private static readonly HeadhunterReachScale _scale = new();
    private static bool _actorLogged;
    private static bool _visualsLogged;

    public static void Sync(int liveBuffs)
    {
        _scale.Set(HeadhunterConfigLoader.Current.ModelSize.Factor(liveBuffs));
    }

    public static void Reset()
    {
        _scale.Reset();
    }

    public static void ForActor(Actor actor, ref float range)
    {
        if (_scale.IsNormal)
        {
            return;
        }

        ScaleIfPlayer(
            PointerOf(actor),
            PointerOf(Refs_Manager.player_actor),
            ref range,
            ref _actorLogged,
            "Actor"
        );
    }

    public static void ForVisuals(ActorVisuals visuals, ref float range)
    {
        if (_scale.IsNormal)
        {
            return;
        }

        ScaleIfPlayer(
            PointerOf(visuals),
            PointerOf(Refs_Manager.player_visuals),
            ref range,
            ref _visualsLogged,
            "ActorVisuals"
        );
    }

    private static void ScaleIfPlayer(
        IntPtr instance,
        IntPtr player,
        ref float range,
        ref bool logged,
        string target
    )
    {
        float scaled = _scale.ScaleFor(instance, player, range);
        if (scaled == range)
        {
            return;
        }

        range = scaled;
        if (logged)
        {
            return;
        }

        logged = true;
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        Main.logger_instance?.Msg(
            "HH reach patch fired: " + target + " (factor " + _scale.Factor + ")"
        );
    }

    private static IntPtr PointerOf(Il2CppObjectBase o)
    {
        return o is null ? IntPtr.Zero : o.Pointer;
    }
}
