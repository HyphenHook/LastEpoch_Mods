using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.ModUI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell.Host;

/// <summary>Takes and releases the game's input lock while a menu is open.</summary>
internal static class HudInputLock
{
    private static readonly MenuInputLock _lock = new();

    public static void Apply(bool menuOpen)
    {
        EpochInputManager manager = Refs_Manager.epoch_input_manager;
        if (manager.IsNullOrDestroyed())
        {
            return;
        }
        switch (_lock.Next(menuOpen, manager.isControllerActive, manager.forceDisableInput))
        {
            case InputLockAction.Take:
                manager.forceDisableInput = true;
                break;
            case InputLockAction.Release:
                manager.forceDisableInput = false;
                break;
        }
    }
}
