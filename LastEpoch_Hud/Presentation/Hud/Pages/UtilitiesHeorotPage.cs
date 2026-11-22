using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using LastEpoch_Hud.Scripts.Mods.Character.Heorot;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

internal static class UtilitiesHeorotPage
{
    private static HudFormPage page;
    public static void Build(HudPageId id, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, id);
        if (page == null) return;
        // The wording and numbers come from the Heorot CSV tables once the statuses are registered.
        const string waiting = "Available once the game's status list has loaded.";
        Add(HeorotBuffKind.Power, "Heorot’s Power", waiting);
        Add(HeorotBuffKind.Energy, "Heorot’s Energy", waiting);
        Add(HeorotBuffKind.Defence, "Heorot’s Defence", waiting);
    }
    private static void Add(HeorotBuffKind kind, string name, string description)
    {
        var card = page.AddCard(kind.ToString(), name);
        page.AddText(card, kind + "Description", HeorotBuffs.Registered ? HeorotBuffs.DescriptionFor(kind) : description, 90f);
        page.AddToggle(card, kind + "Test", "Enable test buff until removed",
            () => HeorotBuffs.IsActive(Refs_Manager.player_actor, kind),
            enabled =>
            {
                if (HeorotBuffs.IsActive(Refs_Manager.player_actor, kind) != enabled) HeorotBuffs.ToggleTest(kind);
            });
    }
    public static void Show() => page?.Show();
    public static void Hide() => page?.Hide();
    public static void Refresh() => page?.RefreshValues();
}
