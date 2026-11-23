using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using LastEpoch_Hud.Scripts.Mods.Character.Volcanic;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>The three buffs of Volcanic Shaman’s Core, with a test toggle for each.</summary>
internal static class BossDropsVolcanicEffectsPage
{
    private static HudFormPage page;

    public static void Build(HudPageId id, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, id);
        if (page == null) return;
        // The wording and numbers come from the Volcanic CSV tables once the statuses are registered.
        const string waiting = "Available once the game's status list has loaded.";
        Add(VolcanicBuffKind.Wrath, "Bhuldar’s Anger", waiting);
        Add(VolcanicBuffKind.Hunger, "Logi’s Hunger", waiting);
        Add(VolcanicBuffKind.Vessel, "Herkir’s Vessel", waiting);
    }

    private static void Add(VolcanicBuffKind kind, string name, string description)
    {
        var card = page.AddCard(kind.ToString(), name);
        page.AddText(card, kind + "Description", VolcanicBuffs.Registered ? VolcanicBuffs.DescriptionFor(kind) : description, 110f);
        page.AddToggle(card, kind + "Test", "Enable test buff until removed",
            () => VolcanicBuffs.IsActive(Refs_Manager.player_actor, kind),
            enabled =>
            {
                if (VolcanicBuffs.IsActive(Refs_Manager.player_actor, kind) != enabled) VolcanicBuffs.ToggleTest(kind);
            });
    }

    public static void Show() => page?.Show();
    public static void Hide() => page?.Hide();
    public static void Refresh() => page?.RefreshValues();
}
