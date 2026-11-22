using System;
using Il2CppInterop.Runtime;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;

/// <summary>Loads the two buff bar prefabs from the HUD bundle, again if they get destroyed.</summary>
internal static class HeadhunterBuffBarAssets
{
    private const string BarSuffix = "headhunter/prefab/buffs.prefab";
    private const string EntrySuffix = "headhunter/prefab/buff.prefab";
    private static readonly HeadhunterAssetLoadGate _gate = new();

    public static GameObject BarPrefab { get; private set; }
    public static GameObject EntryPrefab { get; private set; }

    public static bool TryLoad()
    {
        if (PrefabsAlive())
        {
            return true;
        }

        long bundleId = BundleId();
        if (!_gate.ShouldLoad(bundleId))
        {
            return false;
        }

        LoadAll();
        if (PrefabsAlive())
        {
            return true;
        }

        _gate.MarkMissing(bundleId);
        Main.logger_instance?.Warning("Headhunter buff bar prefabs missing in the bundle");
        return false;
    }

    private static bool PrefabsAlive()
    {
        return !BarPrefab.IsNullOrDestroyed() && !EntryPrefab.IsNullOrDestroyed();
    }

    private static long BundleId()
    {
        if (Hud_Manager.asset_bundle.IsNullOrDestroyed())
        {
            return 0;
        }

        return Hud_Manager.asset_bundle.Pointer.ToInt64();
    }

    private static void LoadAll()
    {
        foreach (string name in Hud_Manager.asset_bundle.GetAllAssetNames())
        {
            string lower = name.Replace('\\', '/').ToLowerInvariant();
            if (lower.EndsWith(BarSuffix, StringComparison.Ordinal))
            {
                BarPrefab = Load(name);
            }

            if (lower.EndsWith(EntrySuffix, StringComparison.Ordinal))
            {
                EntryPrefab = Load(name);
            }
        }
    }

    private static GameObject Load(string name)
    {
        GameObject prefab = Hud_Manager
            .asset_bundle.LoadAsset(name, Il2CppType.Of<GameObject>())
            ?.TryCast<GameObject>();
        if (!prefab.IsNullOrDestroyed())
        {
            prefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }

        return prefab;
    }
}
