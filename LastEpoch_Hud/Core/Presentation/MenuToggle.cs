namespace LastEpoch_Hud.Scripts.Core.ModUI;

/// <summary>Decides the mod-menu open flag from the F3, Esc and scene state.</summary>
public static class MenuToggle
{
    public static bool Next(
        bool open,
        bool inGameScene,
        bool hudReady,
        bool menuKeyDown,
        bool escapeDown
    )
    {
        if (!inGameScene)
        {
            return false;
        }
        if (!hudReady)
        {
            return open;
        }
        return (open != menuKeyDown) && !escapeDown;
    }
}
