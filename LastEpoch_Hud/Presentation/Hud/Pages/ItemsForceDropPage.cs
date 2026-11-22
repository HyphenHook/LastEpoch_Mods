using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using FD = LastEpoch_Hud.Scripts.ModUI.ForceDrop.ForceDropModel;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

// Items > Force Drop. The feature still reads the original prefab controls as
// its game-facing data model (hidden once at init), while ForceDropBuilder
// supplies the runtime view inside the redesigned HUD shell.
internal static class ItemsForceDropPage
{
    public static void Show()
    {
        if (FD.ContentObject.IsNullOrDestroyed())
        {
            return;
        }

        FD.SetActive(true);
    }

    public static void Hide()
    {
        FD.SetActive(false);
    }
}
