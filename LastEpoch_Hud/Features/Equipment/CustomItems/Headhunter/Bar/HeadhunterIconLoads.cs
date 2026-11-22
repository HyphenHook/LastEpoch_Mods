using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppLE.AssetManagement;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.ModUI;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;

/// <summary>Owns one non-blocking game icon load per stat key; failed keys wait for the next scene.</summary>
internal static class HeadhunterIconLoads
{
    private static readonly Dictionary<HeadhunterStatKey, LoadRef<Sprite>> _loads = new();
    private static readonly HeadhunterIconLoadGate _gate = new();

    /// <summary>Lets failed loads start once more (call on every scene load).</summary>
    public static void AllowRetry()
    {
        _gate.AllowRetry();
    }

    /// <summary>Game icon for a stat, or null while pending or missing. settled is true once the answer is final.</summary>
    public static Sprite Poll(HeadhunterStatKey key, out bool settled)
    {
        settled = false;
        if (!GlobalAssets.NodeTooltipIconListAvailable)
        {
            return null;
        }

        try
        {
            return Step(key, out settled);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter buff icon");
            _gate.MarkFailed(key);
            DropLoad(key);
            return null;
        }
    }

    private static Sprite Step(HeadhunterStatKey key, out bool settled)
    {
        settled = !NodeTooltipIconList.hasSpriteForPropertyAndTags(
            (ushort)key.StatId,
            (AT)key.Tags
        );
        if (settled)
        {
            return null;
        }

        HeadhunterIconStep step = _gate.Next(key, StatusOf(key));
        if (step == HeadhunterIconStep.Start)
        {
            StartLoad(key);
            return null;
        }

        if (step == HeadhunterIconStep.Drop)
        {
            DropLoad(key);
            LogFailed(key);
            return null;
        }

        if (step != HeadhunterIconStep.Use)
        {
            return null;
        }

        settled = true;
        return _loads[key].AssetOrNull;
    }

    private static HeadhunterIconLoadStatus StatusOf(HeadhunterStatKey key)
    {
        if (!_loads.TryGetValue(key, out LoadRef<Sprite> load))
        {
            return HeadhunterIconLoadStatus.None;
        }

        AssetStatus status = load.Status;
        if (status == AssetStatus.Loading)
        {
            return HeadhunterIconLoadStatus.Loading;
        }

        bool usable = status == AssetStatus.Loaded && !load.AssetOrNull.IsNullOrDestroyed();
        return usable ? HeadhunterIconLoadStatus.Loaded : HeadhunterIconLoadStatus.Failed;
    }

    private static void StartLoad(HeadhunterStatKey key)
    {
        SoftRef<Sprite> softRef = NodeTooltipIconList.getSprite((ushort)key.StatId, (AT)key.Tags);
        LoadRef<Sprite> load =
            softRef == null || !softRef
                ? null
                : SoftRefExtensions.CreateLoadRef(softRef, "LastEpoch_Hud", 0);
        if (load == null)
        {
            _gate.MarkFailed(key);
            return;
        }

        _loads[key] = load;
    }

    private static void DropLoad(HeadhunterStatKey key)
    {
        if (_loads.Remove(key, out LoadRef<Sprite> load))
        {
            load.Dispose();
        }
    }

    private static void LogFailed(HeadhunterStatKey key)
    {
        if (ModSettings.Debug.Enabled.Value)
        {
            Main.logger_instance?.Msg("Headhunter icon load failed " + key.StatId + "/" + key.Tags);
        }
    }
}
