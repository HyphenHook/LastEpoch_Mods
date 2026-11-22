using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppLE.Services.Cosmetics;
using LastEpoch_Hud.Scripts.Core.Cosmetics;

namespace LastEpoch_Hud.Scripts.Mods.Cosmetics;

/// <summary>Reads the game's cosmetics registry into a Core catalog.</summary>
internal static class CosmeticRegistryReader
{
    public static CosmeticCatalog Read(CosmeticResources registry)
    {
        var catalog = new CosmeticCatalog();
        LEAssetBundlesCatalog bundles = CosmeticStoreIcon.CurrentBundles();
        if (bundles.IsNullOrDestroyed())
        {
            Main.logger_instance?.Msg(
                "Offline cosmetics: asset bundle catalog not ready, store icon check uses GUID only"
            );
        }
        AddList(registry.ItemCosmetics, catalog, bundles);
        AddList(registry.AbilityCosmetics, catalog, bundles);
        AddList(registry.BackSlotCosmetics, catalog, bundles);
        AddList(registry.PortalCosmetics, catalog, bundles);
        AddList(registry.PetCosmetics, catalog, bundles);
        AddList(registry.PetEffectCosmetics, catalog, bundles);
        AddList(registry.PortraitCosmetics, catalog, bundles);
        AddList(registry.CharacterTitleCosmetics, catalog, bundles);
        AddList(registry.FootprintCosmetics, catalog, bundles);
        AddList(registry.CharacterFXCosmetics, catalog, bundles);
        AddList(registry.StashCosmetics, catalog, bundles);
        AddList(registry.CosmeticBundles, catalog, bundles);
        AddBundleContents(registry.CosmeticBundles, catalog, bundles);
        AddLookupPairs(registry.CosmeticLookupPairs, catalog, bundles);
        return catalog;
    }

    private static void AddList<T>(
        Il2CppSystem.Collections.Generic.List<T> list,
        CosmeticCatalog catalog,
        LEAssetBundlesCatalog bundles
    )
        where T : Cosmetic
    {
        if (list.IsNullOrDestroyed())
        {
            return;
        }
        for (int i = 0; i < list.Count; i++)
        {
            T cosmetic = list[i];
            if (cosmetic.IsNullOrDestroyed())
            {
                continue;
            }
            catalog.Add(
                cosmetic.BackendID,
                cosmetic.DisplayName,
                CosmeticStoreIcon.HasStoreIcon(cosmetic, bundles),
                CosmeticStoreIcon.IsTitle(cosmetic)
            );
        }
    }

    private static void AddBundleContents(
        Il2CppSystem.Collections.Generic.List<CosmeticBundle> cosmeticBundles,
        CosmeticCatalog catalog,
        LEAssetBundlesCatalog bundles
    )
    {
        if (cosmeticBundles.IsNullOrDestroyed())
        {
            return;
        }
        for (int i = 0; i < cosmeticBundles.Count; i++)
        {
            CosmeticBundle bundle = cosmeticBundles[i];
            if (bundle.IsNullOrDestroyed())
            {
                continue;
            }
            AddList(bundle.bundledCosmetics, catalog, bundles);
        }
    }

    private static void AddLookupPairs(
        Il2CppSystem.Collections.Generic.Dictionary<string, Cosmetic> pairs,
        CosmeticCatalog catalog,
        LEAssetBundlesCatalog bundles
    )
    {
        if (pairs.IsNullOrDestroyed())
        {
            return;
        }
        foreach (Il2CppSystem.Collections.Generic.KeyValuePair<string, Cosmetic> pair in pairs)
        {
            catalog.Add(
                pair.Key,
                DisplayNameOf(pair.Value),
                HasStoreIcon(pair.Value, bundles),
                IsTitle(pair.Value)
            );
        }
    }

    private static string DisplayNameOf(Cosmetic cosmetic) =>
        cosmetic.IsNullOrDestroyed() ? null : cosmetic.DisplayName;

    private static bool HasStoreIcon(Cosmetic cosmetic, LEAssetBundlesCatalog bundles) =>
        !cosmetic.IsNullOrDestroyed() && CosmeticStoreIcon.HasStoreIcon(cosmetic, bundles);

    private static bool IsTitle(Cosmetic cosmetic) =>
        !cosmetic.IsNullOrDestroyed() && CosmeticStoreIcon.IsTitle(cosmetic);
}
