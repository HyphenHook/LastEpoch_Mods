using System.Collections.Generic;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Keeps our icon on the inventory and stash views that show a custom unique.</summary>
public static class CustomItemInventoryIcons
{
    private static readonly List<InventoryItemUI> _views = new();

    /// <summary>Tracks the view and applies the icon when it shows a custom unique, else untracks it.</summary>
    public static void Bind(InventoryItemUI ui)
    {
        if (ui.IsNullOrDestroyed())
        {
            return;
        }

        int index = IndexOf(ui);
        if (index < 0)
        {
            _views.Remove(ui);
            return;
        }

        if (!_views.Contains(ui))
        {
            _views.Add(ui);
        }
        CustomItemIcons.Apply(ui.contentImage, index);
    }

    /// <summary>Native image loads can finish after our postfix, so re-apply on tracked active views.</summary>
    public static void Refresh()
    {
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            RefreshAt(i);
        }
    }

    private static void RefreshAt(int i)
    {
        InventoryItemUI ui = _views[i];
        int index = IndexOf(ui);
        if (index < 0)
        {
            _views.RemoveAt(i);
            return;
        }

        if (!ui.gameObject.activeInHierarchy)
        {
            return;
        }
        CustomItemIcons.Apply(ui.contentImage, index);
    }

    private static int IndexOf(InventoryItemUI ui)
    {
        if (ui.IsNullOrDestroyed() || ui.EntryRef.IsNullOrDestroyed())
        {
            return -1;
        }

        return CustomUniqueItems.IndexOf(ui.EntryRef.data);
    }
}
