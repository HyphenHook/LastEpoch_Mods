using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell.Host;

/// <summary>Finds the game's pause menu, hides its default buttons and clicks Resume.</summary>
internal static class GamePauseMenu
{
    private const float ProbeInterval = 1f;
    private static float _nextProbe;
    private static GameObject _root;
    private static GameObject _defaultButtons;

    public static Button ResumeButton { get; private set; }

    public static bool IsOpen()
    {
        if (_root.IsNullOrDestroyed())
        {
            return false;
        }
        return _root.active;
    }

    public static void Probe(float now)
    {
        if (Refs_Manager.game_uibase.IsNullOrDestroyed())
        {
            return;
        }
        if (!_root.IsNullOrDestroyed() && !_defaultButtons.IsNullOrDestroyed())
        {
            return;
        }
        if (now < _nextProbe)
        {
            return;
        }
        _nextProbe = now + ProbeInterval;
        Find();
    }

    public static void HideDefaultButtons()
    {
        if (Refs_Manager.game_uibase.IsNullOrDestroyed())
        {
            return;
        }
        if (_defaultButtons.IsNullOrDestroyed() || !_defaultButtons.active)
        {
            return;
        }
        _defaultButtons.active = false;
    }

    public static void Resume()
    {
        if (ResumeButton.IsNullOrDestroyed())
        {
            return;
        }
        ResumeButton.onClick.Invoke();
    }

    private static void Find()
    {
        if (Refs_Manager.game_uibase.bottomScreenMenu.IsNullOrDestroyed())
        {
            return;
        }
        GameObject root = Refs_Manager.game_uibase.bottomScreenMenu.gameObject;
        // The old pause panel lived under "Menu Image". The bottom bar does not.
        // Only treat this object as the pause menu when that panel is actually there,
        // otherwise the title screen looks paused and clicks (Play Offline) are blocked.
        GameObject menuImage = FindDirectChild(root, "Menu Image");
        if (menuImage.IsNullOrDestroyed())
        {
            return;
        }
        _root = root;
        _defaultButtons = menuImage;
        FindResumeButton();
    }

    private static void FindResumeButton()
    {
        GameObject buttons = Functions.GetChild(_defaultButtons, "Buttons");
        if (buttons.IsNullOrDestroyed())
        {
            return;
        }
        GameObject resume = Functions.GetChild(buttons, "ResumeButton (1)", false);
        if (resume.IsNullOrDestroyed())
        {
            return;
        }
        ResumeButton = resume.GetComponent<Button>();
    }

    private static GameObject FindDirectChild(GameObject obj, string name)
    {
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            GameObject child = obj.transform.GetChild(i).gameObject;
            if (child.name == name)
            {
                return child;
            }
        }
        return null;
    }
}
