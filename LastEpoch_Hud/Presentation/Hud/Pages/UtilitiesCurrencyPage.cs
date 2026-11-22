using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>Utilities &gt; Currency HUD page. State resets on HUD re-bind (Build).</summary>
internal static class UtilitiesCurrencyPage
{
    private static HudActionCard page;

    public static void Build(HudPageId pageId, GameObject parent, Font font)
    {
        page = HudActionCard.Build(
            parent,
            font,
            pageId,
            "Currency",
            new List<HudActionCard.Definition>
            {
                Action(
                    "Runes",
                    "Add All Runes x99",
                    Mods.Character.Character_Materials.GetAllRunesX99
                ),
                Action(
                    "Glyphs",
                    "Add All Glyphs x99",
                    Mods.Character.Character_Materials.GetAllGlyphsX99
                ),
                Action(
                    "Affixes",
                    "Add All Affixes x10",
                    Mods.Character.Character_Materials.GetAllShardsX10
                ),
                Action(
                    "AncientBones",
                    "Add Ancient Bones x10,000",
                    () => Mods.Character.Character_Materials.AddAncientBones(10000)
                ),
                Action(
                    "MemoryAmber",
                    "Add 10,000 Memory Amber",
                    Mods.Character.Character_MemoryAmber.Add10000
                ),
                Action(
                    "GoldMillion",
                    "Spawn 1,000,000 Gold",
                    Mods.Character.Character_Gold.SpawnMillion
                ),
                Action(
                    "SoulEmbers",
                    "Add 1,000 Soul Embers",
                    () => Mods.Character.Character_SoulEmbers.Add(1000)
                ),
            }
        );
    }

    public static void Show() => page?.Show();

    public static void Hide() => page?.Hide();

    private static HudActionCard.Definition Action(string id, string label, System.Action click) =>
        new()
        {
            Id = id,
            Label = label,
            Click = click,
        };
}
