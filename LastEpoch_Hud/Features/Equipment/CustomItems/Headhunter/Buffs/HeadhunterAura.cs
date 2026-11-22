using System;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;
using LastEpoch_Hud.Scripts.ModUI;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

/// <summary>Tints the player with the rarity glow by distinct Headhunter buff count and restores the look.</summary>
internal static class HeadhunterAura
{
    private static readonly HeadhunterAuraTracker _tracker = new();
    private static HeadhunterRendererSnapshot _snapshot;

    public static void Sync(int liveBuffs)
    {
        try
        {
            Step(liveBuffs);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter aura");
            Remove();
        }
    }

    public static void Remove()
    {
        try
        {
            RestoreSnapshot();
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter aura restore");
        }

        _tracker.Reset();
    }

    private static void Step(int liveBuffs)
    {
        float strength = HeadhunterConfigLoader.Current.Aura.Strength(liveBuffs);
        RendererManager manager = null;
        MonsterRarityColorSettings rare = null;
        if (strength > HeadhunterAuraCurve.Off)
        {
            manager = PlayerRenderers();
            rare = MaterialList.getRareMonsterRaritySettings();
        }

        bool ready = !manager.IsNullOrDestroyed() && !rare.IsNullOrDestroyed();
        int key = ready ? KeyOf(manager) : 0;
        HeadhunterAuraAction action = _tracker.Next(strength, ready, key);
        if (action == HeadhunterAuraAction.None)
        {
            return;
        }

        Run(action, manager, rare, strength);
    }

    private static void Run(
        HeadhunterAuraAction action,
        RendererManager manager,
        MonsterRarityColorSettings rare,
        float strength
    )
    {
        if (action is HeadhunterAuraAction.Remove or HeadhunterAuraAction.Rebuild)
        {
            RestoreSnapshot();
        }

        if (action == HeadhunterAuraAction.Remove)
        {
            Log("Headhunter aura: off");
            return;
        }

        if (action is HeadhunterAuraAction.Apply or HeadhunterAuraAction.Rebuild)
        {
            _snapshot = HeadhunterRendererSnapshot.Capture(manager);
        }

        Tint(manager, rare, strength);
        _snapshot.MarkApplied();
        LogOn(strength);
    }

    private static RendererManager PlayerRenderers()
    {
        ActorVisuals visuals = Refs_Manager.player_visuals;
        if (visuals.IsNullOrDestroyed())
        {
            return null;
        }

        RendererManager manager = visuals.rendererManager;
        return manager.IsNullOrDestroyed() ? null : manager;
    }

    private static int KeyOf(RendererManager manager)
    {
        var key = new HashCode();
        key.Add(manager.GetInstanceID());
        foreach (Renderer renderer in manager.renderers)
        {
            key.Add(renderer.IsNullOrDestroyed() ? 0 : renderer.GetInstanceID());
        }

        return key.ToHashCode();
    }

    private static void Tint(
        RendererManager manager,
        MonsterRarityColorSettings rare,
        float strength
    )
    {
        var settings = new MonsterRarityColorSettings
        {
            color = rare.color * strength,
            bias = rare.bias,
            power = rare.power,
            normalInfluence = rare.normalInfluence,
        };
        manager.ApplyMonsterRarityPropertyBlock(settings);
    }

    private static void RestoreSnapshot()
    {
        HeadhunterRendererSnapshot snapshot = _snapshot;
        _snapshot = null;
        snapshot?.Restore();
    }

    private static void LogOn(float strength)
    {
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        Log(
            "Headhunter aura: on "
                + strength
                + " renderers="
                + _snapshot.Count
                + " rarityFieldNull="
                + _snapshot.RarityWasNull
                + " sameRefAfterTint="
                + _snapshot.RarityChangedInPlace
                + " emptyBefore="
                + _snapshot.RarityEmptyBefore
        );
    }

    private static void Log(string message)
    {
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        Main.logger_instance?.Msg(message);
    }
}
