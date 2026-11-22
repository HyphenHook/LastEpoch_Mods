namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mjolner;

/// <summary>Says when the Mjolner hit hook must be added, moved or removed. Holds the trigger memory itself; never reset.</summary>
public sealed class MjolnerHookWatch
{
    private long _hookedPlayer;
    private bool _sceneLoaded;

    /// <summary>Flags a scene load: the next hook check re-hooks.</summary>
    public void MarkSceneLoaded()
    {
        _sceneLoaded = true;
    }

    /// <summary>True when the hook must be added or moved to the current player.</summary>
    public bool ShouldHook(bool enabled, long playerId, bool hookAlive)
    {
        if (!enabled || playerId == 0)
        {
            return false;
        }

        return _sceneLoaded || playerId != _hookedPlayer || !hookAlive;
    }

    /// <summary>Stores the hooked player and clears the scene flag.</summary>
    public void MarkHooked(long playerId)
    {
        _hookedPlayer = playerId;
        _sceneLoaded = false;
    }

    /// <summary>True when a hook exists but Mjolner is disabled or no player is left.</summary>
    public bool ShouldUnhook(bool enabled, long playerId)
    {
        return _hookedPlayer != 0 && (!enabled || playerId == 0);
    }

    /// <summary>Forgets the hooked player.</summary>
    public void MarkUnhooked()
    {
        _hookedPlayer = 0;
    }
}
