namespace LastEpoch_Hud.Scripts.Core.ModUI;

/// <summary>Tracks whether the mod holds the game's input lock, so it never releases a lock the game set.</summary>
public sealed class MenuInputLock
{
    public bool Held { get; private set; }

    public InputLockAction Next(bool menuOpen, bool controllerActive, bool lockActive)
    {
        bool wantLock = menuOpen && !controllerActive;
        if (wantLock && !lockActive)
        {
            Held = true;
            return InputLockAction.Take;
        }
        if (!wantLock && Held)
        {
            Held = false;
            return InputLockAction.Release;
        }
        return InputLockAction.None;
    }
}
