using LastEpoch_Hud.Scripts.Mods.Items.Accessories;

namespace LastEpoch_Hud.Scripts.Mods.Items.Charms;

/// <summary>The Charm's equipment slot on the inventory panel.</summary>
internal static class CharmSlotUI
{
    private static readonly AccessorySlotView view = new(0, "Charm Equipment Slot", "Charm Item Icon");
    private static int builtFor = -1;

    internal static string Inspect() => AccessoryPanel.Inspect();

    internal static void Tick()
    {
        if (builtFor != AccessoryPanel.Generation)
        {
            view.Release();
            builtFor = AccessoryPanel.Generation;
        }
        if (AccessoryPanel.Box == null || !AccessoryPanel.Visible) return;
        if (!view.IsBuilt)
            view.Build(AccessoryPanel.Box, AccessoryPanel.Layer, AccessoryPanel.Side, AccessoryPanel.CharmPosition);
        view.Refresh(CharmEquipment.Equipped);
    }
}
