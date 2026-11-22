using System;
using Il2Cpp;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Shell.Host;

/// <summary>Instantiates the HUD prefab from the bundle and wires its sections.</summary>
internal static class HudPrefab
{
    private static readonly string[] _unusedNames =
    {
        "Base",
        "Btn_Menu_Maxroll",
        "Btn_Menu_NewItems",
        "Maxroll_Content",
        "NewItems_Content",
    };

    public static bool TryInstantiate(AssetBundle bundle, out GameObject hud)
    {
        hud = null;
        if (bundle.IsNullOrDestroyed())
        {
            return false;
        }
        string assetName = FindPrefabName(bundle);
        if (assetName == null)
        {
            return false;
        }
        UnityEngine.Object obj = bundle.LoadAsset(assetName);
        GameObject prefab = obj.TryCast<GameObject>();
        if (prefab.IsNullOrDestroyed())
        {
            Main.logger_instance.Error("Hud Manager : Hud Prefab not found");
            return false;
        }
        prefab.active = false; //Hide

        //Instantiate
        if (Main.debug)
        {
            Main.logger_instance.Msg("Hud Manager : Instantiate hud prefab");
        }
        hud = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        hud.AddComponent<UIMouseListener>(); //Block Mouse
        hud.AddComponent<WindowFocusManager>();
        UnityEngine.Object.DontDestroyOnLoad(hud);
        RemoveUnusedButtons(hud);
        return true;
    }

    public static void Bind(GameObject hud)
    {
        if (Main.debug)
        {
            Main.logger_instance.Msg("Hud Manager : Initialize hud refs");
        }
        SafeInit(
            "ForceDropModel",
            () =>
            {
                ForceDrop.ForceDropModel.GetRefs(hud);
                ForceDrop.ForceDropModel.HideLegacyControls();
                ForceDrop.ForceDropModel.SetActive(false);
            }
        );
        SafeInit("ModUI.SaveManager.BindHud", () => Settings.SaveManager.BindHud(hud));
        Main.logger_instance?.Msg("[Startup] HUD binding completed.");
        SafeInit("ModUI.HudLayout.Initialize", () => HudLayout.Initialize(hud));
    }

    private static string FindPrefabName(AssetBundle bundle)
    {
        foreach (string name in bundle.GetAllAssetNames())
        {
            if (
                Functions.Check_Prefab(name)
                && name.Contains("/hud/")
                && name.Contains("hud.prefab")
            )
            {
                if (Main.debug)
                {
                    Main.logger_instance.Msg("Hud Manager : Dhud prefab found");
                }
                return name;
            }
        }
        return null;
    }

    private static void SafeInit(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, name + " Init");
        }
    }

    private static void RemoveUnusedButtons(GameObject hud)
    {
        foreach (string name in _unusedNames)
        {
            GameObject button = Functions.FindDescendant(hud, name);
            if (!button.IsNullOrDestroyed())
            {
                UnityEngine.Object.Destroy(button);
            }
        }
    }
}
