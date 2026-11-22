using System.IO;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Shell.Host;

/// <summary>Loads the mod's asset bundle that holds the HUD prefab.</summary>
internal static class HudBundleLoader
{
    private const string BundleName = "lastepochmods";

    public static bool TryLoad(out AssetBundle bundle)
    {
        // Do not read AssetBundleCreateRequest.assetBundle before async loading
        // completes. A fresh process can hit this race while Unity is starting.
        string assetPath = UnityEngine.Application.dataPath + "/../Mods/" + Main.mod_name + "/Assets";
        string bundlePath = Path.Combine(assetPath, BundleName);
        bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle.IsNullOrDestroyed())
        {
            Main.logger_instance?.Error("[Startup] HUD asset bundle unavailable: " + bundlePath);
            return false;
        }
        UnityEngine.Object.DontDestroyOnLoad(bundle);
        Main.logger_instance?.Msg("[Startup] HUD asset bundle loaded.");
        return true;
    }
}
