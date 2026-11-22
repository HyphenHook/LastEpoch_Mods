using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Bar;

/// <summary>Finds flask icon PNGs among the bundle asset names.</summary>
public static class MagebloodFlaskIconAssets
{
    public const string Folder = "/mageblood/texture2d/flask_icons/";
    private const string Extension = ".png";

    public static bool TryIconName(string assetName, out string iconName)
    {
        iconName = null;
        if (string.IsNullOrEmpty(assetName))
        {
            return false;
        }

        string path = assetName.Replace('\\', '/').ToLowerInvariant();
        int at = path.IndexOf(Folder, StringComparison.Ordinal);
        if (at < 0)
        {
            return false;
        }

        string file = path.Substring(at + Folder.Length);
        if (file.Contains('/') || !file.EndsWith(Extension, StringComparison.Ordinal))
        {
            return false;
        }

        string stem = file.Substring(0, file.Length - Extension.Length);
        if (stem.Length == 0)
        {
            return false;
        }

        iconName = stem;
        return true;
    }

    public static string Key(string iconName)
    {
        return string.IsNullOrWhiteSpace(iconName) ? null : iconName.ToLowerInvariant();
    }
}
