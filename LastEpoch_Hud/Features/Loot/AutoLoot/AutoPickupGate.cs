namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Shared cheap gate for the pickup patches: save present, toggle on, game scene.</summary>
internal static class AutoPickupGate
{
    public static bool IsOn(AutoPickupToggle toggle)
    {
        if (Save_Manager.instance is null)
        {
            return false;
        }

        if (!IsToggleOn(in Save_Manager.instance.data.Items.Pickup, toggle))
        {
            return false;
        }

        return Scenes.IsGameScene();
    }

    private static bool IsToggleOn(in Save_Manager.Data.Pickup pickup, AutoPickupToggle toggle)
    {
        return toggle switch
        {
            AutoPickupToggle.Gold => pickup.Enable_AutoPickup_Gold,
            AutoPickupToggle.Potions => pickup.Enable_AutoPickup_Potions,
            AutoPickupToggle.XpTome => pickup.Enable_AutoPickup_XpTome,
            AutoPickupToggle.FavorTome => pickup.Enable_AutoPickup_FavorTome,
            AutoPickupToggle.MemoryAmber => pickup.Enable_AutoPickup_MemoryAmber,
            AutoPickupToggle.Materials => pickup.Enable_AutoPickup_Materials,
            _ => false,
        };
    }
}
