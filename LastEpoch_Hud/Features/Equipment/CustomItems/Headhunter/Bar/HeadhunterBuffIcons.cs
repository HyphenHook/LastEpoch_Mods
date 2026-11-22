using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;

/// <summary>Icon per buffed stat: bundle icon, then game stat icon, then the item icon.</summary>
internal static class HeadhunterBuffIcons
{
    private static readonly Dictionary<HeadhunterStatKey, Sprite> _sprites = new();

    /// <summary>Icon for a stat. isFinal is false while a placeholder stands in for a pending load.</summary>
    public static Sprite For(int statId, int tags, out bool isFinal)
    {
        var key = new HeadhunterStatKey(statId, tags);
        if (_sprites.TryGetValue(key, out Sprite cached) && !cached.IsNullOrDestroyed())
        {
            isFinal = true;
            return cached;
        }

        Sprite sprite = Resolve(key, out isFinal);
        if (isFinal)
        {
            _sprites[key] = sprite;
        }

        return sprite;
    }

    /// <summary>Icon of the bar prefab child with this name; null when the prefab or child is missing.</summary>
    internal static Sprite FromBar(string childName)
    {
        GameObject prefab = HeadhunterBuffBarAssets.BarPrefab;
        if (prefab.IsNullOrDestroyed())
        {
            return null;
        }

        GameObject child = Functions.GetChild(prefab, childName, false);
        if (child.IsNullOrDestroyed())
        {
            return null;
        }

        Image image = child.GetComponent<Image>();
        if (image.IsNullOrDestroyed() || image.sprite.IsNullOrDestroyed())
        {
            return null;
        }

        return Protect(image.sprite);
    }

    private static Sprite Resolve(HeadhunterStatKey key, out bool isFinal)
    {
        if (key.Tags != 0)
        {
            return ResolveTagged(key, out isFinal);
        }

        Sprite sprite = FromBar(((SP)key.StatId).ToString());
        if (!sprite.IsNullOrDestroyed())
        {
            isFinal = true;
            return sprite;
        }

        sprite = HeadhunterIconLoads.Poll(key, out bool settled);
        if (!sprite.IsNullOrDestroyed())
        {
            isFinal = true;
            return sprite;
        }

        sprite = ItemIcon();
        isFinal = settled && !sprite.IsNullOrDestroyed();
        return sprite;
    }

    private static Sprite ResolveTagged(HeadhunterStatKey key, out bool isFinal)
    {
        Sprite tagged = HeadhunterIconLoads.Poll(key, out bool settled);
        if (!tagged.IsNullOrDestroyed())
        {
            isFinal = true;
            return tagged;
        }

        Sprite fallback = For(key.StatId, 0, out bool fallbackFinal);
        isFinal = settled && fallbackFinal;
        return fallback;
    }

    private static Sprite ItemIcon()
    {
        int index = CustomUniqueLookup.IndexOf(CustomUniqueSpecs.Headhunter.UniqueId);
        return CustomItemIcons.Get(index);
    }

    private static Sprite Protect(Sprite sprite)
    {
        sprite.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        if (!sprite.texture.IsNullOrDestroyed())
        {
            sprite.texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }

        return sprite;
    }
}
