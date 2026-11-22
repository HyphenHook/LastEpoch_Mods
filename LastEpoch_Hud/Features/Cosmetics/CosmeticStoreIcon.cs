using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppLE.Networking.Cosmetics;
using Il2CppLE.Services.Cosmetics;
using LastEpoch_Hud.Scripts.Core.Cosmetics;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Cosmetics;

/// <summary>Reads the game state that tells a released cosmetic (store icon) from a dev leftover.</summary>
internal static class CosmeticStoreIcon
{
    /// <summary>The asset bundle catalog, or null when the game has not built it yet.</summary>
    public static LEAssetBundlesCatalog CurrentBundles()
    {
        AssetBundlesManager manager = AssetBundlesManager.InstanceOrNull;
        return manager.IsNullOrDestroyed() ? null : AssetBundlesManager.Catalog;
    }

    /// <summary>True when the store icon reference points at a known asset; unknown bundles give true.</summary>
    public static bool HasStoreIcon(Cosmetic c, LEAssetBundlesCatalog bundles)
    {
        SoftRef<Sprite> soft = c.StoreSpriteSoftRef;
        bool hasSoftRef = !soft.IsNullOrDestroyed();
        bool guidValid = hasSoftRef && soft.Guid.IsValid();
        bool bundlesKnown = !bundles.IsNullOrDestroyed();
        bool found =
            guidValid && bundlesKnown && bundles.TryGetAssetBundleId(soft.Guid, out BundleId _);
        return CosmeticStoreIconRules.HasStoreIcon(hasSoftRef, guidValid, bundlesKnown, found);
    }

    public static bool IsTitle(Cosmetic c) => c.slot == CosmeticEquipSlot.CharacterTitle;
}
