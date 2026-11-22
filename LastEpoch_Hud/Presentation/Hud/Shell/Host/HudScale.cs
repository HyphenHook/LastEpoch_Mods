using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Shell.Host;

/// <summary>Keeps the HUD canvas scale equal to the game UI canvas scale.</summary>
internal static class HudScale
{
    private static Canvas _gameCanvas;
    private static Canvas _hudCanvas;

    public static void Apply()
    {
        if (Refs_Manager.game_uibase.IsNullOrDestroyed())
        {
            return;
        }
        if (_gameCanvas.IsNullOrDestroyed() || _hudCanvas.IsNullOrDestroyed())
        {
            return;
        }
        if (_hudCanvas.scaleFactor == _gameCanvas.scaleFactor)
        {
            return;
        }
        _hudCanvas.scaleFactor = _gameCanvas.scaleFactor;
    }

    public static void FindCanvases(GameObject hud)
    {
        if (_hudCanvas.IsNullOrDestroyed() && !hud.IsNullOrDestroyed())
        {
            _hudCanvas = hud.GetComponent<Canvas>();
        }
        if (Refs_Manager.game_uibase.IsNullOrDestroyed())
        {
            return;
        }
        if (!_gameCanvas.IsNullOrDestroyed() || Refs_Manager.game_uibase.canvases.Count == 0)
        {
            return;
        }
        _gameCanvas = Refs_Manager.game_uibase.canvases[0];
    }
}
