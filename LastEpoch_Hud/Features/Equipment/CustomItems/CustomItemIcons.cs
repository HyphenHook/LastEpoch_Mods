using System;
using Il2CppInterop.Runtime;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Icons of the custom uniques, loaded once from the HUD bundle.</summary>
public static class CustomItemIcons
{
    private static readonly Sprite[] _sprites = new Sprite[CustomUniqueSpecs.All.Count];
    private static readonly Texture2D[] _textures = new Texture2D[CustomUniqueSpecs.All.Count];
    private static readonly string[] _assetNames = new string[CustomUniqueSpecs.All.Count];
    private static bool _attempted;

    /// <summary>Loads every icon in one pass over the bundle, once the bundle exists.</summary>
    public static void LoadOnce()
    {
        if (_attempted || Hud_Manager.asset_bundle.IsNullOrDestroyed())
        {
            return;
        }

        _attempted = true;
        LoadAll();
    }

    /// <summary>The item's icon, reloaded when Unity unloaded it; null when unavailable.</summary>
    public static Sprite Get(int index)
    {
        if (IsUnloaded(index))
        {
            Reload(index);
        }

        return _sprites[index].IsNullOrDestroyed() ? null : _sprites[index];
    }

    /// <summary>Puts the item's icon on the image; does nothing when either is missing.</summary>
    public static void Apply(Image image, int index)
    {
        if (image.IsNullOrDestroyed())
        {
            return;
        }

        Sprite sprite = Get(index);
        if (sprite == null)
        {
            return;
        }

        // A native override sprite can mask Image.sprite entirely.
        image.overrideSprite = null;
        image.sprite = sprite;
    }

    /// <summary>Loads a bundle asset as a sprite kept across scene changes. texture is null when the asset is a Sprite.</summary>
    internal static Sprite LoadBundleSprite(string assetName, out Texture2D texture)
    {
        // Load the texture explicitly and create a runtime sprite;
        // this avoids ambiguous PNG subassets and imported atlas bindings.
        // The hide flag keeps Unity from unloading the assets on scene change.
        texture = Hud_Manager
            .asset_bundle.LoadAsset(assetName, Il2CppType.Of<Texture2D>())
            ?.TryCast<Texture2D>();
        if (!texture.IsNullOrDestroyed())
        {
            texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            var created = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );
            created.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            return created;
        }

        texture = null;
        Sprite loaded = Hud_Manager
            .asset_bundle.LoadAsset(assetName, Il2CppType.Of<Sprite>())
            ?.TryCast<Sprite>();
        if (!loaded.IsNullOrDestroyed())
        {
            loaded.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }

        return loaded;
    }

    private static void LoadAll()
    {
        foreach (string name in AssetNames())
        {
            LoadIcon(name);
        }

        for (int i = 0; i < _sprites.Length; i++)
        {
            LoadFallback(i);
            LogUnavailable(i);
        }
    }

    private static string[] AssetNames()
    {
        try
        {
            return Hud_Manager.asset_bundle.GetAllAssetNames();
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Custom item icons");
            return Array.Empty<string>();
        }
    }

    private static void LoadIcon(string name)
    {
        LoadInto(CustomUniqueLookup.IconIndexOf(name), name);
    }

    private static void LoadFallback(int index)
    {
        int fallback = CustomUniqueLookup.IconFallbackIndexOf(index);
        if (fallback < 0 || !_sprites[index].IsNullOrDestroyed() || _assetNames[fallback] == null)
        {
            return;
        }

        LoadInto(index, _assetNames[fallback]);
    }

    private static void LoadInto(int index, string name)
    {
        if (index < 0 || !_sprites[index].IsNullOrDestroyed())
        {
            return;
        }

        try
        {
            _sprites[index] = LoadBundleSprite(name, out Texture2D texture);
            _textures[index] = texture;
            _assetNames[index] = _sprites[index].IsNullOrDestroyed() ? null : name;
            LogLoaded(index, name);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, $"{CustomUniqueSpecs.All[index].Name} icon");
        }
    }

    private static bool IsUnloaded(int index)
    {
        if (_assetNames[index] == null)
        {
            return false;
        }

        if (_sprites[index].IsNullOrDestroyed())
        {
            return true;
        }

        return _textures[index] != null && _textures[index].IsNullOrDestroyed();
    }

    private static void Reload(int index)
    {
        Main.logger_instance?.Warning(
            $"{CustomUniqueSpecs.All[index].Name} icon was unloaded; reloading"
        );
        string name = _assetNames[index];
        _sprites[index] = null;
        _textures[index] = null;
        _assetNames[index] = null;
        if (!Hud_Manager.asset_bundle.IsNullOrDestroyed())
        {
            LoadInto(index, name);
        }

        LogUnavailable(index);
    }

    private static void LogLoaded(int index, string name)
    {
        Sprite sprite = _sprites[index];
        if (sprite.IsNullOrDestroyed())
        {
            return;
        }

        string path = name.Replace('\\', '/').ToLowerInvariant();
        Main.logger_instance?.Msg(
            $"{CustomUniqueSpecs.All[index].Name} icon loaded: {path} ({sprite.rect.width}x{sprite.rect.height})"
        );
    }

    private static void LogUnavailable(int index)
    {
        if (!_sprites[index].IsNullOrDestroyed())
        {
            return;
        }

        Main.logger_instance?.Warning($"{CustomUniqueSpecs.All[index].Name} icon unavailable");
    }
}
