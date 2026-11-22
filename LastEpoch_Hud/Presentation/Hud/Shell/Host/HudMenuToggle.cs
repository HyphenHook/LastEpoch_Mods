using LastEpoch_Hud.Scripts.Core.ModUI;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Shell.Host;

/// <summary>Applies the F3, Esc and scene rules to the mod-menu open flag.</summary>
internal static class HudMenuToggle
{
    public static readonly KeyCode MenuKey = KeyCode.F3;

    public static bool Next(bool open, bool hudReady)
    {
        return MenuToggle.Next(
            open,
            Scenes.IsGameScene(),
            hudReady,
            Input.GetKeyDown(MenuKey),
            Input.GetKeyDown(KeyCode.Escape)
        );
    }
}
