using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Character;

public class Character_UnlockAllIdols
{
    public static void Update()
    {
        if (!Refs_Manager.item_containers_manager.IsNullOrDestroyed())
        {
            if (CanRun())
            {
                // Refresh the explicit cheat override; never write an invented
                // zero unlock state when no legitimate progress was captured.
                Refs_Manager.item_containers_manager.setIdolUnlockState(99, true);
            }
            else if (overrideApplied)
            {
                // Revert only an override that this mod actually applied.
                overrideApplied = false;
                if (backup_idx < 10)
                    Refs_Manager.item_containers_manager.setIdolUnlockState(98, true);
            }
        }
    }

    private static byte backup_idx = 99;
    private static byte unlock_idx = 9;
    private static bool overrideApplied;

    private static bool CanRun()
    {
        if (!Save_Manager.instance.IsNullOrDestroyed())
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                return Save_Manager.instance.data.Character.Cheats.Enable_UnlockAllIdolsSlots;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemContainersManager), "setIdolUnlockState")]
    public class ItemContainersManager_setIdolUnlockState
    {
        [HarmonyPrefix]
        static void Prefix(ref byte __0)
        {
            if (__0 < 10)
            {
                backup_idx = __0;
                if (CanRun())
                {
                    __0 = unlock_idx;
                    overrideApplied = true;
                }
            }
            else if (__0 == 99)
            {
                __0 = unlock_idx;
                overrideApplied = true;
            }
            else if (__0 == 98)
            {
                __0 = backup_idx;
            }
        }
    }
}
