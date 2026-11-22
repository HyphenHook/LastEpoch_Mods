using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Bar;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Bar;

/// <summary>Icon of one flask: bundle flask icon, then a Headhunter buff icon, then the item icon.</summary>
internal static class MagebloodFlaskIcons
{
    private static readonly Dictionary<string, string> _assets = new();
    private static readonly Dictionary<string, (Sprite Sprite, Texture2D Texture)> _loaded = new();
    private static long _bundleId;

    /// <summary>The flask icon; null when nothing in the chain is available yet.</summary>
    public static Sprite For(MagebloodFlask flask)
    {
        IReadOnlyList<MagebloodIconSource> chain = MagebloodIconChain.For(
            flask.IconName,
            flask.Name,
            StatNames(flask)
        );
        foreach (MagebloodIconSource step in chain)
        {
            Sprite sprite = Step(step);
            if (!sprite.IsNullOrDestroyed())
            {
                return sprite;
            }
        }

        return null;
    }

    private static Sprite Step(MagebloodIconSource step)
    {
        return step.Kind switch
        {
            MagebloodIconSourceKind.FlaskIcon => FromBundle(step.Name),
            MagebloodIconSourceKind.BuffIcon => HeadhunterBuffIcons.FromBar(step.Name),
            _ => ItemIcon(),
        };
    }

    private static Sprite FromBundle(string icon)
    {
        EnsureAssetMap();
        string key = MagebloodFlaskIconAssets.Key(icon);
        if (key == null || !_assets.TryGetValue(key, out string assetName))
        {
            return null;
        }

        if (
            _loaded.TryGetValue(key, out (Sprite Sprite, Texture2D Texture) cached)
            && IsAlive(cached)
        )
        {
            return cached.Sprite;
        }

        Sprite sprite = CustomItemIcons.LoadBundleSprite(assetName, out Texture2D texture);
        _loaded[key] = (sprite, texture);
        return sprite;
    }

    private static bool IsAlive((Sprite Sprite, Texture2D Texture) loaded)
    {
        if (loaded.Sprite.IsNullOrDestroyed())
        {
            return false;
        }

        return loaded.Texture == null || !loaded.Texture.IsNullOrDestroyed();
    }

    private static void EnsureAssetMap()
    {
        long bundleId = BundleId();
        if (bundleId == _bundleId)
        {
            return;
        }

        _bundleId = bundleId;
        _assets.Clear();
        _loaded.Clear();
        if (bundleId == 0)
        {
            return;
        }

        foreach (string name in AssetNames())
        {
            if (MagebloodFlaskIconAssets.TryIconName(name, out string icon))
            {
                _assets[icon] = name;
            }
        }
    }

    private static long BundleId()
    {
        if (Hud_Manager.asset_bundle.IsNullOrDestroyed())
        {
            return 0;
        }

        return Hud_Manager.asset_bundle.Pointer.ToInt64();
    }

    private static string[] AssetNames()
    {
        try
        {
            return Hud_Manager.asset_bundle.GetAllAssetNames();
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Mageblood flask icons");
            return Array.Empty<string>();
        }
    }

    private static Sprite ItemIcon()
    {
        int index = CustomUniqueLookup.IndexOf(CustomUniqueSpecs.Mageblood.UniqueId);
        return index < 0 ? null : CustomItemIcons.Get(index);
    }

    private static List<string> StatNames(MagebloodFlask flask)
    {
        var names = new List<string>(flask.Stats.Count);
        foreach (MagebloodBuffStat stat in flask.Stats)
        {
            names.Add(HeadhunterStatNames.EnumName(stat.StatId));
        }

        return names;
    }
}
