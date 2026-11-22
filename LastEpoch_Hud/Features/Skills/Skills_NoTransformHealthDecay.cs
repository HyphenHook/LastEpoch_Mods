using System;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Skills;
using LastEpoch_Hud.Scripts.ModUI;

namespace LastEpoch_Hud.Scripts.Mods.Skills;

/// <summary>Skips health decay drains that belong to the local player's transform form.</summary>
[HarmonyPatch(typeof(AcceleratingHealthDrain), nameof(AcceleratingHealthDrain.OnUpdateTick))]
internal static class Skills_NoTransformHealthDecay
{
    private static readonly FormDrainGate _gate = new();
    private static readonly Func<AcceleratingHealthDrain, FormDrainVerdict> _classifyFunc =
        Classify;

    public static void Reset() => _gate.Reset();

    [HarmonyPrefix]
    private static bool Prefix(AcceleratingHealthDrain __instance)
    {
        if (!ModSettings.TransformForms.NoHealthDecay.Value)
        {
            return true;
        }

        try
        {
            return !_gate.ShouldSkip(
                Scenes.SceneName,
                __instance.GetInstanceID(),
                __instance,
                _classifyFunc
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Skills_NoTransformHealthDecay");
            return true;
        }
    }

    private static FormDrainVerdict Classify(AcceleratingHealthDrain drain)
    {
        Actor player = Refs_Manager.player_actor;
        Actor actor = drain.actor;
        if (player.IsNullOrDestroyed() || actor.IsNullOrDestroyed())
        {
            return FormDrainVerdict.NotReady;
        }

        if (actor.Pointer != player.Pointer)
        {
            return FormDrainVerdict.Keep;
        }

        return IsFormDrain(drain) ? FormDrainVerdict.Skip : FormDrainVerdict.Keep;
    }

    private static bool IsFormDrain(AcceleratingHealthDrain drain)
    {
        bool isForm = !drain.GetComponentInParent<FormChanger>().IsNullOrDestroyed();
        if (ModSettings.Debug.Enabled.Value)
        {
            Main.logger_instance?.Msg(
                "[NoTransformHealthDecay] " + drain.gameObject.name + " form=" + isForm
            );
        }
        return isForm;
    }
}
