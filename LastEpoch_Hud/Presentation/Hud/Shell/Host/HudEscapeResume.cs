using LastEpoch_Hud.Scripts.Core.ModUI;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Shell.Host;

/// <summary>Clicks the game's Resume button when Esc is released while a menu is shown.</summary>
internal static class HudEscapeResume
{
    private static readonly KeyRelease _escape = new();

    public static void Tick()
    {
        if (Refs_Manager.epoch_input_manager.IsNullOrDestroyed())
        {
            return;
        }
        if (Refs_Manager.epoch_input_manager.isControllerActive)
        {
            return;
        }
        if (GamePauseMenu.ResumeButton.IsNullOrDestroyed())
        {
            return;
        }
        if (!_escape.Next(Input.GetKeyDown(KeyCode.Escape), Input.GetKeyUp(KeyCode.Escape)))
        {
            return;
        }
        GamePauseMenu.Resume();
    }
}
