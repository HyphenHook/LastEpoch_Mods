using HarmonyLib;

namespace LastEpoch_Hud.Scripts.Mods.Items.Pickup;

public class Items_AutoStore_OnInventoryOpen
{
    public static bool CanRun()
    {
        if ((Scenes.IsGameScene()) && (!Save_Manager.instance.IsNullOrDestroyed()))
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                return Save_Manager.instance.data.Items.Pickup.Enable_AutoStore_OnInventoryOpen;
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

    [HarmonyPatch(typeof(Il2CppLE.UI.PanelSystem.InventoryPanel), "OnOpen")]
    public class InventoryPanel_OnOpen
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (CanRun())
            {
                Items_AutoStore_OnPickup.StoreNow();
            }
        }
    }
}
