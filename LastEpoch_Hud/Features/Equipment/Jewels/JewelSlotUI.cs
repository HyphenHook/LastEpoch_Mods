using LastEpoch_Hud.Scripts.Mods.Items.Accessories;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

/// <summary>The three Jewel equipment slots on the inventory panel, beside the Charm slot.</summary>
internal static class JewelSlotUI
{
    private static readonly AccessorySlotView[] views = CreateViews();
    private static int builtFor = -1;

    private static AccessorySlotView[] CreateViews()
    {
        var created = new AccessorySlotView[JewelEquipment.SlotCount];
        for (int i = 0; i < created.Length; i++)
            created[i] = new AccessorySlotView(JewelEquipment.FirstStoreIndex + i, "Jewel Equipment Slot " + (i + 1),
                "Jewel Item Icon", JewelVisuals.IconFor, () => OrbArt.GreyNamed("jewel_cobalt"));
        return created;
    }

    internal static void Tick()
    {
        if (builtFor != AccessoryPanel.Generation)
        {
            foreach (var view in views) view.Release();
            builtFor = AccessoryPanel.Generation;
        }
        if (AccessoryPanel.Box == null || !AccessoryPanel.Visible) return;
        for (int i = 0; i < views.Length; i++)
        {
            if (!views[i].IsBuilt)
                views[i].Build(AccessoryPanel.Box, AccessoryPanel.Layer, AccessoryPanel.Side, AccessoryPanel.JewelPosition(i + 1));
            views[i].Refresh(JewelEquipment.EquippedAt(i));
        }
    }
}
