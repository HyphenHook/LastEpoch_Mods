using LastEpoch_Hud.Application.Testing;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

internal static class ItemsTestDropsPage
{
    private static HudFormPage page;
    private static TestActionCatalog catalog;

    internal static void Configure(TestActionCatalog actions) =>
        catalog = actions ?? throw new System.ArgumentNullException(nameof(actions));

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null) return;
        var card = page.AddCard("TestDrops", "Test Drops");
        if (catalog == null) throw new System.InvalidOperationException("Test actions must be configured by Bootstrap before building the HUD.");
        foreach (var action in catalog.Actions)
            page.AddButton(card, action.Id, action.Label, action.Execute);
    }

    public static void Show() => page?.Show();
    public static void Hide() => page?.Hide();
}
