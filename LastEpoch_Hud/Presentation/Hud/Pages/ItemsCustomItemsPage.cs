using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Menu;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>Items > Custom Items: one card per custom item. State resets on HUD re-bind (Build).</summary>
internal static class ItemsCustomItemsPage
{
    private static HudFormPage _page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        _page = HudFormPage.Build(parent, hud, font, pageId);
        if (_page == null)
        {
            return;
        }

        MagebloodMenu.Build(_page);
        MagebloodFlasksCard.Build(_page);
    }

    public static void Show()
    {
        if (_page == null)
        {
            return;
        }

        MagebloodMenu.OnShow();
        MagebloodFlasksCard.OnShow();
        _page.Show();
        Refresh();
    }

    public static void Hide() => _page?.Hide();

    public static void Refresh()
    {
        if (_page == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        bool menu = MagebloodMenu.Refresh(now);
        bool flasks = MagebloodFlasksCard.Refresh(now);
        if (!menu && !flasks)
        {
            return;
        }

        _page.RefreshValues();
    }
}
