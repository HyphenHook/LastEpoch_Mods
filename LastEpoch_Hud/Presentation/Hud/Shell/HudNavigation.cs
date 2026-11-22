using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Pages;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

internal static class HudNavigation
{
    public static readonly HudSectionDefinition[] Sections =
    {
        new(
            HudSectionId.Utilities,
            "Utilities",
            true,
            Page(
                HudPageId.UtilitiesCharacter,
                "Character",
                UtilitiesCharacterPage.Build,
                UtilitiesCharacterPage.Show,
                UtilitiesCharacterPage.Hide,
                UtilitiesCharacterPage.Refresh
            ),
            Page(
                HudPageId.UtilitiesMultipliers,
                "Multipliers",
                UtilitiesMultipliersPage.Build,
                UtilitiesMultipliersPage.Show,
                UtilitiesMultipliersPage.Hide
            ),
            Page(
                HudPageId.UtilitiesCurrency,
                "Currency",
                (id, parent, _, font) => UtilitiesCurrencyPage.Build(id, parent, font),
                UtilitiesCurrencyPage.Show,
                UtilitiesCurrencyPage.Hide
            ),
            Page(
                HudPageId.UtilitiesBuffs,
                "Buffs",
                UtilitiesBuffsPage.Build,
                UtilitiesBuffsPage.Show,
                UtilitiesBuffsPage.Hide
            ),
            Page(
                HudPageId.UtilitiesHeorot,
                "Heorot",
                UtilitiesHeorotPage.Build,
                UtilitiesHeorotPage.Show,
                UtilitiesHeorotPage.Hide,
                UtilitiesHeorotPage.Refresh
            ),
            Page(
                HudPageId.UtilitiesQol,
                "QOL",
                UtilitiesQolPage.Build,
                UtilitiesQolPage.Show,
                UtilitiesQolPage.Hide,
                UtilitiesQolPage.Refresh
            )
        ),
        new(
            HudSectionId.Items,
            "Items",
            true,
            Page(
                HudPageId.ItemsDrop,
                "Drop",
                ItemsDropPage.Build,
                ItemsDropPage.Show,
                ItemsDropPage.Hide,
                ItemsDropPage.Refresh
            ),
            Page(
                HudPageId.ItemsTestDrops,
                "Test Drops",
                ItemsTestDropsPage.Build,
                ItemsTestDropsPage.Show,
                ItemsTestDropsPage.Hide
            ),
            // No builder; HudPageRoutes has no root, so it stays out of search.
            Page(
                HudPageId.ItemsForceDrop,
                "Force Drop",
                null,
                ItemsForceDropPage.Show,
                ItemsForceDropPage.Hide
            ),
            Page(
                HudPageId.ItemsCraftingSlot,
                "Crafting Slot",
                ItemsCraftingSlotPage.Build,
                ItemsCraftingSlotPage.Show,
                ItemsCraftingSlotPage.Hide,
                ItemsCraftingSlotPage.Refresh
            ),
            Page(
                HudPageId.ItemsCustomItems,
                CustomItemLocaleKeys.CustomItemsPage,
                ItemsCustomItemsPage.Build,
                ItemsCustomItemsPage.Show,
                ItemsCustomItemsPage.Hide,
                ItemsCustomItemsPage.Refresh
            )
        ),
        new(
            HudSectionId.World,
            "World",
            true,
            Page(
                HudPageId.WorldDifficulty,
                "Difficulty",
                WorldDifficultyPage.Build,
                WorldDifficultyPage.Show,
                WorldDifficultyPage.Hide,
                WorldDifficultyPage.Refresh
            ),
            Page(
                HudPageId.WorldMonoliths,
                "Monoliths",
                WorldMonolithsPage.Build,
                WorldMonolithsPage.Show,
                WorldMonolithsPage.Hide,
                WorldMonolithsPage.Refresh
            ),
            Page(
                HudPageId.WorldMisc,
                "Misc",
                WorldMiscPage.Build,
                WorldMiscPage.Show,
                WorldMiscPage.Hide,
                WorldMiscPage.Refresh
            ),
            Page(
                HudPageId.WorldCamera,
                "Camera",
                WorldCameraPage.Build,
                WorldCameraPage.Show,
                WorldCameraPage.Hide,
                WorldCameraPage.Refresh
            )
        ),
        new(
            HudSectionId.Skills,
            "Skills",
            true,
            Page(
                HudPageId.SkillsMinions,
                "Minions",
                SkillsMinionsPage.Build,
                SkillsMinionsPage.Show,
                SkillsMinionsPage.Hide,
                SkillsMinionsPage.Refresh
            ),
            Page(
                HudPageId.SkillsCompanions,
                "Companions",
                SkillsCompanionsPage.Build,
                SkillsCompanionsPage.Show,
                SkillsCompanionsPage.Hide,
                SkillsCompanionsPage.Refresh
            ),
            Page(
                HudPageId.SkillsSummon,
                "Summon",
                SkillsSummonPage.Build,
                SkillsSummonPage.Show,
                SkillsSummonPage.Hide,
                SkillsSummonPage.Refresh
            ),
            Page(
                HudPageId.SkillsQol,
                "QOL",
                SkillsQolPage.Build,
                SkillsQolPage.Show,
                SkillsQolPage.Hide,
                SkillsQolPage.Refresh
            )
        ),
    };

    public static IEnumerable<HudPageDefinition> Pages
    {
        get
        {
            foreach (var section in Sections)
            foreach (var page in section.Pages)
                yield return page;
        }
    }

    public static bool TryGetPage(
        HudPageId pageId,
        out HudSectionDefinition section,
        out HudPageDefinition page
    )
    {
        foreach (var candidateSection in Sections)
        foreach (var candidatePage in candidateSection.Pages)
            if (candidatePage.Id == pageId)
            {
                section = candidateSection;
                page = candidatePage;
                return true;
            }
        section = null;
        page = null;
        return false;
    }

    private static HudPageDefinition Page(
        HudPageId id,
        string label,
        Action<HudPageId, GameObject, GameObject, Font> build,
        Action show,
        Action hide,
        Action refresh = null
    ) => new(id, label, build, show, hide, refresh);
}
