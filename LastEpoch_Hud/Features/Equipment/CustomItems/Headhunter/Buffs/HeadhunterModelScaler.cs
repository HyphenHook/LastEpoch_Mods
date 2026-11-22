using System;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;
using LastEpoch_Hud.Scripts.ModUI;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

/// <summary>Scales the visible player model (human or shapeshift form) by distinct buff count and restores it.</summary>
internal static class HeadhunterModelScaler
{
    private static readonly HeadhunterSizeTracker _tracker = new();
    private static ActorVisuals _visuals;
    private static Transform _baseModel;
    private static Transform _applied;
    private static Vector3 _original;
    private static Vector3 _written;

    public static void Apply(int liveBuffs)
    {
        try
        {
            Step(liveBuffs);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter model size");
            Restore();
        }
    }

    public static void Restore()
    {
        try
        {
            if (_tracker.ShouldRestore(ScaleIntact()))
            {
                RestoreApplied();
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter model restore");
        }

        _tracker.Reset();
        _applied = null;
    }

    private static void Step(int liveBuffs)
    {
        float factor = HeadhunterConfigLoader.Current.ModelSize.Factor(liveBuffs);
        Transform model = CurrentModel(out HeadhunterModelSource source);
        HeadhunterSizeAction action = _tracker.Next(IdOf(model), factor, ScaleIntact());
        if (action == HeadhunterSizeAction.None)
        {
            return;
        }

        if (action == HeadhunterSizeAction.RestoreThenRescale)
        {
            RestoreApplied();
        }

        _applied = null;
        if (model.IsNullOrDestroyed() || factor == HeadhunterSizeCurve.NormalFactor)
        {
            return;
        }

        Scale(model, factor);
        Log(liveBuffs, factor, source);
    }

    private static Transform CurrentModel(out HeadhunterModelSource source)
    {
        source = HeadhunterModelSource.Base;
        ActorVisuals visuals = Refs_Manager.player_visuals;
        if (visuals.IsNullOrDestroyed())
        {
            return null;
        }

        Transform form = FormModel(visuals);
        source = HeadhunterModelSelector.Pick(visuals._isTransformed, !form.IsNullOrDestroyed());
        return source == HeadhunterModelSource.Form ? form : BaseModel(visuals);
    }

    private static Transform FormModel(ActorVisuals visuals)
    {
        VisualFormChanger changer = visuals.visualFormChangerManager?.currentVisualFormChanger;
        return changer.IsNullOrDestroyed() ? null : changer.transform;
    }

    private static Transform BaseModel(ActorVisuals visuals)
    {
        if (visuals == _visuals && !_baseModel.IsNullOrDestroyed())
        {
            return _baseModel;
        }

        _visuals = visuals;
        _baseModel = FindModel(visuals);
        return _baseModel;
    }

    private static Transform FindModel(ActorVisuals visuals)
    {
        ActorModelIndicator indicator = visuals.visualFormChangerManager?.actorModel;
        if (indicator.IsNullOrDestroyed())
        {
            indicator = visuals.GetComponentInChildren<ActorModelIndicator>(true);
        }

        return indicator.IsNullOrDestroyed() ? null : indicator.transform;
    }

    private static bool ScaleIntact()
    {
        if (_applied is null)
        {
            return true;
        }

        return !_applied.IsNullOrDestroyed() && _applied.localScale == _written;
    }

    private static void Scale(Transform model, float factor)
    {
        _original = model.localScale;
        _written = _original * factor;
        model.localScale = _written;
        _applied = model;
    }

    private static void RestoreApplied()
    {
        if (_applied.IsNullOrDestroyed())
        {
            return;
        }

        _applied.localScale = _original;
    }

    private static int IdOf(Transform model)
    {
        return model.IsNullOrDestroyed() ? HeadhunterSizeTracker.NoModel : model.GetInstanceID();
    }

    private static void Log(int buffs, float factor, HeadhunterModelSource source)
    {
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        Main.logger_instance?.Msg(
            "Headhunter size: "
                + buffs
                + " buff(s), x"
                + factor
                + ", "
                + source.ToString().ToLowerInvariant()
        );
    }
}
